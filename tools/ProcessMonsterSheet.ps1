param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$MonsterSlug
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Test-IsBackground {
    param(
        [System.Drawing.Color]$Color
    )

    return ($Color.R -ge 246 -and $Color.G -ge 246 -and $Color.B -ge 246)
}

function Get-RowRanges {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$Threshold
    )

    $rows = New-Object System.Collections.Generic.List[int]
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        $count = 0
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if (-not (Test-IsBackground $Bitmap.GetPixel($x, $y))) {
                $count++
            }
        }

        if ($count -gt $Threshold) {
            [void]$rows.Add($y)
        }
    }

    $ranges = New-Object System.Collections.Generic.List[object]
    if ($rows.Count -eq 0) {
        return $ranges
    }

    $start = $rows[0]
    $previous = $rows[0]
    for ($i = 1; $i -lt $rows.Count; $i++) {
        if ($rows[$i] -ne ($previous + 1)) {
            [void]$ranges.Add([pscustomobject]@{ Start = $start; End = $previous })
            $start = $rows[$i]
        }

        $previous = $rows[$i]
    }

    [void]$ranges.Add([pscustomobject]@{ Start = $start; End = $previous })
    return $ranges
}

function Get-ColumnRanges {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$StartY,
        [int]$EndY,
        [int]$Threshold
    )

    $columns = New-Object System.Collections.Generic.List[int]
    for ($x = 0; $x -lt $Bitmap.Width; $x++) {
        $count = 0
        for ($y = $StartY; $y -le $EndY; $y++) {
            if (-not (Test-IsBackground $Bitmap.GetPixel($x, $y))) {
                $count++
            }
        }

        if ($count -gt $Threshold) {
            [void]$columns.Add($x)
        }
    }

    $ranges = New-Object System.Collections.Generic.List[object]
    if ($columns.Count -eq 0) {
        return $ranges
    }

    $start = $columns[0]
    $previous = $columns[0]
    for ($i = 1; $i -lt $columns.Count; $i++) {
        if ($columns[$i] -ne ($previous + 1)) {
            [void]$ranges.Add([pscustomobject]@{ Start = $start; End = $previous })
            $start = $columns[$i]
        }

        $previous = $columns[$i]
    }

    [void]$ranges.Add([pscustomobject]@{ Start = $start; End = $previous })
    return $ranges
}

function Get-ExpandedRectangle {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$StartX,
        [int]$EndX,
        [int]$StartY,
        [int]$EndY,
        [int]$Padding
    )

    $x = [Math]::Max(0, $StartX - $Padding)
    $y = [Math]::Max(0, $StartY - $Padding)
    $right = [Math]::Min($Bitmap.Width - 1, $EndX + $Padding)
    $bottom = [Math]::Min($Bitmap.Height - 1, $EndY + $Padding)

    return [System.Drawing.Rectangle]::new($x, $y, ($right - $x + 1), ($bottom - $y + 1))
}

function Get-CroppedBitmap {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [System.Drawing.Rectangle]$Rectangle
    )

    $result = New-Object System.Drawing.Bitmap($Rectangle.Width, $Rectangle.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage(
            $Bitmap,
            [System.Drawing.Rectangle]::new(0, 0, $Rectangle.Width, $Rectangle.Height),
            $Rectangle,
            [System.Drawing.GraphicsUnit]::Pixel
        )
    }
    finally {
        $graphics.Dispose()
    }

    return $result
}

function Remove-WhiteBackground {
    param(
        [System.Drawing.Bitmap]$Bitmap
    )

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $color = $Bitmap.GetPixel($x, $y)
            if (Test-IsBackground $color) {
                $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
            }
        }
    }
}

function New-FrameSheet {
    param(
        [System.Drawing.Bitmap[]]$Frames,
        [int]$FrameWidth,
        [int]$FrameHeight,
        [double]$Scale,
        [string]$OutputPath
    )

    $sheet = New-Object System.Drawing.Bitmap(($Frames.Count * $FrameWidth), $FrameHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($sheet)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        for ($i = 0; $i -lt $Frames.Count; $i++) {
            $frame = $Frames[$i]
            $targetWidth = [Math]::Max(1, [int][Math]::Round($frame.Width * $Scale))
            $targetHeight = [Math]::Max(1, [int][Math]::Round($frame.Height * $Scale))
            $offsetX = ($FrameWidth * $i) + [int][Math]::Floor(($FrameWidth - $targetWidth) / 2)
            $offsetY = $FrameHeight - $targetHeight

            $graphics.DrawImage(
                $frame,
                [System.Drawing.Rectangle]::new($offsetX, $offsetY, $targetWidth, $targetHeight),
                [System.Drawing.Rectangle]::new(0, 0, $frame.Width, $frame.Height),
                [System.Drawing.GraphicsUnit]::Pixel
            )
        }

        $sheet.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $sheet.Dispose()
    }
}

