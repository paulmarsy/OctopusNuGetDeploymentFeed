$Account = 'paulmarsy'
$Repo = 'OctopusNuGetDeploymentFeed'
$BinaryName = 'OctopusDeployNuGetFeed.exe'
$InstallDir = 'C:\OctopusDeployNuGetFeed'
$AppFilePath = Join-Path $InstallDir $BinaryName

$request = [System.Net.WebRequest]::Create("https://github.com/$Account/$Repo/releases/latest/")
$request.AllowAutoRedirect = $false
$response=$request.GetResponse()

$downloadUri = $([String]$response.GetResponseHeader("Location")).Replace('tag','download') + '/' + $BinaryName

if (!(Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory
}

Invoke-WebRequest -UseBasicParsing -Uri  $downloadUri -OutFile $AppFilePath

& $AppFilePath install
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not install" }

& $AppFilePath start
if ($LASTEXITCODE -ne 0) { throw "$BinaryName did not start" }