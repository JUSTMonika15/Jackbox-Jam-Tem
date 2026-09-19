$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$rulesType = $types | Where-Object Name -eq 'PlayerFeedbackRules' | Select-Object -First 1
$kindType = $types | Where-Object Name -eq 'PlayerFeedbackKind' | Select-Object -First 1
if ($null -eq $rulesType -or $null -eq $kindType) { throw 'Player feedback rules are missing.' }

$rules = [Activator]::CreateInstance($rulesType)
function Kind([string]$name) { [Enum]::Parse($kindType, $name) }
function Assert-Equal($expected, $actual, [string]$behavior) {
    if ($expected -ne $actual) { throw "$behavior expected $expected but received $actual." }
}

Assert-Equal 2.25 ($rules.Duration((Kind 'Jail'))) 'jail remains noticeable without blocking the next tile'
Assert-Equal 2.25 ($rules.Duration((Kind 'Teleport'))) 'return-to-start remains noticeable without blocking the next tile'
Assert-Equal 1.5 ($rules.Duration((Kind 'Positive'))) 'ordinary rewards clear quickly'
Assert-Equal $true ($rules.IsMajor((Kind 'Jail'))) 'jail uses major screen feedback'
Assert-Equal $false ($rules.IsMajor((Kind 'SpeedUp'))) 'speed feedback does not obscure play'
Assert-Equal 'Jail' ($rules.SoundFor((Kind 'Jail')).ToString()) 'jail uses the jail sound'
Assert-Equal 'Reward' ($rules.SoundFor((Kind 'Pass')).ToString()) 'passes use the reward sound'
Assert-Equal 'Penalty' ($rules.SoundFor((Kind 'Negative')).ToString()) 'money loss uses the penalty sound'

Write-Output 'PlayerFeedbackRules tests passed.'
