param($AppInsightsKey, $EncodedCustomDeployScript, $DownloadUri)

Write-Output "Starting install at $(Get-Date)"

$installDir = 'C:\OctopusDeployNuGetFeed'
if (!(Test-Path $installDir)) {
    New-Item -Path $installDir -ItemType Directory
}

$binaryName = 'OctopusDeployNuGetFeed.exe'
$appFilePath = Join-Path $installDir $binaryName

if (Test-Path $appFilePath) {
    & $appFilePath stop
    & $appFilePath uninstall
    $existingVersion = & $appFilePath version
    Write-Output "Existing version: $existingVersion"
}

if ([string]::IsNullOrWhiteSpace($DownloadUri)) {
    $request = [System.Net.WebRequest]::Create("https://github.com/paulmarsy/OctopusNuGetDeploymentFeed/releases/latest/")
    $request.AllowAutoRedirect = $false
    $DownloadUri = ([string]$request.GetResponse().GetResponseHeader("Location")).Replace('tag','download') + '/' + $binaryName
}
Write-Output "Downloading $DownloadUri to $appFilePath"

Invoke-WebRequest -UseBasicParsing -Uri $DownloadUri -OutFile $appFilePath -Verbose
$deployedVersion = & $appFilePath version
Write-Output "Version deployed: $deployedVersion"

& $appFilePath install -aikey:$AppInsightsKey
if ($LASTEXITCODE -ne 0) { throw "$appFilePath did not install, exit code: $LASTEXITCODE" }

if (!([string]::IsNullOrWhiteSpace($EncodedCustomDeployScript))) {
    $customScript = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($EncodedCustomDeployScript))
    [scriptblock]::Create($customScript).Invoke()
}

& $appFilePath start
if ($LASTEXITCODE -ne 0) { throw "$appFilePath did not start, exit code: $LASTEXITCODE" }