param(
  [string]$Directory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Processed')
)

# Independent, read-only check of the five final forest PNGs.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Directory = [IO.Path]::GetFullPath($Directory)
$names = @('forest-far-mist', 'forest-far', 'forest-far-trees', 'forest-belt-mid', 'forest-near')
$issues = [System.Collections.Generic.List[string]]::new()

foreach ($name in $names) {
  $path = Join-Path $Directory "$name.png"
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    $issues.Add("$name : missing PNG")
    continue
  }

  $bitmap = [System.Drawing.Bitmap]::FromFile($path)
  try {
    if ($bitmap.Width -ne 2172 -or $bitmap.Height -ne 724 -or $bitmap.Width -ne 3 * $bitmap.Height) {
      $issues.Add("$name : expected 2172x724 (3:1), got $($bitmap.Width)x$($bitmap.Height)")
      continue
    }

    $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
      if ($data.Stride -le 0) { throw "$name : unsupported negative bitmap stride" }
      $bytes = New-Object byte[] ($data.Stride * $bitmap.Height)
      [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    } finally {
      $bitmap.UnlockBits($data)
    }

    $colors = [System.Collections.Generic.HashSet[uint32]]::new()
    $opaque = 0
    $transparent = 0
    $partial = 0
    $badBlocks = 0
    $firstVisible = 181
    $lastVisible = -1
    $upperClear = $true
    $midFloorOpaque = $true
    $treeLowerClear = $true
    $edgeAlphaMismatch = 0
    $edgeRgbSamples = 0
    $edgeRgbAbsTotal = 0
    $edgeRgbAbsMax = 0

    for ($by = 0; $by -lt 181; $by++) {
      $row = $by * 4 * $data.Stride
      for ($bx = 0; $bx -lt 543; $bx++) {
        $base = $row + $bx * 16
        $a = [int]$bytes[$base + 3]
        if ($a -eq 255) {
          $opaque++
          $rgb = ([uint32]$bytes[$base + 2] -shl 16) -bor
            ([uint32]$bytes[$base + 1] -shl 8) -bor [uint32]$bytes[$base]
          [void]$colors.Add($rgb)
          if ($by -lt $firstVisible) { $firstVisible = $by }
          $lastVisible = $by
        } elseif ($a -eq 0) {
          $transparent++
        } else {
          $partial++
        }

        if ($name -eq 'forest-near' -and $by -lt 126 -and $a -ne 0) { $upperClear = $false }
        if ($name -eq 'forest-belt-mid' -and $by -ge 121 -and $a -ne 255) { $midFloorOpaque = $false }
        if ($name -eq 'forest-far-trees' -and $by -ge 135 -and $a -ne 0) { $treeLowerClear = $false }

        # Inspect every pixel, not merely the 543x181 sample grid.
        for ($dy = 0; $dy -lt 4; $dy++) {
          $pixelRow = $base + $dy * $data.Stride
          for ($dx = 0; $dx -lt 4; $dx++) {
            $i = $pixelRow + $dx * 4
            if ($bytes[$i] -ne $bytes[$base] -or
                $bytes[$i + 1] -ne $bytes[$base + 1] -or
                $bytes[$i + 2] -ne $bytes[$base + 2] -or
                $bytes[$i + 3] -ne $bytes[$base + 3]) {
              $badBlocks++
              break
            }
          }
          if ($dx -lt 4) { break }
        }
      }

      # Compare the two sides at the source grid's row spacing. This is a
      # visual loop-seam indicator, not a pass/fail condition for parallax art.
      $left = $row
      $right = $row + (2172 - 1) * 4
      if ($bytes[$left + 3] -ne $bytes[$right + 3]) { $edgeAlphaMismatch++ }
      if ($bytes[$left + 3] -eq 255 -and $bytes[$right + 3] -eq 255) {
        $delta = 0
        for ($c = 0; $c -lt 3; $c++) {
          $d = [Math]::Abs([int]$bytes[$left + $c] - [int]$bytes[$right + $c])
          $delta += $d
          if ($d -gt $edgeRgbAbsMax) { $edgeRgbAbsMax = $d }
        }
        $edgeRgbAbsTotal += $delta
        $edgeRgbSamples++
      }
    }

    if ($badBlocks -gt 0) { $issues.Add("$name : $badBlocks nonuniform 4x4 blocks") }
    if ($partial -gt 0) { $issues.Add("$name : $partial partial-alpha grid pixels") }
    if ($colors.Count -gt 64) { $issues.Add("$name : $($colors.Count) opaque colors, expected at most 64") }
    if ($name -in @('forest-far-mist', 'forest-far') -and $opaque -ne 543 * 181) {
      $issues.Add("$name : background has transparent pixels")
    }
    if ($name -in @('forest-far-trees', 'forest-belt-mid', 'forest-near') -and $transparent -eq 0) {
      $issues.Add("$name : no transparent openings")
    }
    if (-not $midFloorOpaque) { $issues.Add("$name : lower third of mid path has transparent gaps") }
    if (-not $upperClear) { $issues.Add("$name : near layer intrudes into upper 70 percent") }
    if (-not $treeLowerClear) { $issues.Add("$name : far trees intrude into lower 25 percent") }

    $edgeMean = if ($edgeRgbSamples) { [Math]::Round($edgeRgbAbsTotal / (3 * $edgeRgbSamples), 1) } else { 'n/a' }
    Write-Output ("{0}: {1}x{2}; colors={3}; opaque={4}; clear={5}; partial={6}; bad4x4={7}; visibleRows={8}..{9}; edge alpha mismatches={10}/181; edge mean RGB delta={11}; edge max channel delta={12}" -f
      $name, $bitmap.Width, $bitmap.Height, $colors.Count, $opaque, $transparent, $partial,
      $badBlocks, $firstVisible, $lastVisible, $edgeAlphaMismatch, $edgeMean, $edgeRgbAbsMax)
  } finally {
    $bitmap.Dispose()
  }
}

if ($issues.Count) { throw ("Forest verification failed:`n" + ($issues -join "`n")) }
Write-Output 'VERIFIED: all five forest layers pass structural checks; edge metrics are informational.'
