import re

with open(r'd:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Make handlers return Task<bool>
for method in ['HandleIdleAsync', 'HandleSelectingTargetAsync', 'HandleOpeningAsync', 'HandleEngagingAsync', 'HandleKillLoopAsync', 'HandlePostKillAsync', 'HandleLootingAsync', 'SendAttackAsync']:
    text = re.sub(rf'private async Task {method}', f'private async Task<bool> {method}', text)

# Fix void methods
void_methods = [
    'ClearPendingTargetIfNeeded', 'SetPhase', 'Trace', 'ResetCombatState', 
    'FinishPostKill', 'ClearSelectionOriginOverride', 'TraceOnce', 'BeginPostKill', 'Debug'
]

for vm in void_methods:
    pattern = rf'(private void {vm}[^{{]*{{)(.*?)(?=\n\s*(?:private |public |internal |protected |$))'
    def replacer(m):
        return m.group(1) + m.group(2).replace('return false;', 'return;')
    text = re.sub(pattern, replacer, text, flags=re.DOTALL)

# Add return false to the end of Handlers
def append_return_false(match):
    # match.group(1) is the body before the final closing brace
    if 'return ' in match.group(0).split('\n')[-2]:
        return match.group(0)
    return match.group(0)[:-1] + "    return false;\n    }"
    
for method in ['HandleIdleAsync', 'HandleSelectingTargetAsync', 'HandleOpeningAsync', 'HandleEngagingAsync', 'HandleKillLoopAsync', 'HandlePostKillAsync', 'HandleLootingAsync', 'SendAttackAsync']:
    pattern = rf'private async Task<bool> {method}[^{{]*{{(?:[^{{}}]*{{[^{{}}]*}})*[^{{}}]*}}'
    # Actually just matching braces is too complex for python re. 
    pass

with open(r'd:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs', 'w', encoding='utf-8') as f:
    f.write(text)
