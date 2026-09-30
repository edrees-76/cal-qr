<#
.SYNOPSIS
    يبني مثبِّت Windows (Setup.exe) لنظام CAL-QR بنفس نمط مثبِّت منظومة مصادر.

.DESCRIPTION
    1. dotnet publish نشر ذاتي الاكتفاء (self-contained) لمعمارية win-x64 (PublishSingleFile=false دائماً).
    2. deploy\assets\generate-wizard-images.ps1 لإنتاج صور المعالج (BMP).
    3. ISCC.exe (Inno Setup 6) على installer.iss مع /DPublishDir و/DAppVersion (من <Version> في CAL-QR.csproj).
    4. أرشفة الناتج في deploy\Release\v{الإصدار}\CAL-QRSetup_v{الإصدار}.exe.

.NOTES
    يتطلب Inno Setup 6 على جهاز البناء (لا يُثبَّت تلقائياً). لا توقيع رقمي.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $deployDir "..")
$projectPath = Join-Path $repoRoot "CAL-QR\CAL-QR.csproj"
$publishDir = Join-Path $repoRoot "CAL-QR\bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish"
$issPath = Join-Path $deployDir "installer.iss"

if (-not (Test-Path $projectPath)) {
    throw "ملف المشروع غير موجود: $projectPath"
}

$csprojContent = Get-Content -Path $projectPath -Raw
$versionMatch = [regex]::Match($csprojContent, "<Version>(.*?)</Version>")
if (-not $versionMatch.Success -or [string]::IsNullOrWhiteSpace($versionMatch.Groups[1].Value)) {
    throw "تعذّر استخلاص رقم الإصدار من <Version> داخل: $projectPath — لا قيمة افتراضية صامتة."
}
$appVersion = $versionMatch.Groups[1].Value.Trim()
Write-Host "رقم الإصدار المستخلص من CAL-QR.csproj: $appVersion"

Write-Host "=== الخطوة 1/4: dotnet publish ($RuntimeIdentifier, $Configuration، ذاتي الاكتفاء، ملف واحد: لا) ==="
& dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "فشل dotnet publish (رمز الخروج $LASTEXITCODE)."
}

if (-not (Test-Path (Join-Path $publishDir "CAL-QR.exe"))) {
    throw "اكتمل dotnet publish لكن CAL-QR.exe غير موجود في: $publishDir"
}

Write-Host "=== الخطوة 2/4: توليد صور معالج التثبيت (BMP) ==="
& (Join-Path $deployDir "assets\generate-wizard-images.ps1")

Write-Host "=== الخطوة 3/4: تصريف مثبِّت Inno Setup (ISCC.exe) ==="
if (-not (Test-Path $InnoSetupCompiler)) {
    throw "لم يُعثر على ISCC.exe في: $InnoSetupCompiler`nثبّت Inno Setup 6 أو مرّر المسار عبر -InnoSetupCompiler."
}

& $InnoSetupCompiler "/DPublishDir=$publishDir" "/DAppVersion=$appVersion" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "فشل تصريف Inno Setup (رمز الخروج $LASTEXITCODE)."
}

Write-Host "=== الخطوة 4/4: أرشفة المثبِّت حسب رقم الإصدار ==="
$outputInstallerPath = Join-Path $deployDir "output\CAL-QRSetup.exe"
if (-not (Test-Path $outputInstallerPath)) {
    throw "الملف الناتج غير موجود: $outputInstallerPath"
}

$releaseDir = Join-Path $deployDir "Release\v$appVersion"
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$archivedInstallerPath = Join-Path $releaseDir "CAL-QRSetup_v$appVersion.exe"
Copy-Item -Path $outputInstallerPath -Destination $archivedInstallerPath -Force

Write-Host "=== اكتمل بناء المثبِّت. الأرشيف: $archivedInstallerPath ==="
