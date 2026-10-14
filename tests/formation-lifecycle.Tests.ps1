$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTHeroDetachmentBehavior.cs') -Raw
function Get-ProductionMethod([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $open = $source.IndexOf('{', $start)
    $arrow = $source.IndexOf('=>', $start)
    if ($arrow -ge 0 -and $arrow -lt $open) {
        return $source.Substring($start, $source.IndexOf(';', $arrow) + 1 - $start)
    }
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced method: $signature" }
    return $source.Substring($start, $end - $start)
}
$behaviorMethods = @(
    'public bool IsDetached(Agent agent)',
    'private void ReleaseStaleDetachment(',
    'private static void DestroyOwnedDetachment(',
    'internal static void RestoreFormationOrders(',
    'public string Detach(Agent agent)',
    'public string Attach(Agent agent)',
    'private static void ClearAgentNavigatingAggressively(',
    'private void CleanupDetachmentOnDeath(',
    'private void CleanupDetachment(Agent agent',
    'private static void ApplyFormationPositionPreference(',
    'public void ClearIgnoredClasses('
    'private void BeginUsingSiegeWeapon('
    'private void ReleaseSiegeReservation('
    'private void ApplyAdvance('
    'public string Focus('
    'public string ValidateFocus('
    'private static Formation FindFocusFormation('
    'public string ClearFocus('
    'public string SetFiringOrder('
    'public string SetRidingOrder('
    'private static void ApplyRidingOrder('
    'public override void OnAgentDeleted('
) | ForEach-Object { Get-ProductionMethod $_ }
$detachmentMethods = @(
    'public void AddAgentAtSlotIndex(',
    'public void RemoveAgent(Agent agent)',
    'public void ForgetAgent(Agent agent)',
    'public void OnFormationLeave(Formation formation)'
) | ForEach-Object { Get-ProductionMethod $_ }
$harness = Get-Content (Join-Path $PSScriptRoot 'formation-lifecycle-harness.cs') -Raw
$source = Get-Content (Join-Path $PSScriptRoot '../BannerlordTwitch/BLTAdoptAHero/Actions/FormationCommand.cs') -Raw
$commandMethods = @(
    'private void ExecuteAdvancedCommand(',
    'private static string FindNearestMount(',
    'private bool TryGetFocusClass(',
    'private static string ClearFocus(',
    'private static string EnsureDetached(',
    'private static string EnsureAttached(',
    'private static string ResetHeroControl('
) | ForEach-Object { (Get-ProductionMethod $_).Replace('BLTHeroDetachmentBehavior', 'Behavior') }
$numericStart = $source.IndexOf('            if (settings.Filter)')
$numericEnd = $source.IndexOf('        private void TransferHeroToFormation(', $numericStart)
$numeric = $source.Substring($numericStart, $numericEnd - $numericStart)
$numeric = $numeric.Substring(0, $numeric.LastIndexOf('        }'))
Add-Type -TypeDefinition $harness.Replace('__BEHAVIOR__', (($behaviorMethods -join "`n").Replace('public override void OnAgentDeleted', 'public void OnAgentDeleted'))).Replace('__DETACHMENT__', ($detachmentMethods -join "`n")).Replace('__COMMANDS__', ($commandMethods -join "`n")).Replace('__NUMERIC__', $numeric)
[FormationLifecycleTests.Tests]::Run()
