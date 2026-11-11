param (
    [string]$Version = '1.0.0',
    [switch]$PublishOnly,
    [switch]$SkipPublish,
    [string]$MakeNSIS
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath("$PSScriptRoot/..")
$publish = [IO.Path]::GetFullPath("$PSScriptRoot/publish")
$generated = Join-Path $PSScriptRoot 'generated'
$output = Join-Path $PSScriptRoot 'output'

if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') { throw 'Use a version such as 1.0.0 or 1.0.0-rc.1.' }
$numbers = $Version.Split('-')[0].Split('.')

if ($numbers | Where-Object { [long]$_ -gt 65535 }) { throw 'Windows version components must not exceed 65535.' }
if ($PublishOnly -and $SkipPublish) { throw 'PublishOnly and SkipPublish cannot be combined.' }
if ($publish -ne (Join-Path (Join-Path $repository 'Setup') 'publish')) { throw 'Invalid setup staging path.' }
if (-not $SkipPublish) {
    # This fixed, generated staging directory must be clean so removed release files cannot survive.
    if (Test-Path -LiteralPath $publish) {
        if ((Get-Item -LiteralPath $publish).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Setup/publish may not be a filesystem link.' }
        Remove-Item -LiteralPath $publish -Recurse -Force
    }
    dotnet publish (Join-Path $repository 'src/Tower.WinForms/Tower.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Tower publication failed.' }
    # Never distribute the developer's controller selection, local paths or display preferences.
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'default-settings.ini') -Destination (Join-Path $publish 'settings.ini') -Force
}
& (Join-Path $repository '.github/scripts/Verify-Release.ps1') -Published $publish

if (-not (Test-Path -LiteralPath (Join-Path $publish 'settings.ini'))) { throw 'The payload requires settings.ini.' }

New-Item -ItemType Directory -Force -Path $generated, $output | Out-Null

$utf8 = New-Object Text.UTF8Encoding($true)
$install = New-Object 'Collections.Generic.List[string]'
$uninstall = New-Object 'Collections.Generic.List[string]'
$directories = New-Object 'Collections.Generic.HashSet[string]'
$lastDirectory = $null
$files = Get-ChildItem -LiteralPath $publish -File -Recurse | Sort-Object FullName

foreach ($file in $files) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Payload links are not supported.' }
    $relative = $file.FullName.Substring($publish.Length + 1)
    if ($relative -match '["$\r\n]' -or $relative.Contains('..')) { throw "Unsupported payload filename: $relative" }
    if ($relative -ieq 'settings.ini') { continue }
    $directory = [IO.Path]::GetDirectoryName($relative)
    if ($directory -ne $lastDirectory) {
        $install.Add(('SetOutPath "$INSTDIR\{0}"' -f $directory))
        $lastDirectory = $directory
    }
    $install.Add(('File "${{PAYLOAD_ROOT}}\{0}"' -f $relative))
    $uninstall.Add(('Delete "$INSTDIR\{0}"' -f $relative))
    while ($directory) {
        [void]$directories.Add($directory)
        $directory = [IO.Path]::GetDirectoryName($directory)
    }
}

foreach ($directory in $directories | Sort-Object Length -Descending) { 
    $uninstall.Add(('RMDir "$INSTDIR\{0}"' -f $directory)) 
}

[IO.File]::WriteAllLines((Join-Path $generated 'install-files.nsh'), $install, $utf8)
[IO.File]::WriteAllLines((Join-Path $generated 'uninstall-files.nsh'), $uninstall, $utf8)

if ($PublishOnly) { Write-Output "Verified installer payload: $publish"; return }
if (-not $MakeNSIS) {
    $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($command) { $MakeNSIS = $command.Source }
    else { $MakeNSIS = Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe' }
}

if (-not (Test-Path -LiteralPath $MakeNSIS)) {
    throw 'Install NSIS 3, or supply -MakeNSIS with the path to makensis.exe.' 
}

$installer = Join-Path $output "Tower-Windows-x64-Setup.exe"
$fileVersion = ($numbers -join '.') + '.0'
& $MakeNSIS /V3 "/DPRODUCT_VERSION=$Version" "/DFILE_VERSION=$fileVersion" "/DPAYLOAD_ROOT=$publish" "/DOUTPUT_FILE=$installer" (Join-Path $PSScriptRoot 'tower_setup.nsi')

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer)) { throw 'NSIS compilation failed.' }

$portable = Join-Path $output "Tower-Windows-x64-Portable.zip"

if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem

[IO.Compression.ZipFile]::CreateFromDirectory($publish, $portable, [IO.Compression.CompressionLevel]::Optimal, $false)
$hashes = @($installer, $portable) | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_) }

[IO.File]::WriteAllLines((Join-Path $output "Tower-$Version-SHA256SUMS.txt"), $hashes, (New-Object Text.UTF8Encoding($false)))

Write-Output "Installer: $installer"
Write-Output "Portable: $portable"
