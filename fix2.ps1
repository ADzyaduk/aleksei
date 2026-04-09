$path = "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs"
$content = Get-Content $path -Raw

# HandleIdleAsync returns true if it sends command, false otherwise
$content = $content -replace "(?sm)(private async Task<bool> HandleIdleAsync.*?)return;(.*?)(return;)(.*?)\}(?=\s*private async Task<bool> HandleSelectingTargetAsync)", "`$1return false;`$2return false;`$4    world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);`n        return true;`n    }"

# HandleSelectingTargetAsync
$content = $content -replace "(?sm)(private async Task<bool> HandleSelectingTargetAsync.*?)return;(.*?)_targetSelectionUtc = DateTime.UtcNow;(.*?)return;(.*?)return;(.*?)SetPhase\(world, CombatPhase.Idle, `"target clear fallback`"\);\s*\}", "`$1return false;`$2_targetSelectionUtc = DateTime.UtcNow;`n                world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);`n                return true;`$3return false;`$4return false;`$5SetPhase(world, CombatPhase.Idle, `"target clear fallback`");`n        return false;`n    }"

# HandleOpeningAsync
$content = $content -replace "(?sm)(private async Task<bool> HandleOpeningAsync.*?)_targetEngageUtc = DateTime.UtcNow;\s*\}(.*?)SetPhase\(world, CombatPhase.Engaging, `"opening -> engaging`"\);\s*\}", "`$1_targetEngageUtc = DateTime.UtcNow;`n        return false;`n    }`$2SetPhase(world, CombatPhase.Engaging, `"opening -> engaging`");`n        return false;`n    }"

# HandleEngagingAsync
$content = $content -replace "(?sm)(private async Task<bool> HandleEngagingAsync.*?)ClearPendingTargetIfNeeded\(world, profile.Combat\);\s*\}", "`$1ClearPendingTargetIfNeeded(world, profile.Combat);`n        return false;`n    }"

# HandleKillLoopAsync
$content = $content -replace "(?sm)(private async Task<bool> HandleKillLoopAsync.*?)ClearPendingTargetIfNeeded\(world, profile.Combat\);\s*\}", "`$1ClearPendingTargetIfNeeded(world, profile.Combat);`n        return false;`n    }"

# HandlePostKillAsync
$content = $content -replace "(?sm)(private async Task<bool> HandlePostKillAsync.*?)return;(.*?)FinishPostKill\(world, sender, ct\);\s*\}", "`$1return false;`$2FinishPostKill(world, sender, ct);`n        return false;`n    }"

# HandleLootingAsync
$content = $content -replace "(?sm)(private async Task<bool> HandleLootingAsync.*?)return;(.*?)return;(.*?)Debug\(`"all items looted or exhausted`"\);\s*\}", "`$1return false;`$2return false;`$3Debug(`"all items looted or exhausted`");`n        return false;`n    }"

# SendAttackAsync
$content = $content -replace "(?sm)(private async Task<bool> SendAttackAsync.*?)return;(.*?)Debug\(`"`$attack command close enough dist=\{dist:F0\}`"\);\s*\}(.*?)var attackPacket = profile.Combat.UseTargetEnter.*?\}(.*?)var actionPacket = profile.Combat.UseTargetEnter.*?\}", "`$1return false;`$2Debug(`"`$attack command close enough dist={dist:F0}`");`n        return true;`n    }`$3world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(150);`n        var attackPacket = profile.Combat.UseTargetEnter`$4world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(150);`n        var actionPacket = profile.Combat.UseTargetEnter`n    }"
$content = $content -replace "(?sm)(private async Task<bool> SendAttackAsync.*?)Debug\(`"`$attack sent target=\{target\.ObjectId\} dist=\{dist:F0\} dx=\{dx:F0\} dy=\{dy:F0\}`"\);\s*\}", "`$1Debug(`"`$attack sent target={target.ObjectId} dist={dist:F0} dx={dx:F0} dy={dy:F0}`");`n        return true;`n    }"

# Other tasks that return Task<bool> internally (TryCastSkill, TrySweep, TrySpoil)
$content = $content -replace "var packet = GamePackets.UseSkill\(skillId, profile.Combat.CombatSkillPacket\);", "world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);`n            var packet = GamePackets.UseSkill(skillId, profile.Combat.CombatSkillPacket);"

Set-Content $path $content
