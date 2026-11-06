# Altınkule logosundan arayüz için şeffaf PNG'ler üretir (Windows'un kendi WIC/WPF bileşenleriyle; ek paket yok).
# Kullanım (proje kökünde):  powershell -ExecutionPolicy Bypass -File docs\brand\logo-donustur.ps1
# Girdi : docs\brand\altinkule-logo-orijinal.webp   (açık gri #F7F7F7 zeminli)
# Çıktı : EmlakOfisi.WebUI\wwwroot\images\brand\
#   altinkule-logo.png        renkli logo, açık zemin için (giriş/hata sayfası), 480px
#   altinkule-logo-beyaz.png  koyu zemin için: antrasit yazılar beyaz (sol menü), 300px
# Not: zemin rengi (#F7F7F7) ve ikon kesimi (y<560) bu logoya özeldir; logo değişirse güncellenmeli.
#   altinkule-ikon-512.png / -192.png / -64.png   sadece kule + kavisler, kare (favicon, ileride PWA)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$src = Join-Path $root 'docs\brand\altinkule-logo-orijinal.webp'
$outDir = Join-Path $root 'EmlakOfisi.WebUI\wwwroot\images\brand'
New-Item -ItemType Directory -Force $outDir | Out-Null

# --- Oku (BGRA, düz alfa) ---
$fs = [IO.File]::OpenRead($src)
try {
    $dec = [Windows.Media.Imaging.BitmapDecoder]::Create($fs, 'PreservePixelFormat', 'OnLoad')
    $bmp = New-Object Windows.Media.Imaging.FormatConvertedBitmap($dec.Frames[0], [Windows.Media.PixelFormats]::Bgra32, $null, 0)
} finally { $fs.Close() }
$W = $bmp.PixelWidth; $H = $bmp.PixelHeight; $stride = $W * 4
$px = New-Object byte[] ($stride * $H); $bmp.CopyPixels($px, $stride, 0)

# --- Zemini saydamlaştır: zemine uzaklığa göre alfa, sonra rengi zeminden "ayır" ---
$bg = 247.0; $soft = 96.0
for ($i = 0; $i -lt $px.Length; $i += 4) {
    $b = $px[$i]; $g = $px[$i + 1]; $r = $px[$i + 2]
    $d = [Math]::Max([Math]::Abs($r - $bg), [Math]::Max([Math]::Abs($g - $bg), [Math]::Abs($b - $bg)))
    $a = [Math]::Min(1.0, $d / $soft)
    if ($a -lt 0.04) { $px[$i] = 0; $px[$i + 1] = 0; $px[$i + 2] = 0; $px[$i + 3] = 0; continue }
    foreach ($k in 0, 1, 2) {
        $c = $bg + ($px[$i + $k] - $bg) / $a
        $px[$i + $k] = [byte][Math]::Max(0, [Math]::Min(255, [Math]::Round($c)))
    }
    $px[$i + 3] = [byte][Math]::Round($a * 255)
}

function Get-Bbox([byte[]]$p, [int]$x0, [int]$y0, [int]$x1, [int]$y1) {
    $minX = $x1; $minY = $y1; $maxX = $x0; $maxY = $y0
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = $x0; $x -lt $x1; $x++) {
            if ($p[$y * $stride + $x * 4 + 3] -gt 8) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    return @($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
}

function New-Source([byte[]]$p) {
    [Windows.Media.Imaging.BitmapSource]::Create($W, $H, 96, 96, [Windows.Media.PixelFormats]::Bgra32, $null, $p, $stride)
}

# Kırp -> (isteğe bağlı kare tuval) -> ölçekle -> PNG
function Save-Png($source, $box, [int]$targetW, [bool]$square, [string]$name) {
    # Kırpma kutunun dışına taşmaz (ikonda harfler girmesin); boşluk kare tuvalde/saydam kenarda verilir
    $x = $box[0]; $y = $box[1]; $w = $box[2]; $h = $box[3]
    $crop = New-Object Windows.Media.Imaging.CroppedBitmap($source, (New-Object Windows.Int32Rect($x, $y, $w, $h)))
    $pre = New-Object Windows.Media.Imaging.FormatConvertedBitmap($crop, [Windows.Media.PixelFormats]::Pbgra32, $null, 0)

    $side = [int]([Math]::Max($w, $h) * 1.08)   # kenarlarda %4 boşluk
    $cw = if ($square) { $side } else { [int]($w * 1.04) }; $ch = if ($square) { $side } else { [int]($h * 1.04) }
    $scale = $targetW / $cw
    $outW = [int][Math]::Round($cw * $scale); $outH = [int][Math]::Round($ch * $scale)

    $dv = New-Object Windows.Media.DrawingVisual
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($dv, 'HighQuality')
    $dc = $dv.RenderOpen()
    $ox = (($cw - $w) / 2) * $scale; $oy = (($ch - $h) / 2) * $scale
    $dc.DrawImage($pre, (New-Object Windows.Rect($ox, $oy, ($w * $scale), ($h * $scale))))
    $dc.Close()
    $rtb = New-Object Windows.Media.Imaging.RenderTargetBitmap($outW, $outH, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)

    $enc = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $path = Join-Path $outDir $name
    $out = [IO.File]::Create($path); try { $enc.Save($out) } finally { $out.Close() }
    "{0,-28} {1}x{2}  {3:N0} KB" -f $name, $outW, $outH, ((Get-Item $path).Length / 1KB)
}

$color = New-Source $px
$full = Get-Bbox $px 0 0 $W $H

# Koyu zemin sürümü: düşük doygunluklu koyu pikseller (antrasit yazı) beyaz olur; altın tonlar kalır
$white = [byte[]]$px.Clone()
for ($i = 0; $i -lt $white.Length; $i += 4) {
    if ($white[$i + 3] -eq 0) { continue }
    $b = $white[$i]; $g = $white[$i + 1]; $r = $white[$i + 2]
    $mx = [Math]::Max($r, [Math]::Max($g, $b)); $mn = [Math]::Min($r, [Math]::Min($g, $b))
    if ($mx -lt 120 -and ($mx - $mn) -lt 40) { $white[$i] = 255; $white[$i + 1] = 255; $white[$i + 2] = 255 }
}
$whiteSrc = New-Source $white

# İkon: yazıların üstündeki kule + kavislerin tamamı (harfler y≈600'de başladığı için y<560 ile sınırlı).
# Kavisler üst üste bindiği için daha dar bir kesim kenarlarda kesik çizgi bırakıyordu; tamamı alınır.
$markBox = Get-Bbox $px 0 0 $W 560

# Genişlikler gösterilen boyutun 2 katı (net görünüm, küçük dosya): giriş sayfası ≤240px, menü 150px
Save-Png $color    $full    480 $false 'altinkule-logo.png'
Save-Png $whiteSrc $full    300 $false 'altinkule-logo-beyaz.png'
Save-Png $color    $markBox 512 $true  'altinkule-ikon-512.png'
Save-Png $color    $markBox 192 $true  'altinkule-ikon-192.png'
Save-Png $color    $markBox 64  $true  'altinkule-ikon-64.png'