function New-PortraitImage {
    param(
        [System.Drawing.Bitmap]$Portrait,
        [string]$OutputPath
    )

    $canvas = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        $maxSize = 236.0
        $scale = [Math]::Min($maxSize / $Portrait.Width, $maxSize / $Portrait.Height)
        $targetWidth = [Math]::Max(1, [int][Math]::Round($Portrait.Width * $scale))
        $targetHeight = [Math]::Max(1, [int][Math]::Round($Portrait.Height * $scale))
        $offsetX = [int][Math]::Floor((256 - $targetWidth) / 2)
        $offsetY = 256 - $targetHeight

        $graphics.DrawImage(
            $Portrait,
            [System.Drawing.Rectangle]::new($offsetX, $offsetY, $targetWidth, $targetHeight),
            [System.Drawing.Rectangle]::new(0, 0, $Portrait.Width, $Portrait.Height),
            [System.Drawing.GraphicsUnit]::Pixel
        )

        $canvas.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $canvas.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source not found: $SourcePath"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$source = [System.Drawing.Bitmap]::new($SourcePath)
try {
    $rowRanges = Get-RowRanges -Bitmap $source -Threshold 10
    if ($rowRanges.Count -lt 2) {
        throw 'Could not detect sprite row and portrait row.'
    }

    $spriteRow = $rowRanges[0]
    $portraitRow = $rowRanges[$rowRanges.Count - 1]

    $spriteRanges = Get-ColumnRanges -Bitmap $source -StartY $spriteRow.Start -EndY $spriteRow.End -Threshold 5
    if ($spriteRanges.Count -lt 4) {
        throw 'Could not detect the four small sprite frames.'
    }

    $portraitRanges = Get-ColumnRanges -Bitmap $source -StartY $portraitRow.Start -EndY $portraitRow.End -Threshold 5
    if ($portraitRanges.Count -lt 1) {
        throw 'Could not detect portrait bounds.'
    }

    $spriteRects = @()
    foreach ($range in $spriteRanges) {
        $spriteRects += Get-ExpandedRectangle -Bitmap $source -StartX $range.Start -EndX $range.End -StartY $spriteRow.Start -EndY $spriteRow.End -Padding 8
    }

    $portraitRect = Get-ExpandedRectangle -Bitmap $source -StartX $portraitRanges[0].Start -EndX $portraitRanges[0].End -StartY $portraitRow.Start -EndY $portraitRow.End -Padding 12

    $sprites = @()
    foreach ($rect in $spriteRects) {
        $sprite = Get-CroppedBitmap -Bitmap $source -Rectangle $rect
        Remove-WhiteBackground -Bitmap $sprite
        $sprites += $sprite
    }

    $portrait = Get-CroppedBitmap -Bitmap $source -Rectangle $portraitRect
    Remove-WhiteBackground -Bitmap $portrait

    $maxSpriteWidth = ($sprites | Measure-Object -Property Width -Maximum).Maximum
    $maxSpriteHeight = ($sprites | Measure-Object -Property Height -Maximum).Maximum
    $commonScale = [Math]::Min(30.0 / $maxSpriteWidth, 30.0 / $maxSpriteHeight)

    $idleFrames = @($sprites[0], $sprites[1], $sprites[0], $sprites[1])
    $attackFrames = @($sprites[1], $sprites[2], $sprites[3])

    $idleOutput = Join-Path $OutputDir ("mon_{0}_idle.png" -f $MonsterSlug)
    $attackOutput = Join-Path $OutputDir ("mon_{0}_attack.png" -f $MonsterSlug)
    $portraitOutput = Join-Path $OutputDir ("mon_{0}_portrait.png" -f $MonsterSlug)

    New-FrameSheet -Frames $idleFrames -FrameWidth 32 -FrameHeight 32 -Scale $commonScale -OutputPath $idleOutput
    New-FrameSheet -Frames $attackFrames -FrameWidth 32 -FrameHeight 32 -Scale $commonScale -OutputPath $attackOutput
    New-PortraitImage -Portrait $portrait -OutputPath $portraitOutput

    Write-Output "Created:"
    Write-Output $idleOutput
    Write-Output $attackOutput
    Write-Output $portraitOutput
}
finally {
    $source.Dispose()
}
