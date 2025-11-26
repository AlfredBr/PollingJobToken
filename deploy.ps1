Stop-Process -Name PollingJobToken -Force -ErrorAction SilentlyContinue
dotnet clean
Remove-Item -Recurse -Force .\bin -ErrorAction SilentlyContinue
dotnet publish -c Release -r linux-arm64 --self-contained true

$nodes = @("node0", "node1", "node2", "node3")
$name = "aspnet"
$port = 5050

foreach ($node in $nodes) {
	ssh.exe alfredbr@$node "pkill -f $name"
    ssh.exe alfredbr@$node "mkdir -p ~/bin/cluster"
    ssh.exe alfredbr@$node "rm -rf ~/bin/cluster/*"
    scp.exe -r bin/Release/net10.0/linux-arm64/publish/* alfredbr@${node}:~/bin/cluster
	ssh.exe alfredbr@$node "mv ~/bin/cluster/PollingJobToken ~/bin/cluster/$name"
    ssh.exe alfredbr@$node "chmod +x ~/bin/cluster/$name"
	ssh.exe alfredbr@$node "cd ~/bin/cluster; nohup ./$name --urls http://0.0.0.0:$port" &
}

$healthResults = foreach ($node in $nodes) {
	$uri = "http://${node}:$port/health"
	try {
		$response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 5
		$hasExpectedShape = $response -and
			($response.PSObject.Properties.Name -contains "Status") -and
			($response.PSObject.Properties.Name -contains "Timestamp") -and
			($response.PSObject.Properties.Name -contains "Hostname")
		if (-not $hasExpectedShape -or $response.Status -ne "Healthy") {
			throw "Unexpected health payload returned from $uri"
		}
		[PSCustomObject]@{
			Node      = $node
			Status    = $response.Status
			Timestamp = $response.Timestamp
			Hostname  = $response.Hostname
			Error     = $null
		}
	}
	catch {
		Write-Error "Health check failed for ${node}: ${_}"
		[PSCustomObject]@{
			Node      = $node
			Status    = "Error"
			Timestamp = $null
			Hostname  = $null
			Error     = $_.Exception.Message
		}
	}
}

# Present a concise table of node health results for quick scanning
$healthResults | Format-Table -AutoSize