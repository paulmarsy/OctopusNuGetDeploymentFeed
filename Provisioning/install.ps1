param($Version = 'latest', $AppInsightsKey)

Write-Host "Version: $Version"
if (![string]::IsNullOrWhiteSpace($AppInsightsKey)) {
    Write-Host 'Setting Application Insights Instrumentation Key...'
    [System.Environment]::SetEnvironmentVariable('AppInsightsInstrumentationKey', $AppInsightsKey, [System.EnvironmentVariableTarget]::Machine)
}

$BinaryName = 'OctopusDeployNuGetFeed.exe'
$InstallDir = 'C:\OctopusDeployNuGetFeed'
$AppFilePath = Join-Path $InstallDir $BinaryName

if ($Version -ne 'latest' -and (Test-Path $AppFilePath)) {
    $existingVersion = & $AppFilePath version
    if ($Version -eq $existingVersion) {
        Write-Host "Already on version $existingVersion"
        return
    }
}

if (Test-Path $AppFilePath) {
    & $AppFilePath stop
    & $AppFilePath uninstall
}

$request = [System.Net.WebRequest]::Create("https://github.com/paulmarsy/OctopusNuGetDeploymentFeed/releases/$Version/")
$request.AllowAutoRedirect = $false
$downloadUri = ([string]$request.GetResponse().GetResponseHeader("Location")).Replace('tag','download') + '/' + $BinaryName

Write-Host "Downloading $downloadUri to $AppFilePath"
if (!(Test-Path $InstallDir)) { New-Item -Path $InstallDir -ItemType Directory }
Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $AppFilePath -Verbose

& $AppFilePath install
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not install" }

& $AppFilePath start
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not start" }