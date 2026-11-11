param([string]$Version = '1.0.0', [string]$MakeNSIS)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath("$PSScriptRoot/..")
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid installer version.' }
if (-not $MakeNSIS) { $MakeNSIS = Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe' }
$report = Join-Path $repository 'obj/SetupSmoke'
New-Item -ItemType Directory -Force -Path $report | Out-Null
$installer = Join-Path $report 'Tower-InstallerSmoke.exe'
$payload = Join-Path $PSScriptRoot 'publish'
$fileVersion = $Version.Split('-')[0] + '.0'
& $MakeNSIS /V2 /DINSTALLER_TEST "/DPRODUCT_VERSION=$Version" "/DFILE_VERSION=$fileVersion" "/DPAYLOAD_ROOT=$payload" "/DOUTPUT_FILE=$installer" (Join-Path $PSScriptRoot 'tower_setup.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer smoke compilation failed.' }
$key = 'HKCU:\Software\Sibra-Soft\Tower-InstallerSmoke'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SibraSoft.Tower.InstallerSmoke'
if ((Test-Path $key) -or (Test-Path $uninstallKey)) { throw 'A previous installer smoke is still registered. Refusing to touch an existing installation.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('Tower-InstallerSmoke-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $testRoot 'Tower Installation'
[IO.File]::WriteAllText((Join-Path $report 'installation-path.txt'), $install)
function Run-Setup {
    $process = Start-Process -FilePath $installer -ArgumentList "/S /D=$install" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Installer timed out.' }
    if ($process.ExitCode -ne 0) { throw "Installer exited with $($process.ExitCode)." }
}
function Check-Payload {
    foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
        $relative = $file.FullName.Substring($payload.Length + 1)
        if ($relative -eq 'settings.ini') { continue }
        $target = Join-Path $install $relative
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Installed payload differs: $relative" }
    }
    if ((Get-ItemProperty $uninstallKey).DisplayVersion -ne $Version) { throw 'Installed version registration differs.' }
    if ((Get-ItemProperty $key).InstallDir -ne $install) { throw 'Installation directory registration differs.' }
}
Run-Setup
Check-Payload
$settings = Join-Path $install 'settings.ini'
$personal = '; installer smoke: preserve my preferences' + "`r`n" + [IO.File]::ReadAllText($settings)
[IO.File]::WriteAllText($settings, $personal)
[IO.File]::WriteAllText((Join-Path $install 'personal.set'), 'User layout must survive.')
[IO.File]::WriteAllText((Join-Path $install 'profiles.json'), '[]')
$addon = Join-Path $install 'Data/TST/user-airport.keep'
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($addon)) | Out-Null
[IO.File]::WriteAllText($addon, 'User airport must survive.')
Run-Setup
Check-Payload
if ([IO.File]::ReadAllText($settings) -ne $personal -or -not (Test-Path -LiteralPath $addon)) { throw 'Upgrade changed preferences or add-ons.' }
$process = Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList '/S' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Uninstaller timed out.' }
for ($attempt = 0; $attempt -lt 150; $attempt++) {
    if (-not (Test-Path -LiteralPath (Join-Path $install 'Tower.exe')) -and -not (Test-Path $key) -and -not (Test-Path $uninstallKey)) { break }
    Start-Sleep -Milliseconds 200
}
if ((Test-Path -LiteralPath (Join-Path $install 'Tower.exe')) -or (Test-Path $key) -or (Test-Path $uninstallKey)) { throw 'Uninstall left application files or registration.' }
if ([IO.File]::ReadAllText($settings) -ne $personal) { throw 'Uninstall changed preferences.' }
foreach ($relative in @('personal.set', 'profiles.json', 'Data/TST/user-airport.keep')) {
    if (-not (Test-Path -LiteralPath (Join-Path $install $relative))) { throw "Uninstall removed user data: $relative" }
}
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object Name -ne 'settings.ini') {
    if (Test-Path -LiteralPath (Join-Path $install $file.FullName.Substring($payload.Length + 1))) { throw 'Uninstall left an owned payload file.' }
}
[IO.File]::WriteAllText((Join-Path $report 'result.txt'), 'PASS: install, byte-identical payload, version, upgrade, settings/layout/profile/add-on preservation and uninstall.')
Write-Output 'PASS: installer, upgrade and uninstall; user data preserved.'
