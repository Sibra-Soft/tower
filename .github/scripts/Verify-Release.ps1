param([Parameter(Mandatory)][string]$Published)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
foreach ($name in @('Tower.exe', 'Tower.dll', 'Shared.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Published $name))) { throw "Missing release application file: $name" }
}
$forbidden = Get-ChildItem $Published -Recurse -Force | Where-Object {
    $_.Name -like 'Tower.Tools*' -or $_.Name -in @('Backups', 'PACKAGES', 'AirportPackages', 'Speech.json', 'Context', 'controllers.json') -or $_.Extension -in @('.bak', '.tmp')
}
if ($forbidden) { throw "Internal files, registrations or backups found in release: $($forbidden.Name -join ', ')" }
$source = Join-Path $repository 'Data'
$expected = @{}
foreach ($asset in Get-ChildItem $source -Recurse -File) {
    $relative = $asset.FullName.Substring($source.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1)
    if ($relative -notmatch '^(CYN[\\/]|GRAPHICS[\\/]MODELS[\\/]|AIRPORTS[\\/]CYN\.CFG$|[^\\/]+\.VOC$|SPEECH\.BIN$)') { continue }
    if ($relative -match '(^|[\\/])Backups([\\/]|$)' -or $asset.Extension -in @('.bak', '.tmp', '.dll')) { continue }
    $expected[$relative] = $asset.FullName
}
foreach ($relative in $expected.Keys) {
    $target = Join-Path (Join-Path $Published Data) $relative
    if (-not (Test-Path -LiteralPath $target)) { throw "Missing release asset: $relative" }
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $expected[$relative]).Hash) { throw "Changed release asset: $relative" }
}
foreach ($asset in Get-ChildItem (Join-Path $Published Data) -Recurse -File) {
    if ($asset.Name -cne $asset.Name.ToUpperInvariant()) { throw "Game asset filename must be uppercase: $($asset.FullName)" }
    $relative = $asset.FullName.Substring((Join-Path ([IO.Path]::GetFullPath($Published)) Data).Length + 1)
    if (-not $expected.ContainsKey($relative)) { throw "Unexpected add-on or game asset in base release: $relative" }
}
Write-Output "Verified Canyon release and $($expected.Count) byte-identical game assets."
