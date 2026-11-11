# Windows integration check

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet run --project Tests/WindowsSmoke/WindowsSmoke.csproj -c Release
```

For a focused check of default layout creation, cancelled/confirmed close,
automatic saving and restoration in a new scenario, use:

```powershell
dotnet run --project Tests/WindowsSmoke/WindowsSmoke.csproj -- --layouts-only
```

Layout checks use isolated output folders and do not write the user's INI or SET files.

Use `--free-traffic-only` for a focused check of generated free traffic and arrival
entry labels in radar and flightstrips on Canyon (CYN). It uses original airport
assets and isolated layouts, without recording controller results or changing
speech preferences. Core tests cover generator timing, selection and save/load.

Use `--performance-only` for the focused Performance Review/reward check: it
checks the nine command groups, airport difficulty, rating, practice status,
reward text and icon with sample data. It writes screenshots, not profile data.

This executable is internal verification tooling. It is not referenced by Tower
and is never included in the published game. It creates transparent test windows,
renders screenshots, clicks existing controls and answers only its own thread's
simulation-close confirmation dialogs. A failure returns a nonzero exit code.

The checks cover all six 3D view modes, exclusive menu checkmarks, required
aircraft selection, retargeting, manual-pan and missing-target fallback, and
confirmed aircraft deletion. They also cover separate arrival/departure strips, radar/strip right-click
addressing, command input resizing, TowerView pan/zoom, completed-arrival removal,
retained logs and both No/Yes close outcomes. With the original repository `Data`
present, they also load all aircraft models/scenery, run real arrival/departure
radio messages through both speech engines, and export English Windows speech to
WAV. Without original assets, only the training UI and installed Windows speech
can be checked; the report says so explicitly. Physical audio playback and every
possible command, reminder and rejected clearance are not covered by this smoke
check. Missing BAO phrases are reported without approving recordings or falling
back to Windows speech.

Reports and screenshots go to `obj/WindowsSmoke`. Set `TOWER_SMOKE_OUTPUT` to
change only the output directory. Game data resolution remains the same as Tower:
repository-root `Data` in development, adjacent `Data` in a standalone installation.
The check does not save user settings, layouts, speech metadata or reviews.

A complete local portable package is created with:

```powershell
dotnet publish src/Tower.WinForms -c Release -r win-x64 --self-contained true -o obj/Release
```

Publishing includes game Data, `settings.ini` and the four original layout presets
when present. It excludes speech backups, original graphics DLLs, custom layouts
and internal tools. Speech backups live in `Backups/Speech` beside Data and are
excluded from Git. Game assets belong in version control alongside code. CI
requires complete game Data, verifies the published files against their sources,
and produces a package containing the game and Data. Missing essential assets
stop publication. `-p:TowerRequireGameData=false` remains available for an explicit
local application-only build; CI does not use this override.

To check a standalone package, copy this check's four `WindowsSmoke` executable,
DLL, deps and runtimeconfig files alongside the published game in a temporary
directory outside the repository. For a self-contained package, copy
`Tower.runtimeconfig.json` and `Tower.deps.json` over the check's corresponding
`WindowsSmoke` hosting manifests so it uses the published runtime and dependencies.
Run it there: it must report that directory's
adjacent Data path. Keep verification files out of the release distributed to users.

CI and release verification use isolated Canyon-only data. Package lifecycle and
manager checks generate synthetic add-ons from Canyon; no installed DCA, ORD or
EIN airport, private release or external package download is required.
