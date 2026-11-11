param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'
if ($Tag -cnotmatch '^v?(?<version>[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?)$') {
    throw 'Use a version tag such as 1.0.0, v1.0.0 or v1.0.0-rc.1.'
}
$Matches['version']
