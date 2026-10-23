# Runs Sync World (docs/workflow.md) in batch mode, for when the Unity Editor is closed.
# With the Editor open, use R'th > World > Sync World there instead (the project can only be open once).
#
#   powershell -File tools/unity-sync.ps1                       # regions that changed since the last sync, with screenshots
#   powershell -File tools/unity-sync.ps1 -Regions tolling      # some regions, changed or not
#   powershell -File tools/unity-sync.ps1 -All                  # every region
#   powershell -File tools/unity-sync.ps1 -NoScreens            # quicker, no screenshots
param(
    [string[]]$Regions = @(),
    [switch]$NoScreens,
    [switch]$All,
    [string]$Unity = ""
)

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "client"
if ($Unity -eq "") {
    $version = (Get-Content (Join-Path $project "ProjectSettings/ProjectVersion.txt") | Select-String "m_EditorVersion:").ToString().Split(":")[1].Trim()
    $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $Unity)) { Write-Error "Unity not found at $Unity; pass -Unity <path to Unity.exe>."; exit 2 }

Write-Host "Building the shared DLLs first (dotnet build)..."
dotnet build (Join-Path $root "Rth.slnx") --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$log = Join-Path $project "Temp/rth-sync.log"
$arguments = @("-batchmode", "-projectPath", $project, "-executeMethod", "Rth.Client.Editor.WorldSync.SyncFromCommandLine", "-logFile", $log)
if ($Regions.Count -gt 0) { $arguments += @("-rthRegions", ($Regions -join ",")) }
if ($NoScreens) { $arguments += "-rthNoScreens" }
if ($All) { $arguments += "-rthAll" }

Write-Host "Running Sync World in batch mode (log: $log)..."
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
$report = Join-Path $project "Temp/rth-sync-report.txt"
if (Test-Path $report) { Get-Content $report } else { Get-Content $log -Tail 40 }
exit $process.ExitCode
