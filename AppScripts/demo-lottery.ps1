param(
	[string]$BaseUrl = "https://localhost:7171",
	[switch]$SkipCert
)

Write-Host "Submitting LotteryNumber job..."
$payload = @{} | ConvertTo-Json

$submit = Invoke-WebRequest -Method POST -Uri "$BaseUrl/jobs/lottery" -ContentType 'application/json' -Body $payload -SkipHttpErrorCheck:$true
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
		Write-Host "Completed: " -NoNewline -ForegroundColor Green 
		$dateObj = [DateTime]::Parse($job.Data.Date)
		$formattedDate = $dateObj.ToString("MMMM d, yyyy")
		$numbers = $job.Data.Numbers -join ", "
		Write-Host "The ficticious lottery numbers for $formattedDate were $numbers" -ForegroundColor Cyan
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
	Write-Warning "Maximum attempts reached. Canceling job $jobId ..."
	$cancelParams = @{ Method = 'DELETE'; Uri = $location; SkipHttpErrorCheck = $true }
	if ($SkipCert) { $cancelParams['SkipCertificateCheck'] = $true }
	$cancelResp = Invoke-WebRequest @cancelParams
	Write-Host "Cancel status: $($cancelResp.StatusCode)"
}
catch {
	Write-Warning "Cancel request error: $($_.Exception.Message)"
}

try {
	$purgeUri = if ($location -match '\?') { "$location&purge=true" } else { "$location?purge=true" }
	Write-Warning "Purging job $jobId ..."
	$purgeParams = @{ Method = 'DELETE'; Uri = $purgeUri; SkipHttpErrorCheck = $true }
	if ($SkipCert) { $purgeParams['SkipCertificateCheck'] = $true }
	$purgeResp = Invoke-WebRequest @purgeParams
	Write-Host "Purge status: $($purgeResp.StatusCode)"
}
catch {
	Write-Warning "Purge request error: $($_.Exception.Message)"
}

Write-Error "Maximum attempts ($maxAttempts) reached without completion"
exit 3