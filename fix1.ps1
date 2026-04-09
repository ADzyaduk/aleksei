$path = "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs"
$content = Get-Content $path -Raw

# ExecuteAsync
$content = $content -replace "public async Task ExecuteAsync", "public async Task<bool> ExecuteAsync"
$content = $content -replace "(?sm)public async Task<bool> ExecuteAsync.*?if \(_phase \=\= CombatPhase.Recovering\)\s*return;", "public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)
    {
        if (DateTime.UtcNow < world.ActionLockUntilUtc)
            return false;

        var combat = profile.Combat;
        var me = world.Me;

        if (!combat.Enabled)
        {
            ResetCombatState(world, `"combat disabled`");
            return false;
        }

        if (profile.Party.Enabled && profile.Party.Mode != PartyMode.None)
        {
            if (_phase != CombatPhase.Idle || world.CurrentCombatPhase != CombatPhase.Idle || world.Me.TargetId != 0 || world.Me.PendingTargetId != 0)
                ResetCombatState(world, `"party mode active`");
            return false;
        }

        if (me.IsDead)
        {
            SetPhase(world, CombatPhase.Recovering, `"me dead`");
            return false;
        }

        if (me.IsSitting)
        {
            SetPhase(world, CombatPhase.Recovering, `"me sitting`");
            return false;
        }

        if (_phase == CombatPhase.Recovering)
            return false;"

# ExecuteAsync switch cases
$content = $content -replace "await HandleIdleAsync\(world, sender, profile, ct\);\s*return;", "return await HandleIdleAsync(world, sender, profile, ct);"
$content = $content -replace "await HandleSelectingTargetAsync\(world, sender, profile, ct\);\s*return;", "return await HandleSelectingTargetAsync(world, sender, profile, ct);"
$content = $content -replace "await HandleOpeningAsync\(world, sender, profile, ct\);\s*return;", "return await HandleOpeningAsync(world, sender, profile, ct);"
$content = $content -replace "await HandleEngagingAsync\(world, sender, profile, ct\);\s*return;", "return await HandleEngagingAsync(world, sender, profile, ct);"
$content = $content -replace "await HandleKillLoopAsync\(world, sender, profile, ct\);\s*return;", "return await HandleKillLoopAsync(world, sender, profile, ct);"
$content = $content -replace "await HandlePostKillAsync\(world, sender, profile, ct\);\s*return;", "return await HandlePostKillAsync(world, sender, profile, ct);"
$content = $content -replace "await HandleLootingAsync\(world, sender, profile, ct\);\s*return;", "return await HandleLootingAsync(world, sender, profile, ct);"
$content = $content -replace "SetPhase\(world, CombatPhase.Idle, `"fallback`"\);\s*return;", "SetPhase(world, CombatPhase.Idle, `"fallback`"); return false;"

# Handler signatures
$handlers = @("HandleIdleAsync", "HandleSelectingTargetAsync", "HandleOpeningAsync", "HandleEngagingAsync", "HandleKillLoopAsync", "HandlePostKillAsync", "HandleLootingAsync", "SendAttackAsync")
foreach ($handler in $handlers) {
    $content = $content -replace "private async Task $handler", "private async Task<bool> $handler"
}

Set-Content $path $content
