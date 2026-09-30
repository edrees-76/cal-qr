<#
.SYNOPSIS
    يحوّل شعارات PNG في Assets إلى صور BMP بمقاسات معالج Inno Setup القياسية باستخدام System.Drawing فقط.

.DESCRIPTION
    - wizard_large.bmp (164x314) من nuclear-center-logo.png لصفحتي الترحيب والإنهاء.
    - wizard_small.bmp (55x58) من cal-qr-mark.png للصفحات الداخلية.
    كل صورة بخلفية بيضاء وهامش 10% وتحجيم عالي الجودة مع توسيط الشعار. صور ناتجة لا تُحفظ في Git.
#>

[CmdletBinding()]
param(
    [string]$AssetsSourceDir = (Join-Path $PSScriptRoot "..\..\CAL-QR\Assets\Logo"),
    [string]$OutputDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-PaddedBmp {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePngPath,
        [Parameter(Mandatory = $true)][string]$DestBmpPath,
        [Parameter(Mandatory = $true)][int]$TargetWidth,
        [Parameter(Mandatory = $true)][int]$TargetHeight,
        [double]$MarginRatio = 0.10
    )

    if (-not (Test-Path -LiteralPath $SourcePngPath)) {
        throw "ملف الشعار المصدر غير موجود: $SourcePngPath"
    }

    $sourceImage = [System.Drawing.Image]::FromFile($SourcePngPath)
    try {
        $canvas = New-Object System.Drawing.Bitmap($TargetWidth, $TargetHeight)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.Clear([System.Drawing.Color]::White)

                $usableWidth = $TargetWidth * (1.0 - $MarginRatio)
                $usableHeight = $TargetHeight * (1.0 - $MarginRatio)
                $scale = [Math]::Min($usableWidth / $sourceImage.Width, $usableHeight / $sourceImage.Height)
                $drawWidth = [int][Math]::Round($sourceImage.Width * $scale)
                $drawHeight = [int][Math]::Round($sourceImage.Height * $scale)
                $offsetX = [int][Math]::Round(($TargetWidth - $drawWidth) / 2)
                $offsetY = [int][Math]::Round(($TargetHeight - $drawHeight) / 2)

                $destRect = New-Object System.Drawing.Rectangle($offsetX, $offsetY, $drawWidth, $drawHeight)
                $graphics.DrawImage($sourceImage, $destRect)
            }
            finally {
                $graphics.Dispose()
            }

            $canvas.Save($DestBmpPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
            Write-Host "تم إنشاء: $DestBmpPath ($TargetWidth x $TargetHeight)"
        }
        finally {
            $canvas.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

New-PaddedBmp -SourcePngPath (Join-Path $AssetsSourceDir "nuclear-center-logo.png") -DestBmpPath (Join-Path $OutputDir "wizard_large.bmp") -TargetWidth 164 -TargetHeight 314
New-PaddedBmp -SourcePngPath (Join-Path $AssetsSourceDir "cal-qr-mark.png") -DestBmpPath (Join-Path $OutputDir "wizard_small.bmp") -TargetWidth 55 -TargetHeight 58

Write-Host "اكتمل إنشاء صور المعالج في: $OutputDir"
