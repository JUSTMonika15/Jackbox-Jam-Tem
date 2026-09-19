$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$rulesType = $types | Where-Object Name -eq 'MovementBonusRules' | Select-Object -First 1
if ($null -eq $rulesType) { throw 'MovementBonusRules is missing.' }

$rules = [Activator]::CreateInstance($rulesType)
$player = [object]::new()

function Assert-Equal([int]$expected, [int]$actual, [string]$behavior) {
    if ($expected -ne $actual) {
        throw "$behavior expected $expected but received $actual."
    }
}

Assert-Equal 10 ($rules.TryClaim($player, 0)) 'first claim on lap zero'
Assert-Equal 0 ($rules.TryClaim($player, 0)) 'repeat claim on the same lap'
Assert-Equal 15 ($rules.TryClaim($player, 1)) 'claim after completing one lap'
Assert-Equal 60 ($rules.TryClaim($player, 10)) 'reward continues growing without a gameplay cap'
$rules.Reset()
Assert-Equal 10 ($rules.TryClaim($player, 0)) 'new match reset restores lap-zero eligibility'

Write-Output 'MovementBonusRules tests passed.'
