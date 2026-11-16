# Pravi assets\merac.ico - ikonicu pomoćnog programa (BronxSoft Netmetar Merac).
# Isti stil kao glavna ikonica: krem kućište, tamnozeleni ekran; na ekranu su merni stubići
# (merač po programu), umesto strelica glavnog programa.
# Pokretanje: powershell -ExecutionPolicy Bypass -File assets\napravi-ikonicu.ps1
Add-Type -AssemblyName System.Drawing

$izlaz = Join-Path $PSScriptRoot 'merac.ico'
$velicine = 16, 24, 32, 48, 64, 128, 256
$velika = 1024

function Zaobljen([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$bmp = New-Object System.Drawing.Bitmap $velika, $velika
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# kućište
$telo = Zaobljen 64 64 896 896 208
$cetka = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 241, 240, 235))
$g.FillPath($cetka, $telo)
$olovka = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 213, 210, 200)), 10
$g.DrawPath($olovka, $telo)

# ekran
$ekran = Zaobljen 176 208 672 576 104
$tamno = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 13, 40, 28))
$g.FillPath($tamno, $ekran)
$ivica = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 26, 66, 47)), 8
$g.DrawPath($ivica, $ekran)

# merni stubići (sve svetliji zdesna)
$stubovi = @(
    @{ x = 320; h = 188; boja = [System.Drawing.Color]::FromArgb(255, 176, 220, 198) },
    @{ x = 464; h = 316; boja = [System.Drawing.Color]::FromArgb(255, 208, 240, 222) },
    @{ x = 608; h = 444; boja = [System.Drawing.Color]::FromArgb(255, 240, 255, 249) }
)
$osnova = 716
foreach ($s in $stubovi) {
    $r = Zaobljen $s.x ($osnova - $s.h) 96 $s.h 26
    $c = New-Object System.Drawing.SolidBrush $s.boja
    $g.FillPath($c, $r)
}

# tanka linija osnove
$linija = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 150, 200, 172)), 10
$g.DrawLine($linija, 320, 760, 704, 760)

$g.Dispose()

# svaka veličina: umanjeno umetanje pa PNG
$slike = @()
foreach ($v in $velicine) {
    $mala = New-Object System.Drawing.Bitmap $v, $v
    $gm = [System.Drawing.Graphics]::FromImage($mala)
    $gm.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gm.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $gm.DrawImage($bmp, 0, 0, $v, $v)
    $gm.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $mala.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $slike += [pscustomobject]@{ Velicina = $v; Podaci = $ms.ToArray() }
    $ms.Dispose()
    $mala.Dispose()
}
$bmp.Dispose()

# ICO kontejner (PNG unutra, kao i glavna ikonica)
$fs = [System.IO.File]::Create($izlaz)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$slike.Count)
$pomak = 6 + 16 * $slike.Count
foreach ($s in $slike) {
    $b = if ($s.Velicina -ge 256) { 0 } else { $s.Velicina }
    $bw.Write([Byte]$b); $bw.Write([Byte]$b)
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$s.Podaci.Length); $bw.Write([UInt32]$pomak)
    $pomak += $s.Podaci.Length
}
foreach ($s in $slike) { $bw.Write($s.Podaci) }
$bw.Dispose(); $fs.Dispose()

"Napravljeno: $izlaz (" + (Get-Item $izlaz).Length + " B, " + $slike.Count + " veličina)"
