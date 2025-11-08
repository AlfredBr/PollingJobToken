# Polling Job Token API

## Overview

This sample demonstrates a token-based polling pattern for long-running operations (LROs). Instead of forcing clients to wait for work to finish synchronously, the API accepts a request, immediately acknowledges it with `202 Accepted`, and hands back an operation token that the caller can poll. The Microsoft REST API Guidelines and Google Cloud APIs both recommend this approach for operations that may take seconds or minutes to complete.

## Why Polling?

- `POST /jobs` records the work request and returns quickly.
- The response carries a token (or operation URL) that uniquely identifies the job.
- Clients poll `/jobs/{id}` at their preferred cadence.
- The server reports status along the way:
  - `202 Accepted` while processing is in progress.
  - `200 OK` (or `201 Created`) with the final payload when the job completes.
  - `4xx/5xx` if the job fails.

This design decouples client responsiveness from job duration, supports retries naturally, and keeps server resources free from long-held connections.

## Project Structure

- `Controllers/JobsController.cs` orchestrates job submission, cancellation, and status checks.
- `Services/IJobProcessor.cs` and implementations represent long-running work.
- `Services/IJobStore.cs` encapsulates job persistence; `InMemoryJobStore` and `CachedJobStore` illustrate different storage approaches.
- `Models/JobResult.cs` and `Models/JobStatus.cs` define the data contracts returned by the API.
- `Models/WeatherForecastRequest.cs` and `Models/WeatherForecastResponse.cs` capture the polling job input and payload delivered on completion.

## Prerequisites

- .NET SDK 10.0 (preview) or later to match the target framework specified in `api.csproj`.
- PowerShell 7+ (optional) for the demo script in `scripts/demo-weather.ps1`.

## Getting Started

1. Restore dependencies:
	```pwsh
	dotnet restore
	```
2. Run the API locally:
	```pwsh
	dotnet run --project api
	```
3. The service listens on the URL defined in `Properties/launchSettings.json` (by default `https://localhost:7182`).

## Core Workflow

1. **Submit a job**
	```http
	POST /jobs
	Content-Type: application/json

	{
	  "city": "Seattle",
	  "date": "2025-11-08"
	}
	```

	Example response:
	```http
	HTTP/1.1 202 Accepted
	Location: /jobs/7b57f728-7f66-4f7f-a788-4c9b6e0a6f1c
	Retry-After: 2

	{
	  "jobId": "7b57f728-7f66-4f7f-a788-4c9b6e0a6f1c",
	  "status": "Pending"
	}
	```

	The `date` field is optional; when omitted the processor selects a default forecast date.

2. **Poll for status**
	```http
	GET /jobs/7b57f728-7f66-4f7f-a788-4c9b6e0a6f1c
	```

	Possible responses:
	- `202 Accepted` with a body containing the current `JobStatus`.
	- `200 OK` with the final `JobResult`, whose `data` property holds a `WeatherForecastResponse` payload:
		```json
		{
		  "jobId": "7b57f728-7f66-4f7f-a788-4c9b6e0a6f1c",
		  "status": "Completed",
		  "data": {
		    "city": "Seattle",
		    "date": "2025-11-08",
		    "temperatureC": 11,
		    "summary": "Totals decrease overnight"
		  }
		}
		```
	- `404 Not Found` if the token is unknown or has expired.

3. **Handle completion or failure**
	- On success, persist or display the returned result.
	- On failure, inspect the error payload and decide whether to retry the original submission.

## API Surface

| Method | Route            | Description                             |
|--------|------------------|-----------------------------------------|
| POST   | `/jobs`          | Submit a new long-running job request.  |
| GET    | `/jobs/{id}`     | Retrieve current status or final result.|
| DELETE | `/jobs/{id}`     | Cancel a job that is still in progress. |

Sample requests are captured in `api.http`, which can be executed with the VS Code REST Client extension or with tools such as `curl`/`Invoke-WebRequest`.

## Demo Script

Run `scripts/demo-weather.ps1` to see the polling pattern end-to-end. The script submits a weather forecast job, polls until completion, and prints the resulting forecast.

## Testing the Workflow

- Use the `demo-weather.md` playbook to follow manual test steps.
- Add integration tests that mimic the polling loop by verifying transitions from `Pending` → `Running` → `Completed`/`Failed` in `JobStatus`.

## References

- Microsoft REST API Guidelines(https://github.com/microsoft/api-guidelines) (long-running operations section)
- Google Cloud API Design Guide (operation resources)

These documents inspired the token-based polling approach implemented here.
