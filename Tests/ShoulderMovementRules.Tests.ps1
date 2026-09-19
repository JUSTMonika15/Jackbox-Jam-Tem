$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$rulesType = $types | Where-Object Name -eq 'ShoulderMovementRules' | Select-Object -First 1
if ($null -eq $rulesType) { throw 'ShoulderMovementRules is missing.' }

$rules = [Activator]::CreateInstance($rulesType)
function Assert-Near([double]$expected, [double]$actual, [string]$behavior) {
    if ([Math]::Abs($expected - $actual) -gt 0.0001) {
        throw "$behavior expected $expected but received $actual."
    }
}

$turnRight = $rules.Resolve(0, 1, 1, 0, 30)
Assert-Near 0.5 $turnRight.FacingX 'D rotates only partway toward the right during one frame'
Assert-Near 0.8660254 $turnRight.FacingZ 'D preserves a smooth normalized facing direction'
Assert-Near 0 $turnRight.MoveX 'D does not strafe horizontally in shoulder view'
Assert-Near 0 $turnRight.MoveZ 'D does not move forward in shoulder view'

$forwardWhileTurning = $rules.Resolve(0, 1, 1, 1, 30)
Assert-Near 0.5 $forwardWhileTurning.MoveX 'W moves along the newly rotated facing direction'
Assert-Near 0.8660254 $forwardWhileTurning.MoveZ 'W keeps moving forward while turning'

$turnLeft = $rules.Resolve(0, 1, -1, 0, 30)
Assert-Near -0.5 $turnLeft.FacingX 'A rotates left gradually'
Assert-Near 0.8660254 $turnLeft.FacingZ 'A preserves a normalized facing direction'

$reverse = $rules.Resolve(0, 1, 0, -1, 30)
Assert-Near 0 $reverse.MoveX 'S does not change the facing axis'
Assert-Near -1 $reverse.MoveZ 'S moves backward without flipping the character'

Write-Output 'ShoulderMovementRules tests passed.'
