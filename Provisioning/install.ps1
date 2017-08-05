param($Version = 'latest', $PrivateIp)

$Account = 'paulmarsy'
$Repo = 'OctopusNuGetDeploymentFeed'
$BinaryName = 'OctopusDeployNuGetFeed.exe'
$InstallDir = 'C:\OctopusDeployNuGetFeed'
$AppFilePath = Join-Path $InstallDir $BinaryName

$request = [System.Net.WebRequest]::Create("https://github.com/$Account/$Repo/releases/$Version/")
$request.AllowAutoRedirect = $false
$response=$request.GetResponse()

$downloadUri = $([String]$response.GetResponseHeader("Location")).Replace('tag','download') + '/' + $BinaryName

if (!(Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory
}

if (Test-Path $AppFilePath) {
    & $AppFilePath stop
    if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not stop" }

    & $AppFilePath uninstall
    if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not uninstall" }
}

Write-Host "Downloading $downloadUri to $AppFilePath"
Invoke-WebRequest -UseBasicParsing -Uri  $downloadUri -OutFile $AppFilePath

& $AppFilePath install -host:$PrivateIp
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not install" }

& $AppFilePath start
if ($LASTEXITCODE -ne 0) {
    Write-Warning "$BinaryName did not start, retrying..."

    Start-Sleep -Seconds 60
    & $AppFilePath start
    if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not start" }
}