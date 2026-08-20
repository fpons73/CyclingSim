# Copia data/pcrm.sqlite y data/stages/*.json dentro de game/data para su uso desde res://
# Uso:  pwsh tools/copy_game_data.ps1
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root "game\data"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root "data\pcrm.sqlite") -Destination (Join-Path $dest "pcrm.sqlite") -Force
$stageDir = Join-Path $root "data\stages"
if (Test-Path $stageDir) {
    Copy-Item -Path (Join-Path $stageDir "*.json") -Destination $dest -Force
}
Write-Host "OK  datos copiados a game\data ($((Get-ChildItem $dest).Count) ficheros)"