# fill_supertypes_by_set.ps1
# Fetches supertypes for all sets not yet covered in supertypes.json
# Queries one set at a time — much more reliable than global pagination
# Usage: .\Tools\fill_supertypes_by_set.ps1 -ApiKey "your-key"
# Run from project root: cd C:\Users\adri1\Documents\Cardwatcher

param([string]$ApiKey = "")

$headers = @{ "Content-Type" = "application/json" }
if ($ApiKey) { $headers["X-Api-Key"] = $ApiKey }

$outPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\SeedData\supertypes.json"))
$setsJsonPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\SeedData\sets.json"))

# Load existing map
$existingJson = [System.IO.File]::ReadAllText($outPath, [System.Text.Encoding]::UTF8)
$existingMap  = [System.Text.Json.JsonSerializer]::Deserialize($existingJson,
    [System.Collections.Generic.Dictionary[string,string]])
Write-Host "Existing entries: $($existingMap.Count)"

# Load set IDs from sets.json
$setsRaw  = [System.IO.File]::ReadAllText($setsJsonPath, [System.Text.Encoding]::UTF8)
$sets     = [System.Text.Json.JsonSerializer]::Deserialize($setsRaw,
    [System.Collections.Generic.List[System.Text.Json.JsonElement]])

$setIds = $sets | ForEach-Object { $_.GetProperty("id").GetString() }
Write-Host "Total sets: $($setIds.Count)"

function Fetch-SetCards($setId) {
    $page    = 1
    $all     = [System.Collections.Generic.List[object]]::new()
    $total   = 1
    while ($all.Count -lt $total) {
        $url     = "https://api.pokemontcg.io/v2/cards?q=set.id:$setId&page=$page&pageSize=250"
        $retries = 6
        $delay   = 3
        $resp    = $null
        for ($i = 0; $i -lt $retries; $i++) {
            try {
                $resp = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 60
                break
            } catch {
                if ($i -lt ($retries - 1)) {
                    Write-Host "      Retry $($i+1) after ${delay}s..."
                    Start-Sleep -Seconds $delay
                    $delay = [Math]::Min($delay * 2, 30)
                }
            }
        }
        if ($null -eq $resp) { Write-Host "  FAILED set $setId page $page"; break }
        $total = $resp.totalCount
        foreach ($c in $resp.data) { $all.Add($c) }
        $page++
        if ($page -gt 1) { Start-Sleep -Milliseconds 500 }
    }
    return $all
}

$newEntries = [System.Collections.Generic.List[string]]::new()
$processed  = 0

foreach ($setId in $setIds) {
    # Check if ANY card from this set is already in the map
    $sampleKey = $existingMap.Keys | Where-Object { $_.StartsWith($setId + "-") } | Select-Object -First 1
    if ($null -ne $sampleKey) {
        # Set already covered
        continue
    }

    Write-Host "Fetching set: $setId"
    $cards = Fetch-SetCards $setId
    foreach ($card in $cards) {
        $id = $card.id
        $st = $card.supertype
        if ($st -eq "Trainer")      { $code = "T" }
        elseif ($st -eq "Energy")   { $code = "E" }
        else                        { $code = "P" }
        $newEntries.Add('"' + $id + '":"' + $code + '"')
        $existingMap[$id] = $code
    }
    $processed++
    Write-Host "  -> $($cards.Count) cards added (sets done: $processed)"
    Start-Sleep -Milliseconds 300
}

if ($newEntries.Count -gt 0) {
    $trimmed  = $existingJson.TrimEnd().TrimEnd('}')
    $appended = $trimmed + ',' + ($newEntries -join ',') + '}'
    [System.IO.File]::WriteAllText($outPath, $appended, [System.Text.Encoding]::UTF8)
    Write-Host "Done. Appended $($newEntries.Count) new entries -> $outPath"
} else {
    Write-Host "All sets already covered. Nothing to append."
}
