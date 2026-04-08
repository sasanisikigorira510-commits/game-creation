param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [int]$OutputSize = 128,
    [int]$Padding = 10,
    [double]$BackgroundThreshold = 62.0,
    [double]$NeighborThreshold = 18.0
)

$drawingAssembly = 'C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.5\System.Drawing.Common.dll'
if (-not ([System.AppDomain]::CurrentDomain.GetAssemblies().Location -contains $drawingAssembly))
{
    Add-Type -Path $drawingAssembly
}

function Get-BorderAverage {
    param(
        [byte[]]$Bytes,
        [int]$Width,
        [int]$Height,
        [int]$Stride
    )

    [double]$sumR = 0
    [double]$sumG = 0
    [double]$sumB = 0
    [double]$count = 0

    for ($x = 0; $x -lt $Width; $x += 1)
    {
        foreach ($y in @(0, ($Height - 1)))
        {
            $index = ($y * $Stride) + ($x * 4)
            $sumB += $Bytes[$index + 0]
            $sumG += $Bytes[$index + 1]
            $sumR += $Bytes[$index + 2]
            $count += 1
        }
    }

    for ($y = 1; $y -lt ($Height - 1); $y += 1)
    {
        foreach ($x in @(0, ($Width - 1)))
        {
            $index = ($y * $Stride) + ($x * 4)
            $sumB += $Bytes[$index + 0]
            $sumG += $Bytes[$index + 1]
            $sumR += $Bytes[$index + 2]
            $count += 1
        }
    }

    return [pscustomobject]@{
        R = ($sumR / $count)
        G = ($sumG / $count)
        B = ($sumB / $count)
    }
}

function Get-ColorDistance {
    param(
        [double]$R1,
        [double]$G1,
        [double]$B1,
        [double]$R2,
        [double]$G2,
        [double]$B2
    )

    $dr = $R1 - $R2
    $dg = $G1 - $G2
    $db = $B1 - $B2
    return [Math]::Sqrt(($dr * $dr) + ($dg * $dg) + ($db * $db))
}

