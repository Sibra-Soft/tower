param(
    [Parameter(Mandatory)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$destinationRoot = [IO.Path]::GetFullPath($Destination)
$fixtureParent = [IO.Path]::GetFullPath((Join-Path $repository 'obj')) + [IO.Path]::DirectorySeparatorChar
if (-not $destinationRoot.StartsWith($fixtureParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test data must be isolated below repository obj.' }
if (Test-Path -LiteralPath $destinationRoot) { throw 'Use a fresh test-data directory to prevent stale installed airports from affecting verification.' }
$data = Join-Path $destinationRoot 'Data'
New-Item -ItemType Directory -Force -Path $data | Out-Null
$source = Join-Path $repository 'Data'
foreach ($folder in @('CYN', 'GRAPHICS/MODELS')) {
    $target = Join-Path $data $folder
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $target -Recurse
}
New-Item -ItemType Directory -Force -Path (Join-Path $data 'AIRPORTS') | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'AIRPORTS/CYN.CFG') -Destination (Join-Path $data 'AIRPORTS/CYN.CFG')
Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -ieq '.VOC' -or $_.Name -ieq 'SPEECH.BIN' } | Copy-Item -Destination $data
Copy-Item -LiteralPath (Join-Path $repository 'Tests/Fixtures/settings.ini') -Destination (Join-Path $destinationRoot 'settings.ini')
Write-Output "Prepared isolated test data: $data"
