# Headless pacing simulator: plays whole games with scripted bots through the real game code and
# writes a report. Runs on a copy of the project (in %LOCALAPPDATA%\FishBreederPacing) with its own
# product name, so the Unity editor can stay open and the real saves are never touched.
#
#   powershell -File Tools/PacingSim/run.ps1 -Days 120 -Seeds 10 -Bots Cautious,Greedy,Breeder,Careless
param(
    [int]$Days = 120,
    [int]$Seeds = 10,
    [string]$Bots = "Cautious,Greedy,Breeder,Careless",
    [int]$FirstSeed = 1,
    [string]$Out = "",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.2.12f1\Editor\Unity.exe"
)
$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$work = Join-Path $env:LOCALAPPDATA "FishBreederPacing"
$copy = Join-Path $work "project"
if (-not $Out) { $Out = Join-Path $work ("run-" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
New-Item -ItemType Directory -Force $copy, $Out | Out-Null

# Mirror the project (Library only the first time: Unity keeps its own up to date after that).
foreach ($dir in "Assets", "Packages", "ProjectSettings") {
    robocopy (Join-Path $project $dir) (Join-Path $copy $dir) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}
if (-not (Test-Path (Join-Path $copy "Library"))) {
    robocopy (Join-Path $project "Library") (Join-Path $copy "Library") /MIR /NFL /NDL /NJH /NJS /NP /XD ScriptAssemblies Bee | Out-Null
}
# Its own product name: its own persistent data folder, PlayerPrefs (dev mode off) and saves.
$settings = Join-Path $copy "ProjectSettings\ProjectSettings.asset"
(Get-Content $settings) -replace '^(\s*productName:).*$', '$1 fish_breeder_pacing' | Set-Content $settings -Encoding utf8

$log = Join-Path $Out "unity.log"
Write-Host "Simulating $Bots x $Seeds seeds x $Days days. Log: $log"
$started = Get-Date
& $Unity -batchmode -nographics -projectPath $copy -logFile $log `
    -pacingSim $Out -pacingBots $Bots -pacingSeeds $Seeds -pacingDays $Days -pacingFirstSeed $FirstSeed | Out-Null
$code = $LASTEXITCODE
Write-Host ("Unity finished (exit {0}) in {1:0}s" -f $code, ((Get-Date) - $started).TotalSeconds)
Select-String -Path $log -Pattern "Pacing sim:|error CS|Exception" | ForEach-Object { $_.Line } | Select-Object -First 80

$runs = Join-Path $Out "runs.jsonl"
if (Test-Path $runs) {
    python (Join-Path $PSScriptRoot "report.py") $runs (Join-Path $Out "report.html") (Join-Path $PSScriptRoot "targets.json")
    Write-Host "Report: $(Join-Path $Out 'report.html')"
}
exit $code
