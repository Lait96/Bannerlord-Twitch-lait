# Tests production policy methods with a fake engine; native movement requires gameplay validation.
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTHeroDetachmentBehavior.cs') -Raw
$start = $source.IndexOf('        private void UpdateRangedPriority()')
$end = $source.IndexOf('        private void ApplyOverrides(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Combat policy methods not found' }
$harness = Get-Content (Join-Path $PSScriptRoot 'ranged-combat-harness.cs') -Raw
Add-Type -TypeDefinition $harness.Replace('__POLICY__', $source.Substring($start, $end - $start))
[CombatPolicyTests.Tests]::Run()
