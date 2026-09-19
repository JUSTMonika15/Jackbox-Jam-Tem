$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Script/GameRules.cs') -Raw
$types = Add-Type -TypeDefinition $source -PassThru
$cardRulesType = $types | Where-Object Name -eq 'HudGameplayCardRules' | Select-Object -First 1
$presentationType = $types | Where-Object Name -eq 'BoardPresentationRules' | Select-Object -First 1
if ($null -eq $cardRulesType) { throw 'HudGameplayCardRules is missing.' }
if ($null -eq $presentationType) { throw 'BoardPresentationRules is missing.' }

$cardRules = [Activator]::CreateInstance($cardRulesType)
$presentation = [Activator]::CreateInstance($presentationType)
function Assert-Equal($expected, $actual, [string]$behavior) {
    if ($expected -ne $actual) { throw "$behavior expected $expected but received $actual." }
}
function Assert-Near([double]$expected, [double]$actual, [string]$behavior) {
    if ([Math]::Abs($expected - $actual) -gt 0.0001) {
        throw "$behavior expected $expected but received $actual."
    }
}

Assert-Equal 'Property' ($cardRules.Choose($true, $true).ToString()) 'property information replaces stale event feedback'
Assert-Equal 'Event' ($cardRules.Choose($false, $true).ToString()) 'event feedback appears away from properties'
Assert-Equal 'None' ($cardRules.Choose($false, $false).ToString()) 'empty gameplay state draws no card'

Assert-Near 90 ($presentation.EventCardTop(720, 120, $true)) 'shoulder event card stays near the top of the screen'
Assert-Near 300 ($presentation.EventCardTop(720, 120, $false)) 'overview event card remains centered'
Assert-Near 90 ($presentation.EventCardTop(720, 150, $true)) 'major shoulder event card stays near the top of the screen'

Write-Output 'HudGameplayRules tests passed.'
