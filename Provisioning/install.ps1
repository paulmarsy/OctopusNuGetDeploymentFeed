param($Version = 'latest', $AppInsightsKey, $Timestamp, $EncodedInstallScript)

Write-Output "Running on $(Get-Date)"
Write-Output "Version: $Version"

$BinaryName = 'OctopusDeployNuGetFeed.exe'
$InstallDir = 'C:\OctopusDeployNuGetFeed'
$AppFilePath = Join-Path $InstallDir $BinaryName

if ($Version -ne 'latest' -and (Test-Path $AppFilePath)) {
    $existingVersion = & $AppFilePath version
    if ($Version -eq $existingVersion) {
        Write-Output "Already on version $existingVersion"
        return
    }
}

if (Test-Path $AppFilePath) {
    & $AppFilePath stop
    & $AppFilePath uninstall
    $existingVersion = & $AppFilePath version
    Write-Output "Existing version: $existingVersion"
}

$request = [System.Net.WebRequest]::Create("https://github.com/paulmarsy/OctopusNuGetDeploymentFeed/releases/$Version/")
$request.AllowAutoRedirect = $false
$downloadUri = ([string]$request.GetResponse().GetResponseHeader("Location")).Replace('tag','download') + '/' + $BinaryName

Write-Output "Downloading $downloadUri to $AppFilePath"
if (!(Test-Path $InstallDir)) { New-Item -Path $InstallDir -ItemType Directory }
Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $AppFilePath -Verbose

$deployedVersion = & $AppFilePath version
Write-Output "Version deployed: $deployedVersion"

& $AppFilePath install -aikey:$AppInsightsKey
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not install" }

if (!([string]::IsNullOrWhiteSpace($EncodedInstallScript))) {
    $installScript = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($EncodedInstallScript))
    [scriptblock]::Create($installScript).Invoke()
}

& $AppFilePath start
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not start" }