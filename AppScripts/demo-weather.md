# Weather Forecast Asynchronous Job Demo

This document explains the design of the asynchronous job API and walks through the `scripts/demo-weather.ps1` script that exercises it.

## Overview
The API uses a token-based polling pattern for long-running operations:
- A client submits a job (e.g., generate a weather forecast) via a POST endpoint.
- The server immediately returns `202 Accepted` with a `jobId` and a `Location` header pointing to a polling URL.
- The client polls `GET /jobs/{jobId}` until the job transitions from `Pending`/`Processing` to a terminal state (`Completed`, `Failed`, `Canceled`).

## Key Endpoints
| Method | Route | Purpose |
| ------ | ----- | ------- |
| POST | `/jobs/weather` | Submit a weather forecast job. Returns202 with jobId token. |
| GET | `/jobs/{id}` | Poll for job status and retrieve result when completed. |
| DELETE | `/jobs/{id}` | Cancel a running job (if not already completed/failed). |

## Status Codes
- `202 Accepted`: Job still in progress. Body contains `{ status, jobId }`.
- `200 OK`: Job completed successfully. Body contains full `JobResult` including `Data` (the forecast).
- `410 Gone`: Job expired or canceled.
- `500 Internal Server Error`: Job failed.
- `404 Not Found`: Unknown job id (never existed or already evicted without tombstone).

## Design Components
### `JobSubmissionControllerBase<TRequest, TResult>`
Provides reusable logic to:
1. Create a job (via `IJobStore`).
2. Run background work using an injected `IJobProcessor<TRequest, TResult>`.
3. Update job status lifecycle (`Pending` → `Processing` → `Completed` or `Failed`).
4. Return standardized `202 Accepted` response with headers.

### `WeatherForecastController`
Specializes the base controller for `WeatherForecastRequest` → `WeatherForecastResponse` jobs at route `/jobs/weather`.

### `JobsController`
Separates polling/cancel responsibilities (`GET /jobs/{id}`, `DELETE /jobs/{id}`) from submission to keep responsibilities clean and allow many specialized submission controllers.

### `IJobProcessor<TRequest, TResult>`
Abstraction for background work. The weather implementation:
- Simulates delay (`Task.Delay`).
- Generates pseudo-random temperature and summary based on city & date for reproducible but fake results.

### `IJobStore` Implementations
- `CachedJobStore` (registered) keeps job state in memory with eviction strategy.
- Status transitions are mediated by helper methods (`SetProcessing`, `SetCompleted`, `SetFailed`, `TryCancel`).

## Lifecycle Sequence
1. Client POSTs JSON `{ "city": "Seattle" }` to `/jobs/weather`.
2. API responds `202 Accepted` with:
 ```json
 { "jobId": "<token>" }
 ```
 And `Location: /jobs/<token>` header.
3. Client polls `GET /jobs/<token>` every few seconds:
 - Receives `202` until work finishes.
4. When complete, `GET` returns `200` with full job result:
 ```json
 {
 "jobId": "<token>",
 "status": "Completed",
 "message": "Completed",
 "data": {
 "city": "Seattle",
 "date": "2025-11-08",
 "temperatureC":12,
 "summary": "Mild"
 }
 }
 ```

