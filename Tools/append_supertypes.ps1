# append_supertypes.ps1
# Resumes fetching from a given page and appends to existing supertypes.json
# Usage: .\Tools\append_supertypes.ps1 -StartPage 58 -ApiKey "your-key"
# Run from project root: cd C:\Users\adri1\Documents\Cardwatcher

param(
    [int]$StartPage = 58,
    [string]$ApiKey = ""
)

$pageSize = 250
$page     = $StartPage
$fetched  = 0
$total    = [int]::MaxValue
$entries  = [System.Collections.Generic.List[string]]::new()

$headers = @{ "Content-Type" = "application/json" }
if ($ApiKey) { $headers["X-Api-Key"] = $ApiKey }

function Fetch-Page($p) {
    $url = "https://api.pokemontcg.io/v2/cards?page=$p&pageSize=$pageSize"
    $retries = 8
    $delay   = 3
    for ($i = 0; $i -lt $retries; $i++) {
        try {
            return Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 90
        } catch {
            if ($i -lt ($retries - 1)) {
                Write-Host "    Retry $($i+1) after ${delay}s... ($_)"
                Start-Sleep -Seconds $delay
                $delay = [Math]::Min($delay * 2, 60)
            } else {
                throw
            }
        }
    }
}

Write-Host "Fetching pages from $StartPage..."

while ($page * $pageSize - $pageSize -lt $total) {
    try {
        $resp = Fetch-Page $page
    } catch {
        Write-Host "Failed page $page after retries: $_"
        $page++
        Start-Sleep -Seconds 5
        continue
    }

    if ($total -eq [int]::MaxValue) { $total = $resp.totalCount }

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

    Write-Host "  Page $page - fetched $fetched new entries (total=$total)"
    $page++
    Start-Sleep -Milliseconds 800
}

$outPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\SeedData\supertypes.json"))

# Read existing JSON, strip closing brace, append new entries
$existing = [System.IO.File]::ReadAllText($outPath, [System.Text.Encoding]::UTF8)
$existing = $existing.TrimEnd().TrimEnd('}')

if ($entries.Count -gt 0) {
    $appended = $existing + ',' + ($entries -join ',') + '}'
    [System.IO.File]::WriteAllText($outPath, $appended, [System.Text.Encoding]::UTF8)
    Write-Host "Done. Appended $fetched entries -> $outPath"
} else {
    Write-Host "No new entries fetched."
}
