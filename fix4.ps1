$lines = Get-Content "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs"
$out = @()
foreach ($line in $lines) {
    if ($line -match "^\s*return false;\s*$") {
        if ($out[-1] -match "return await Handle[A-Za-z]+Async") {
            continue # Skip unreachable return false
        }
    }
    $out += $line
}
Set-Content "d:\pj\aleksei\src\Alexei.Core\Engine\Tasks\AutoCombatTask.cs" $out
