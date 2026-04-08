param(
    [Parameter(Mandatory = $true)]
    [string]$Directory,

    [Parameter(Mandatory = $true)]
    [string]$Filter,

    [int]$TargetSize = 512,
    [int]$Padding = 24,
    [ValidateSet('Center', 'BottomRight')]
    [string]$AnchorMode = 'BottomRight'
)

Add-Type -AssemblyName System.Drawing

$files = Get-ChildItem -Path $Directory -Filter $Filter | Where-Object { $_.Name -notlike '*.tmp.png*' }
foreach ($file in $files)
{
    $sourceBytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $sourceStream = New-Object System.IO.MemoryStream(, $sourceBytes)
    $sourceImage = [System.Drawing.Image]::FromStream($sourceStream, $true, $true)
    $source = New-Object System.Drawing.Bitmap($sourceImage)
    try
    {
        $minX = $source.Width
        $minY = $source.Height
        $maxX = -1
        $maxY = -1

        for ($y = 0; $y -lt $source.Height; $y++)
        {
            for ($x = 0; $x -lt $source.Width; $x++)
            {
                $pixel = $source.GetPixel($x, $y)
                if ($pixel.A -le 10)
                {
                    continue
                }

                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }

        if ($maxX -lt 0)
        {
            continue
        }

        $cropWidth = $maxX - $minX + 1
        $cropHeight = $maxY - $minY + 1
        $cropped = New-Object System.Drawing.Bitmap($cropWidth, $cropHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($cropped)
            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.DrawImage(
                    $source,
                    (New-Object System.Drawing.Rectangle(0, 0, $cropWidth, $cropHeight)),
                    $minX,
                    $minY,
                    $cropWidth,
                    $cropHeight,
                    [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally
            {
                $graphics.Dispose()
            }

            $fitSize = $TargetSize - ($Padding * 2)
            $scale = [Math]::Min($fitSize / [double]$cropWidth, $fitSize / [double]$cropHeight)
            $drawWidth = [int][Math]::Round($cropWidth * $scale)
            $drawHeight = [int][Math]::Round($cropHeight * $scale)
            if ($AnchorMode -eq 'BottomRight')
            {
                $offsetX = $TargetSize - $Padding - $drawWidth
                $offsetY = $TargetSize - $Padding - $drawHeight
            }
            else
            {
                $offsetX = [int][Math]::Round(($TargetSize - $drawWidth) / 2.0)
                $offsetY = [int][Math]::Round(($TargetSize - $drawHeight) / 2.0)
            }

            $output = New-Object System.Drawing.Bitmap($TargetSize, $TargetSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try
            {
                $graphicsOut = [System.Drawing.Graphics]::FromImage($output)
                try
                {
                    $graphicsOut.Clear([System.Drawing.Color]::Transparent)
                    $graphicsOut.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                    $graphicsOut.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                    $graphicsOut.DrawImage($cropped, (New-Object System.Drawing.Rectangle($offsetX, $offsetY, $drawWidth, $drawHeight)))
                }
                finally
                {
                    $graphicsOut.Dispose()
                }

                $tempPath = "$($file.FullName).tmp.png"
                if (Test-Path $tempPath)
                {
                    Remove-Item -Path $tempPath -Force
                }

                $output.Save($tempPath, [System.Drawing.Imaging.ImageFormat]::Png)
                if (Test-Path $file.FullName)
                {
                    Remove-Item -Path $file.FullName -Force
                }
                Move-Item -Path $tempPath -Destination $file.FullName -Force
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
        $sourceImage.Dispose()
        $sourceStream.Dispose()
    }
}
