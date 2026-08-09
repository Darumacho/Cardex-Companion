# generate_supertypes.ps1
# Usage: .\Tools\generate_supertypes.ps1 -ApiKey "your-key"
# Run from project root: cd C:\Users\adri1\Documents\Cardwatcher

param([string]$ApiKey = "")

$pageSize = 250
$page     = 1
$fetched  = 0
$total    = 1
$entries  = [System.Collections.Generic.List[string]]::new()

$headers = @{ "Content-Type" = "application/json" }
if ($ApiKey) { $headers["X-Api-Key"] = $ApiKey }

function Fetch-Page($p) {
    $url = "https://api.pokemontcg.io/v2/cards?page=$p&pageSize=$pageSize"
    $retries = 5
    $delay   = 2
    for ($i = 0; $i -lt $retries; $i++) {
        try {
            return Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 60
        } catch {
            if ($i -lt ($retries - 1)) {
                Write-Host "    Retry $($i+1) after ${delay}s... ($_)"
                Start-Sleep -Seconds $delay
                $delay *= 2
            } else {
                throw
            }
        }
    }
}

Write-Host "Fetching supertypes from pokemontcg.io..."

while ($fetched -lt $total) {
    try {
        $resp = Fetch-Page $page
    } catch {
        Write-Host "Failed page $page after retries: $_"
        break
    }

    if ($page -eq 1) { $total = $resp.totalCount }

    foreach ($card in $resp.data) {
        $id = $card.id
        $st = $card.supertype
        if ($st -eq "Trainer") {
            $code = "T"
        } elseif ($st -eq "Energy") {
            $code = "E"
        } else {
            $code = "P"
        }
        $entries.Add('"' + $id + '":"' + $code + '"')
        $fetched++
    }

    Write-Host "  Page $page - $fetched / $total"
    $page++
    Start-Sleep -Milliseconds 500
}

$json = "{" + ($entries -join ",") + "}"

$outPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\SeedData\supertypes.json"))
[System.IO.File]::WriteAllText($outPath, $json, [System.Text.Encoding]::UTF8)

Write-Host "Done. $fetched cards -> $outPath"