$source = [System.Drawing.Bitmap]::new($InputPath)
try
{
    $bitmap = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try
        {
            $graphics.DrawImage($source, 0, 0, $source.Width, $source.Height)
        }
        finally
        {
            $graphics.Dispose()
        }

        $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
        $lock = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $stride = [Math]::Abs($lock.Stride)
            $bytes = [byte[]]::new($stride * $bitmap.Height)
            [Runtime.InteropServices.Marshal]::Copy($lock.Scan0, $bytes, 0, $bytes.Length)

            $backgroundSample = Get-BorderAverage -Bytes $bytes -Width $bitmap.Width -Height $bitmap.Height -Stride $stride
            $visited = [bool[]]::new($bitmap.Width * $bitmap.Height)
            $background = [bool[]]::new($bitmap.Width * $bitmap.Height)
            $queue = [System.Collections.Generic.Queue[int]]::new()

            function Enqueue-Seed {
                param([int]$X, [int]$Y)

                $flatIndex = ($Y * $bitmap.Width) + $X
                if ($visited[$flatIndex])
                {
                    return
                }

                $byteIndex = ($Y * $stride) + ($X * 4)
                $b = [double]$bytes[$byteIndex + 0]
                $g = [double]$bytes[$byteIndex + 1]
                $r = [double]$bytes[$byteIndex + 2]
                $distance = Get-ColorDistance -R1 $r -G1 $g -B1 $b -R2 $backgroundSample.R -G2 $backgroundSample.G -B2 $backgroundSample.B

                if ($distance -gt $BackgroundThreshold)
                {
                    return
                }

                $visited[$flatIndex] = $true
                $background[$flatIndex] = $true
                $queue.Enqueue($flatIndex)
            }

            for ($x = 0; $x -lt $bitmap.Width; $x += 1)
            {
                Enqueue-Seed -X $x -Y 0
                Enqueue-Seed -X $x -Y ($bitmap.Height - 1)
            }

            for ($y = 1; $y -lt ($bitmap.Height - 1); $y += 1)
            {
                Enqueue-Seed -X 0 -Y $y
                Enqueue-Seed -X ($bitmap.Width - 1) -Y $y
            }

            while ($queue.Count -gt 0)
            {
                $flat = $queue.Dequeue()
                $x = $flat % $bitmap.Width
                $y = [int][Math]::Floor($flat / $bitmap.Width)
                $byteIndex = ($y * $stride) + ($x * 4)
                $currentB = [double]$bytes[$byteIndex + 0]
                $currentG = [double]$bytes[$byteIndex + 1]
                $currentR = [double]$bytes[$byteIndex + 2]

                foreach ($offset in @(@(-1, 0), @(1, 0), @(0, -1), @(0, 1)))
                {
                    $nx = $x + $offset[0]
                    $ny = $y + $offset[1]

                    if ($nx -lt 0 -or $nx -ge $bitmap.Width -or $ny -lt 0 -or $ny -ge $bitmap.Height)
                    {
                        continue
                    }

                    $neighborFlat = ($ny * $bitmap.Width) + $nx
                    if ($visited[$neighborFlat])
                    {
                        continue
                    }

                    $neighborByteIndex = ($ny * $stride) + ($nx * 4)
                    $nb = [double]$bytes[$neighborByteIndex + 0]
                    $ng = [double]$bytes[$neighborByteIndex + 1]
                    $nr = [double]$bytes[$neighborByteIndex + 2]

                    $distanceToAverage = Get-ColorDistance -R1 $nr -G1 $ng -B1 $nb -R2 $backgroundSample.R -G2 $backgroundSample.G -B2 $backgroundSample.B
                    $distanceToCurrent = Get-ColorDistance -R1 $nr -G1 $ng -B1 $nb -R2 $currentR -G2 $currentG -B2 $currentB

                    if ($distanceToAverage -gt $BackgroundThreshold -or $distanceToCurrent -gt $NeighborThreshold)
                    {
                        continue
                    }

                    $visited[$neighborFlat] = $true
                    $background[$neighborFlat] = $true
                    $queue.Enqueue($neighborFlat)
                }
            }

            for ($y = 0; $y -lt $bitmap.Height; $y += 1)
            {
                for ($x = 0; $x -lt $bitmap.Width; $x += 1)
                {
                    $flatIndex = ($y * $bitmap.Width) + $x
                    $byteIndex = ($y * $stride) + ($x * 4)

                    if ($background[$flatIndex])
                    {
                        $bytes[$byteIndex + 3] = 0
                        continue
                    }

                    $bytes[$byteIndex + 3] = 255
                }
            }

            $foregroundVisited = [bool[]]::new($bitmap.Width * $bitmap.Height)
            $largestComponentMembers = [System.Collections.Generic.List[int]]::new()

            for ($y = 0; $y -lt $bitmap.Height; $y += 1)
            {
                for ($x = 0; $x -lt $bitmap.Width; $x += 1)
                {
                    $seedIndex = ($y * $bitmap.Width) + $x
                    if ($foregroundVisited[$seedIndex] -or $background[$seedIndex])
                    {
                        continue
                    }

                    $componentQueue = [System.Collections.Generic.Queue[int]]::new()
                    $componentMembers = [System.Collections.Generic.List[int]]::new()
                    $componentQueue.Enqueue($seedIndex)
                    $foregroundVisited[$seedIndex] = $true

                    while ($componentQueue.Count -gt 0)
                    {
                        $current = $componentQueue.Dequeue()
                        $componentMembers.Add($current)
                        $cx = $current % $bitmap.Width
                        $cy = [int][Math]::Floor($current / $bitmap.Width)

                        foreach ($offset in @(@(-1, 0), @(1, 0), @(0, -1), @(0, 1)))
                        {
                            $nx = $cx + $offset[0]
                            $ny = $cy + $offset[1]

                            if ($nx -lt 0 -or $nx -ge $bitmap.Width -or $ny -lt 0 -or $ny -ge $bitmap.Height)
                            {
                                continue
                            }

                            $neighborIndex = ($ny * $bitmap.Width) + $nx
                            if ($foregroundVisited[$neighborIndex] -or $background[$neighborIndex])
                            {
                                continue
                            }

                            $foregroundVisited[$neighborIndex] = $true
                            $componentQueue.Enqueue($neighborIndex)
                        }
                    }

                    if ($componentMembers.Count -gt $largestComponentMembers.Count)
                    {
                        $largestComponentMembers = $componentMembers
                    }
                }
            }

            if ($largestComponentMembers.Count -eq 0)
            {
                throw "Could not isolate foreground object."
            }

            $keepForeground = [bool[]]::new($bitmap.Width * $bitmap.Height)
            foreach ($index in $largestComponentMembers)
            {
                $keepForeground[$index] = $true
            }

            $minX = $bitmap.Width
            $minY = $bitmap.Height
            $maxX = -1
            $maxY = -1

            for ($y = 0; $y -lt $bitmap.Height; $y += 1)
            {
                for ($x = 0; $x -lt $bitmap.Width; $x += 1)
                {
                    $flatIndex = ($y * $bitmap.Width) + $x
                    $byteIndex = ($y * $stride) + ($x * 4)

                    if (-not $keepForeground[$flatIndex])
                    {
                        $bytes[$byteIndex + 3] = 0
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
                throw "Could not isolate foreground pixels."
            }

            [Runtime.InteropServices.Marshal]::Copy($bytes, 0, $lock.Scan0, $bytes.Length)
        }
        finally
        {
            $bitmap.UnlockBits($lock)
        }

        $cropWidth = ($maxX - $minX) + 1
        $cropHeight = ($maxY - $minY) + 1
        $target = [System.Drawing.Bitmap]::new($OutputSize, $OutputSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            $g2 = [System.Drawing.Graphics]::FromImage($target)
            try
            {
                $g2.Clear([System.Drawing.Color]::Transparent)
                $g2.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $g2.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $maxContent = [Math]::Max(1, ($OutputSize - ($Padding * 2)))
                $scale = [Math]::Min(($maxContent / [double]$cropWidth), ($maxContent / [double]$cropHeight))
                $drawWidth = [int][Math]::Round($cropWidth * $scale)
                $drawHeight = [int][Math]::Round($cropHeight * $scale)
                $drawX = [int][Math]::Floor(($OutputSize - $drawWidth) / 2.0)
                $drawY = [int][Math]::Floor(($OutputSize - $drawHeight) / 2.0)

                $srcRect = [System.Drawing.Rectangle]::new($minX, $minY, $cropWidth, $cropHeight)
                $dstRect = [System.Drawing.Rectangle]::new($drawX, $drawY, $drawWidth, $drawHeight)
                $g2.DrawImage($bitmap, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally
            {
                $g2.Dispose()
            }

            $outputDirectory = Split-Path -Path $OutputPath -Parent
            if (-not [string]::IsNullOrEmpty($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory))
            {
                New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
            }

            $target.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $target.Dispose()
        }
    }
    finally
    {
        $bitmap.Dispose()
    }
}
finally
{
    $source.Dispose()
}
