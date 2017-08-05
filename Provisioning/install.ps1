param($Version = 'latest', $PrivateIp)

Write-Host "Version: $Version"
Write-Host "PrivateIp: $PrivateIp"

$BinaryName = 'OctopusDeployNuGetFeed.exe'
$InstallDir = 'C:\OctopusDeployNuGetFeed'
$AppFilePath = Join-Path $InstallDir $BinaryName

if (!(Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory
}

if ($Version -ne 'latest' -and (Test-Path $AppFilePath)) {
    $existingVersion = & $AppFilePath version
    if ($Version -eq $existingVersion) {
        Write-Host "Already on version $existingVersion"
        return
    }
}

$request = [System.Net.WebRequest]::Create("https://github.com/paulmarsy/OctopusNuGetDeploymentFeed/releases/$Version/")
$request.AllowAutoRedirect = $false
$downloadUri = ([string]$request.GetResponse().GetResponseHeader("Location")).Replace('tag','download') + '/' + $BinaryName

if (Test-Path $AppFilePath) {
    & $AppFilePath stop
    & $AppFilePath uninstall
}

Write-Host "Downloading $downloadUri to $AppFilePath"
Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $AppFilePath -Verbose

& $AppFilePath install -host:$PrivateIp
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not install" }

& $AppFilePath start
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not start" }