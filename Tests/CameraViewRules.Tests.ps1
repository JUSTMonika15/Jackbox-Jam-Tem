$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$rulesType = $types | Where-Object Name -eq 'CameraViewRules' | Select-Object -First 1
if ($null -eq $rulesType) { throw 'CameraViewRules is missing.' }

$rules = [Activator]::CreateInstance($rulesType)
$initial = $rules.Initial()
if ($initial.ToString() -ne 'Overview') {
    throw "a new gameplay scene must begin in Overview but received $initial."
}
$shoulder = $rules.Toggle($initial)
if ($shoulder.ToString() -ne 'Shoulder') { throw 'camera must still toggle to Shoulder during play.' }
$overview = $rules.Toggle($shoulder)
if ($overview.ToString() -ne 'Overview') { throw 'camera must still toggle back to Overview during play.' }

Write-Output 'CameraViewRules tests passed.'
