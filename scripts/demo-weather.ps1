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
		$builder = "$($baseUri.Scheme)://$($locUri.Host):$($locUri.Port)$($locUri.AbsolutePath)"
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
		Write-Host "Attempt $($attempt): $($statusObj.status)" -ForegroundColor Yellow
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

Write-Error "Maximum attempts ($maxAttempts) reached without completion"
exit 3
