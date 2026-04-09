import re

with open(r'd:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Change ExecuteAsync
text = text.replace(
    'public async Task ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)',
    'public async Task<bool> ExecuteAsync(GameWorld world, PacketSender sender, CharacterProfile profile, CancellationToken ct)'
)

text = re.sub(r'if \(DateTime.UtcNow < world.ActionLockUntilUtc\)\s*return;', 'if (DateTime.UtcNow < world.ActionLockUntilUtc)\n            return false;', text)

text = text.replace('ResetCombatState(world, "combat disabled");\n            return;', 'ResetCombatState(world, "combat disabled");\n            return false;')
text = text.replace('ResetCombatState(world, "party mode active");\n            return;', 'ResetCombatState(world, "party mode active");\n            return false;')
text = text.replace('SetPhase(world, CombatPhase.Recovering, "me dead");\n            return;', 'SetPhase(world, CombatPhase.Recovering, "me dead");\n            return false;')
text = text.replace('SetPhase(world, CombatPhase.Recovering, "me sitting");\n            return;', 'SetPhase(world, CombatPhase.Recovering, "me sitting");\n            return false;')
text = text.replace('if (_phase == CombatPhase.Recovering)\n            return;', 'if (_phase == CombatPhase.Recovering)\n            return false;')

# Change the switch cases
cases_to_replace = [
    ('await HandleIdleAsync(world, sender, profile, ct);\n                return;', 'return await HandleIdleAsync(world, sender, profile, ct);'),
    ('await HandleSelectingTargetAsync(world, sender, profile, ct);\n                return;', 'return await HandleSelectingTargetAsync(world, sender, profile, ct);'),
    ('await HandleOpeningAsync(world, sender, profile, ct);\n                return;', 'return await HandleOpeningAsync(world, sender, profile, ct);'),
    ('await HandleEngagingAsync(world, sender, profile, ct);\n                return;', 'return await HandleEngagingAsync(world, sender, profile, ct);'),
    ('await HandleKillLoopAsync(world, sender, profile, ct);\n                return;', 'return await HandleKillLoopAsync(world, sender, profile, ct);'),
    ('await HandlePostKillAsync(world, sender, profile, ct);\n                return;', 'return await HandlePostKillAsync(world, sender, profile, ct);'),
    ('await HandleLootingAsync(world, sender, profile, ct);\n                return;', 'return await HandleLootingAsync(world, sender, profile, ct);'),
    ('SetPhase(world, CombatPhase.Idle, "fallback");\n                return;', 'SetPhase(world, CombatPhase.Idle, "fallback");\n                return false;')
]
for old, new in cases_to_replace:
    text = text.replace(old, new)

# 2. Change signatures of Handlers and SendAttackAsync
for method in ['HandleIdleAsync', 'HandleSelectingTargetAsync', 'HandleOpeningAsync', 'HandleEngagingAsync', 'HandleKillLoopAsync', 'HandlePostKillAsync', 'HandleLootingAsync', 'SendAttackAsync']:
    text = text.replace(f'private async Task {method}', f'private async Task<bool> {method}')

# 3. Handle `return;` inside these methods
# HandleIdleAsync
text = text.replace('return;\n        }\n\n        var target', 'return false;\n        }\n\n        var target')
text = text.replace('return;\n        }\n\n        await sender', 'return false;\n        }\n\n        await sender')
text = text.replace('SetPhase(world, CombatPhase.SelectingTarget, $"select target={target.ObjectId}");\n    }', 'world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);\n        SetPhase(world, CombatPhase.SelectingTarget, $"select target={target.ObjectId}");\n        return true;\n    }')

# HandleSelectingTargetAsync
text = text.replace('return;\n        }\n\n        if (_targetSelectionUtc != DateTime.MinValue)', 'return false;\n        }\n\n        if (_targetSelectionUtc != DateTime.MinValue)')
text = text.replace('await sender.SendAsync(BuildTargetPacket(target, profile.Combat, world), ct);\n                _targetSelectionAttempts++;\n                _targetSelectionUtc = DateTime.UtcNow;', 'await sender.SendAsync(BuildTargetPacket(target, profile.Combat, world), ct);\n                _targetSelectionAttempts++;\n                _targetSelectionUtc = DateTime.UtcNow;\n                world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(200);\n                return true;')
text = text.replace('return;\n            }\n\n            if (_targetSelectionAttempts > 0)', 'return false;\n            }\n\n            if (_targetSelectionAttempts > 0)')
text = text.replace('return;\n            }\n\n            ClearSelectionOriginOverride', 'return false;\n            }\n\n            ClearSelectionOriginOverride')
text = text.replace('SetPhase(world, CombatPhase.Idle, "target clear fallback");\n        }', 'SetPhase(world, CombatPhase.Idle, "target clear fallback");\n        return false;\n    }')

# HandleOpeningAsync
text = text.replace('_targetEngageUtc = DateTime.UtcNow;\n        }', '_targetEngageUtc = DateTime.UtcNow;\n        return false;\n    }')
text = text.replace('Debug("opening skills on cooldown or failed");\n        SetPhase(world, CombatPhase.Engaging, "opening -> engaging");\n    }', 'Debug("opening skills on cooldown or failed");\n        SetPhase(world, CombatPhase.Engaging, "opening -> engaging");\n        return false;\n    }')

# HandleEngagingAsync
text = text.replace('ClearPendingTargetIfNeeded(world, profile.Combat);\n    }', 'ClearPendingTargetIfNeeded(world, profile.Combat);\n        return false;\n    }')

# HandleKillLoopAsync
text = text.replace('ClearPendingTargetIfNeeded(world, profile.Combat);\n    }', 'ClearPendingTargetIfNeeded(world, profile.Combat);\n        return false;\n    }')

# HandlePostKillAsync
text = text.replace('return;\n            }\n        }\n\n        FinishPostKill(world, sender, ct);', 'return false;\n            }\n        }\n\n        FinishPostKill(world, sender, ct);\n        return false;')

# HandleLootingAsync
text = text.replace('return;\n        }\n\n        bool hasUnpicked', 'return false;\n        }\n\n        bool hasUnpicked')
text = text.replace('return;\n        }\n\n        Debug("all items looted or exhausted");', 'return false;\n        }\n\n        Debug("all items looted or exhausted");\n        return false;')

# SendAttackAsync
text = text.replace('return;\n        }\n\n        double dist', 'return false;\n        }\n\n        double dist')
text = text.replace('Debug($"attack command close enough dist={dist:F0}");\n        }', 'Debug($"attack command close enough dist={dist:F0}");\n        return true;\n    }')
text = text.replace('var attackPacket = profile.Combat.UseTargetEnter', 'world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(150);\n        var attackPacket = profile.Combat.UseTargetEnter')
text = text.replace('var actionPacket = profile.Combat.UseTargetEnter', 'world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(150);\n        var actionPacket = profile.Combat.UseTargetEnter')


# Also update the returns in TryCastSkillAsync, TrySweepCorpseAsync, TrySpoilAsync which return Task<bool>
text = text.replace('var packet = GamePackets.UseSkill(skillId, profile.Combat.CombatSkillPacket);', 'world.ActionLockUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);\n            var packet = GamePackets.UseSkill(skillId, profile.Combat.CombatSkillPacket);')


with open(r'd:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs', 'w', encoding='utf-8') as f:
    f.write(text)
