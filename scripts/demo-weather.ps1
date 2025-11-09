param(
	[string]$BaseUrl = "https://localhost:7171",
	[string]$City = "Seattle",
	[switch]$SkipCert
)

Write-Host "Submitting WeatherForecast job for '$City'..."
$payload = @{ city = $City } | ConvertTo-Json

$submit = Invoke-WebRequest -Method POST -Uri "$BaseUrl/jobs/weather" -ContentType 'application/json' -Body $payload -SkipHttpErrorCheck:$true
if ($submit.StatusCode -ne 202) {
	Write-Error "Unexpected status code: $($submit.StatusCode)"
	if ($submit.Content) { Write-Host $submit.Content }
	exit 1
}

$resp = $submit.Content | ConvertFrom-Json
$jobId = $resp.jobId
$location = $submit.Headers['Location']
if ($location -is [array]) {
	$location = $location[0]
}
if (-not $location) {
	$location = "$BaseUrl/jobs/$jobId"
}

# Normalize protocol to BaseUrl if mismatch to avoid self-signed https issues
try {
	$baseUri = [Uri]$BaseUrl
	$locUri = [Uri]$location
	if ($baseUri.Scheme -ne $locUri.Scheme) {
		$builder = "$("+$baseUri.Scheme+")://$("+$locUri.Host+"):$($locUri.Port)$($locUri.AbsolutePath)"
		$location = $builder
	}
}
catch { 
	# Ignore URI parse errors
}

Write-Host "Job submitted. Id=$jobId Location=$location"

# Poll until completed or failed
$maxAttempts = 30
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
	if ($attempt -gt 1) { 
		Start-Sleep -Seconds 2
	}
	try {
		$pollParams = @{ Method = 'GET'; Uri = $location; SkipHttpErrorCheck = $true }
		if ($SkipCert) { $pollParams['SkipCertificateCheck'] = $true }
		$poll = Invoke-WebRequest @pollParams
	}
	catch {
		Write-Warning "Request error: $($_.Exception.Message)"
		continue
	}
	if ($poll.StatusCode -eq 202) {
		$statusObj = $poll.Content | ConvertFrom-Json
		Write-Host "Attempt $($attempt): $($statusObj.status)" -ForegroundColor DarkGray
		continue
	}
	if ($poll.StatusCode -eq 200) {
		$job = $poll.Content | ConvertFrom-Json
		Write-Host "Completed:" -ForegroundColor Green
		$job | ConvertTo-Json -Depth 5
		$job.Data | Format-Table -AutoSize
		exit 0
	}
	if ($poll.StatusCode -eq 410) {
		Write-Error "Job expired or canceled"
		exit 2
	}
	Write-Host "Unexpected status $($poll.StatusCode)"
	if ($poll.Content) { Write-Host $poll.Content }
}

# If we reach here, attempts were exceeded: cancel then purge the job
try {
	Write-Warning "Maximum attempts (${maxAttempts}) reached. Canceling job. Id=${jobId}"
	$cancelParams = @{ Method = 'DELETE'; Uri = $location; SkipHttpErrorCheck = $true }
	if ($SkipCert) { $cancelParams['SkipCertificateCheck'] = $true }
	$cancelResp = Invoke-WebRequest @cancelParams
	Write-Host "Cancel status: $($cancelResp.StatusCode)"
	exit 3
}
catch {
	Write-Warning "Cancel request error: $($_.Exception.Message)"
}

try {
	$purgeUri = if ($location -match '\?') { "${location}&purge=true" } else { "${location}?purge=true" }
	Write-Warning "Purging job. Id=${jobId} Location=${purgeUri}"
	$purgeParams = @{ Method = 'DELETE'; Uri = $purgeUri; SkipHttpErrorCheck = $true }
	if ($SkipCert) { $purgeParams['SkipCertificateCheck'] = $true }
	$purgeResp = Invoke-WebRequest @purgeParams
	Write-Host "Purge status: $($purgeResp.StatusCode)"
	exit 3
}
catch {
	Write-Warning "Purge request error: $($_.Exception.Message)"
}

