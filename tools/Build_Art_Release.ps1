<#
  Build_Art_Release.ps1 — package the full LPC art set into a Release artifact.

  Maintainer tool (not shipped to consumers). Zips the local LPC clone's
  spritesheets/ + CREDITS.csv + LICENSE-ART.txt + a generated CREDITS.md into a
  single LpcArt-full.zip for upload to a GitHub Release. The package's
  "Tools/LPC/Import Bundled Art" then downloads + extracts this.

  Usage:
    pwsh tools/Build_Art_Release.ps1 `
        -LpcSource "D:/Projects/Universal-LPC-Spritesheet-Character-Generator" `
        -OutDir    "$env:TEMP/lpc-art-release"
#>
param(
    [string]$LpcSource = "D:/Projects/Universal-LPC-Spritesheet-Character-Generator",
    [string]$OutDir    = "$env:TEMP/lpc-art-release"
)

$ErrorActionPreference = "Stop"
$pkgRoot = Split-Path -Parent $PSScriptRoot

$sheets  = Join-Path $LpcSource "spritesheets"
$credits = Join-Path $LpcSource "CREDITS.csv"
$license = Join-Path $pkgRoot   "LICENSE-ART.txt"
if (-not (Test-Path $sheets))  { throw "spritesheets/ not found at $sheets" }
if (-not (Test-Path $credits)) { throw "CREDITS.csv not found at $credits" }
if (-not (Test-Path $license)) { throw "LICENSE-ART.txt not found at $license" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$creditsMd = Join-Path $OutDir "CREDITS.md"
$zipPath   = Join-Path $OutDir "LpcArt-full.zip"

# readable attribution header; CREDITS.csv holds the canonical per-file detail
$pngCount = (Get-ChildItem $sheets -Recurse -Filter *.png -File | Measure-Object).Count
@"
# LPC Character Art — Attribution

This art is from the Liberated Pixel Cup / Universal LPC Spritesheet Generator,
multi-licensed CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0 (see LICENSE-ART.txt).

You MUST credit the original artists. The canonical per-file attribution
(author, license, source URL for each sprite) is in **CREDITS.csv**, included in
this artifact. Reproduce the entries for the assets you ship.

- Sprite PNGs: $pngCount
- Source: https://github.com/sanderfrenken/Universal-LPC-Spritesheet-Character-Generator
"@ | Set-Content -Encoding utf8 $creditsMd

Write-Host "Zipping $pngCount PNGs + credits + license -> $zipPath ..."
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# bsdtar (built into Windows 10+) writes zip and is far faster than Compress-Archive
# for 145k files. Multiple -C switches place each input at the archive root.
& tar.exe -a -c -f $zipPath -C $LpcSource spritesheets CREDITS.csv -C $OutDir CREDITS.md -C $pkgRoot LICENSE-ART.txt
if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)" }

$mb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Built $zipPath  ($mb MB)"
Write-Host "Publish with:  gh release create art-vYYYYMMDD `"$zipPath`" -R TCLowe1982/LPC-Unity-Character-Creator -t 'LPC Art' -n 'Full LPC character art bundle'"
