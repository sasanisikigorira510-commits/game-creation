param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [int]$OutputSize = 128,
    [int]$Padding = 10
)

Add-Type -AssemblyName System.Windows.Forms

$source = [System.Drawing.Bitmap]::new($SourcePath)
try
{
    $minX = $source.Width
    $minY = $source.Height
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $source.Height; $y += 1)
    {
        for ($x = 0; $x -lt $source.Width; $x += 1)
        {
            $pixel = $source.GetPixel($x, $y)
            if ($pixel.A -le 8)
            {
                continue
            }

            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }

    if ($maxX -lt $minX -or $maxY -lt $minY)
    {
        throw "No opaque frame content found in source image."
    }

    $cropX = [Math]::Max(0, $minX - $Padding)
    $cropY = [Math]::Max(0, $minY - $Padding)
    $cropWidth = [Math]::Min($source.Width - $cropX, ($maxX - $minX + 1) + ($Padding * 2))
    $cropHeight = [Math]::Min($source.Height - $cropY, ($maxY - $minY + 1) + ($Padding * 2))

    $cropRect = [System.Drawing.Rectangle]::new($cropX, $cropY, $cropWidth, $cropHeight)

    $cropped = [System.Drawing.Bitmap]::new($cropRect.Width, $cropRect.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $cropGraphics = [System.Drawing.Graphics]::FromImage($cropped)
        try
        {
            $cropGraphics.Clear([System.Drawing.Color]::Transparent)
            $cropGraphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $cropRect.Width, $cropRect.Height), $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        {
            $cropGraphics.Dispose()
        }

        $output = [System.Drawing.Bitmap]::new($OutputSize, $OutputSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($output)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $scale = [Math]::Min(($OutputSize / [double]$cropRect.Width), ($OutputSize / [double]$cropRect.Height))
                $drawWidth = [Math]::Max(1, [int][Math]::Round($cropRect.Width * $scale))
                $drawHeight = [Math]::Max(1, [int][Math]::Round($cropRect.Height * $scale))
                $drawX = [int][Math]::Floor(($OutputSize - $drawWidth) / 2.0)
                $drawY = [int][Math]::Floor(($OutputSize - $drawHeight) / 2.0)

                $graphics.DrawImage($cropped, [System.Drawing.Rectangle]::new($drawX, $drawY, $drawWidth, $drawHeight))
            }
            finally
            {
                $graphics.Dispose()
            }

            $outputDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutputPath))
            if (-not [System.IO.Directory]::Exists($outputDirectory))
            {
                [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
            }

            $output.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $output.Dispose()
        }
    }
    finally
    {
        $cropped.Dispose()
    }
}
finally
{
    $source.Dispose()
}
