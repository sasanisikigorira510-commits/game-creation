param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [int]$Column,

    [Parameter(Mandatory = $true)]
    [int]$Row,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [int]$Columns = 2,
    [int]$Rows = 2
)

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Bitmap]::FromFile($SourcePath)
try {
    $cellWidth = [int]($source.Width / $Columns)
    $cellHeight = [int]($source.Height / $Rows)
    $x = $Column * $cellWidth
    $y = $Row * $cellHeight

    $target = New-Object System.Drawing.Bitmap($cellWidth, $cellHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($target)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.DrawImage(
                $source,
                (New-Object System.Drawing.Rectangle(0, 0, $cellWidth, $cellHeight)),
                $x,
                $y,
                $cellWidth,
                $cellHeight,
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {
            $graphics.Dispose()
        }

        $outputDirectory = Split-Path -Path $OutputPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        }

        $target.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $target.Dispose()
    }
}
finally {
    $source.Dispose()
}
