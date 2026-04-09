$lines = Get-Content "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs"
$out = @()
$inTaskBool = $false
foreach ($line in $lines) {
    if ($line -match "public async Task ExecuteAsync") {
        $line = $line -replace "Task", "Task<bool>"
        $inTaskBool = $true
    } elseif ($line -match "private async Task Handle" -or $line -match "private async Task SendAttackAsync") {
        $line = $line -replace "Task", "Task<bool>"
        $inTaskBool = $true
    } elseif ($line -match "private void " -or $line -match "public void " -or $line -match "private string ") {
        $inTaskBool = $false
    } elseif ($line -match "private async Task<bool> ") {
        $inTaskBool = $true
    }
    
    if ($inTaskBool) {
        if ($line -match "^\s*return;\s*$") {
            $line = $line -replace "return;", "return false;"
        }
        if ($line -match "^\s*await Handle[A-Za-z]+Async.*") {
            $line = $line -replace "await Handle", "return await Handle"
        }
    }
    $out += $line
}
Set-Content "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs" $out
