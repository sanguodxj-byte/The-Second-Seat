Add-Type -AssemblyName System.Drawing

$texturePath = "..\The Second Seat - Sideria\Textures\Narrators\Descent\Effects\Sideria"
$fileName = "DragonShadow.png"
$fullPath = Join-Path $texturePath $fileName

# Ensure directory exists
if (-not (Test-Path $texturePath)) {
    New-Item -ItemType Directory -Path $texturePath -Force | Out-Null
    Write-Host "Created directory: $texturePath"
}

# Create a bitmap
$width = 512
$height = 512
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set high quality rendering
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Clear with transparent color
$graphics.Clear([System.Drawing.Color]::Transparent)

# Create a semi-transparent black brush
$alpha = 180
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))

# Draw an ellipse (shadow shape)
# Make it slightly irregular or just a simple ellipse
# Let's make a elongated ellipse to represent a flying dragon shadow
$rect = New-Object System.Drawing.Rectangle(50, 100, 412, 312)
$graphics.FillEllipse($brush, $rect)

# Add some "wings" shadow hints
$wingBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($alpha - 40, 0, 0, 0))
$leftWing = New-Object System.Drawing.Rectangle(0, 200, 150, 100)
$rightWing = New-Object System.Drawing.Rectangle(362, 200, 150, 100)
# $graphics.FillEllipse($wingBrush, $leftWing)
# $graphics.FillEllipse($wingBrush, $rightWing)

# Save the image
$bitmap.Save($fullPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Cleanup
$graphics.Dispose()
$bitmap.Dispose()
$brush.Dispose()
$wingBrush.Dispose()

Write-Host "Created shadow texture at: $fullPath"