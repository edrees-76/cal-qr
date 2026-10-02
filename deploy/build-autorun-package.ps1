<#
.SYNOPSIS
    Assembles the final CAL-QR distribution folder: AutoRun launcher + installer + Arabic user guide.

.DESCRIPTION
    1. Locates CAL-QRSetup_v<Version>.exe in <InstallerDir> (default deploy\Release).
    2. Creates <InstallerPath>.sha256 (format "HASH  FILENAME") if it does not exist; otherwise verifies it.
       NOTE: a hash generated here protects against corruption while copying; it does not prove the installer is the
       original one. Publish the official hash (GitHub Release) and compare when authenticity matters.
    3. Builds CAL-QR.AutoRun (Release; net48 so it runs before the system is installed).
    4. Creates <OutputDir> containing:
         CalQrAutoRun.exe (+ .config), autorun.inf,
         CAL-QRSetup_v<Version>.exe (+ .sha256),
         Docs\UserGuide.ar.pdf

.PARAMETER Version
    Installer version. Defaults to <Version> in CAL-QR\CAL-QR.csproj.

.PARAMETER InstallerDir
    Folder holding the installer. Defaults to deploy\Release.

.PARAMETER ArabicGuide
    Path to the Arabic user guide PDF (the only guide; the launcher opens it in both UI languages).

.PARAMETER OutputDir
    Destination folder. Defaults to deploy\output\CAL-QR-v<Version>-AutoRun. It is recreated on each run.

.EXAMPLE
    .\deploy\build-autorun-package.ps1 -ArabicGuide "D:\cal-qr\deploy\Release\CAL_QR_User_Guide_Arabic.pdf"
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$InstallerDir,
    [Parameter(Mandatory = $true)][string]$ArabicGuide,
    [string]$OutputDir,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
$autorunProject = Join-Path $repoRoot "CAL-QR.AutoRun\CAL-QR.AutoRun.csproj"
$autorunAssets = Join-Path $deployDir "autorun"

if (-not $Version) {
    $csproj = Get-Content (Join-Path $repoRoot "CAL-QR\CAL-QR.csproj") -Raw
    if ($csproj -notmatch '<Version>([^<]+)</Version>') { throw "Could not read <Version> from CAL-QR.csproj." }
    $Version = $Matches[1]
}
if (-not $InstallerDir) { $InstallerDir = Join-Path $deployDir "Release" }
if (-not $OutputDir) { $OutputDir = Join-Path $deployDir "output\CAL-QR-v$Version-AutoRun" }

$installerName = "CAL-QRSetup_v$Version.exe"
$installerPath = Join-Path $InstallerDir $installerName
$checksumPath = "$installerPath.sha256"

foreach ($required in @($installerPath, $ArabicGuide)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file not found: $required" }
}

Write-Host "=== Step 1/4: installer checksum ==="
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash
if (Test-Path -LiteralPath $checksumPath) {
    $expected = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0]
    if ($expected -ne $actual) { throw "Installer checksum mismatch. Expected $expected but got $actual." }
    Write-Host "Verified existing .sha256: $actual"
} else {
    $tmpChecksum = Join-Path ([System.IO.Path]::GetTempPath()) ("calqr-" + [guid]::NewGuid().ToString("N") + ".sha256")
    [System.IO.File]::WriteAllText($tmpChecksum, "$actual  $installerName`r`n", (New-Object System.Text.UTF8Encoding($false)))
    $checksumPath = $tmpChecksum
    Write-Host "No .sha256 found; generated one: $actual"
}

Write-Host "=== Step 2/4: build CAL-QR.AutoRun ($Configuration) ==="
& dotnet build $autorunProject --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
$buildDir = Join-Path $repoRoot "CAL-QR.AutoRun\bin\$Configuration\net48"

Write-Host "=== Step 3/4: assemble $OutputDir ==="
if (Test-Path -LiteralPath $OutputDir) { Remove-Item -LiteralPath $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $OutputDir "Docs") | Out-Null

Copy-Item -LiteralPath (Join-Path $buildDir "CalQrAutoRun.exe") -Destination $OutputDir
$exeConfig = Join-Path $buildDir "CalQrAutoRun.exe.config"
if (Test-Path -LiteralPath $exeConfig) { Copy-Item -LiteralPath $exeConfig -Destination $OutputDir }
Copy-Item -LiteralPath (Join-Path $autorunAssets "autorun.inf") -Destination $OutputDir
Copy-Item -LiteralPath $installerPath -Destination $OutputDir
Copy-Item -LiteralPath $checksumPath -Destination (Join-Path $OutputDir "$installerName.sha256")
Copy-Item -LiteralPath $ArabicGuide -Destination (Join-Path $OutputDir "Docs\UserGuide.ar.pdf")

Write-Host "=== Step 4/4: verify the assembled folder ==="
$expectedFiles = @("CalQrAutoRun.exe", "autorun.inf", $installerName, "$installerName.sha256", "Docs\UserGuide.ar.pdf")
foreach ($f in $expectedFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputDir $f) -PathType Leaf)) { throw "Missing in output folder: $f" }
}
$finalActual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $OutputDir $installerName)).Hash
$finalExpected = ((Get-Content -LiteralPath (Join-Path $OutputDir "$installerName.sha256") -Raw).Trim() -split '\s+')[0]
if ($finalActual -ne $finalExpected) { throw "Copied installer does not match its .sha256." }

Write-Host ""
Write-Host "Package ready: $OutputDir"
Get-ChildItem -LiteralPath $OutputDir -Recurse -File | ForEach-Object {
    "{0,12:N0}  {1}" -f $_.Length, $_.FullName.Substring($OutputDir.Length + 1)
}