## Annotated Script (`scripts/demo-weather.ps1`)
```powershell
param(
 [string]$BaseUrl = "https://localhost:7171", # Base API URL (dev launchSettings https endpoint)
 [string]$City = "Seattle", # City to request forecast for
 [switch]$SkipCert # Optional: skip certificate validation for localhost https
)

Write-Host "Submitting WeatherForecast job for '$City'..."
$payload = @{ city = $City } | ConvertTo-Json # Build JSON body

# Submit the job; expect202 Accepted with jobId and Location
$submit = Invoke-WebRequest -Method POST -Uri "$BaseUrl/jobs/weather" -ContentType 'application/json' -Body $payload -SkipHttpErrorCheck:$true
if ($submit.StatusCode -ne202) { # Fail fast if not202
 Write-Error "Unexpected status code: $($submit.StatusCode)"
 if ($submit.Content) { Write-Host $submit.Content }
 exit1
}

$resp = $submit.Content | ConvertFrom-Json # Parse response JSON
$jobId = $resp.jobId
$location = $submit.Headers['Location'] # Polling URL from Location header
if ($location -is [array]) { $location = $location[0] } # Unwrap header array if needed
if (-not $location) { $location = "$BaseUrl/jobs/$jobId" } # Fallback if header missing

# Normalize scheme (http/https) if mismatch to avoid issues with mixed configs
try {
 $baseUri = [Uri]$BaseUrl
 $locUri = [Uri]$location
 if ($baseUri.Scheme -ne $locUri.Scheme) {
 $builder = "$($baseUri.Scheme)://$($locUri.Host):$($locUri.Port)$($locUri.AbsolutePath)"
 $location = $builder
 }
} catch { }

Write-Host "Job submitted. Id=$jobId Location=$location"

# Poll loop – repeat until terminal status or attempt limit
$maxAttempts =30
for ($attempt =1; $attempt -le $maxAttempts; $attempt++) {
 Start-Sleep -Seconds2 # Backoff between polls
 try {
 $pollParams = @{ Method = 'GET'; Uri = $location; SkipHttpErrorCheck = $true }
 if ($SkipCert) { $pollParams['SkipCertificateCheck'] = $true }
 $poll = Invoke-WebRequest @pollParams # Fetch current job state
 } catch {
 Write-Warning "Request error: $($_.Exception.Message)"; continue
 }

 if ($poll.StatusCode -eq202) { # Still processing
 $statusObj = $poll.Content | ConvertFrom-Json
 Write-Host "Attempt $($attempt): $($statusObj.status)" -ForegroundColor Yellow
 continue
 }
 if ($poll.StatusCode -eq200) { # Completed successfully
 $job = $poll.Content | ConvertFrom-Json
 Write-Host "Completed:" -ForegroundColor Green
 $job | ConvertTo-Json -Depth5 # Show full job envelope
 $job.Data | Format-Table -AutoSize # Tabular view of forecast
 exit0
 }
 if ($poll.StatusCode -eq410) { # Expired or canceled
 Write-Error "Job expired or canceled"; exit2
 }

 Write-Host "Unexpected status $($poll.StatusCode)" # Any other status (404,500)
 if ($poll.Content) { Write-Host $poll.Content }
}

Write-Error "Maximum attempts ($maxAttempts) reached without completion"; exit3
```

## Usage Examples
Submit and poll with defaults:
```powershell
./scripts/demo-weather.ps1
```
Specify city and skip certificate validation:
```powershell
./scripts/demo-weather.ps1 -City "Paris" -SkipCert
```
Point to HTTP (if launched without HTTPS):
```powershell
./scripts/demo-weather.ps1 -BaseUrl http://localhost:5089 -City "Berlin"
```

## Extending for New Job Types
To add a new job kind:
1. Define request & response models.
2. Implement `IJobProcessor<NewRequest, NewResponse>`.
3. Register the processor in `Program.cs`.
4. Create a controller inheriting `JobSubmissionControllerBase<NewRequest, NewResponse>` at a unique route (e.g., `/jobs/image-render`).
5. Reuse existing polling via `GET /jobs/{id}`.

## Advantages of the Pattern
- Separation of concerns (submission vs polling).
- Strongly typed per job without polymorphic POST bodies.
- Simple polling contract shared across all job types.
- Extensible with minimal boilerplate.

## Potential Enhancements
- Include `Retry-After` in GET responses while pending.
- Add cancellation token propagation from controllers.
- Introduce exponential backoff hints.
- Persist jobs (database) instead of in-memory cache.
- Add tombstone metadata for recently finished jobs.

## Summary
The asynchronous job API provides a clean, scalable pattern for long-running operations. The PowerShell script demonstrates the complete lifecycle: submit, poll pending state, and retrieve final data.
