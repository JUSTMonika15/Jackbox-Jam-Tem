$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$rulesType = $types | Where-Object Name -eq 'CameraRelativeMovementRules' | Select-Object -First 1
if ($null -eq $rulesType) { throw 'CameraRelativeMovementRules is missing.' }

$rules = [Activator]::CreateInstance($rulesType)

function Assert-Near([double]$expected, [double]$actual, [string]$behavior) {
    if ([Math]::Abs($expected - $actual) -gt 0.0001) {
        throw "$behavior expected $expected but received $actual."
    }
}

function Resolve([float]$inputX, [float]$inputY,
    [float]$rightX, [float]$rightZ, [float]$forwardX, [float]$forwardZ) {
    $rules.Resolve($inputX, $inputY, $rightX, $rightZ, $forwardX, $forwardZ)
}

$forward = Resolve 0 1 0 -1 1 0
Assert-Near 1 $forward.X 'shoulder W follows the camera forward direction'
Assert-Near 0 $forward.Z 'shoulder W has no sideways drift'

$right = Resolve 1 0 0 -1 1 0
Assert-Near 0 $right.X 'shoulder D has no forward drift'
Assert-Near -1 $right.Z 'shoulder D follows the camera right direction'

$overviewForward = Resolve 0 1 1 0 0 0
Assert-Near 0 $overviewForward.X 'overview W has no sideways drift'
Assert-Near 1 $overviewForward.Z 'overview W preserves the board forward axis'

$overviewRight = Resolve 1 0 1 0 0 0
Assert-Near 1 $overviewRight.X 'overview D preserves the board right axis'
Assert-Near 0 $overviewRight.Z 'overview D has no forward drift'

$diagonal = Resolve 1 1 1 0 0 1
Assert-Near 1 ([Math]::Sqrt($diagonal.X * $diagonal.X + $diagonal.Z * $diagonal.Z)) 'diagonal input remains normalized'

Write-Output 'CameraRelativeMovementRules tests passed.'
