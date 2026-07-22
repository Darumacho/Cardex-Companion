param([string]$ApiKey = "8c5d8afe-4a36-47bd-a401-e5aca8347075")

$headers = @{ "X-Api-Key" = $ApiKey }
$baseUrl = "https://api.pokemontcg.io/v2"
$root = Split-Path $PSScriptRoot -Parent
$seedDir = Join-Path $root "SeedData"

function Invoke-ApiWithRetry {
    param([string]$Url, [int]$MaxRetries = 4)
    $attempt = 0
    while ($true) {
        $attempt++
        try {
            return Invoke-RestMethod $Url -Headers $headers
        }
        catch {
            if ($attempt -ge $MaxRetries) { throw }
            $delay = [Math]::Pow(2, $attempt)
            Write-Host " [retry $attempt, ${delay}s]" -NoNewline -ForegroundColor Yellow
            Start-Sleep -Seconds $delay
        }
    }
}

Write-Host "Fetching sets..." -ForegroundColor Cyan
$setsResp = Invoke-ApiWithRetry "$baseUrl/sets?orderBy=releaseDate&pageSize=250"
$sets = $setsResp.data
Write-Host "  $($sets.Count) sets found"

$seedSets = $sets | ForEach-Object {
    [ordered]@{
        id          = $_.id
        name        = $_.name
        series      = $_.series
        total       = $_.total
        releaseDate = $_.releaseDate
        logoUrl     = $_.images.logo
        symbolUrl   = $_.images.symbol
    }
}
$seedSets | ConvertTo-Json -Compress | Out-File (Join-Path $seedDir "sets.json") -Encoding utf8NoBOM
Write-Host "  sets.json written" -ForegroundColor Green

Write-Host "Fetching cards for $($sets.Count) sets..." -ForegroundColor Cyan
$allCards = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[string]]::new()
$i = 0

foreach ($set in $sets) {
    $i++
    Write-Host "  [$i/$($sets.Count)] $($set.name)" -NoNewline
    $page = 1
    $fetched = 0
    $failed = $false

    while ($true) {
        $url = "$baseUrl/cards?q=set.id:$($set.id)&page=$page&pageSize=250&orderBy=number"
        try {
            $resp = Invoke-ApiWithRetry $url
        }
        catch {
            Write-Host " -- SKIPPED (error: $_)" -ForegroundColor Red
            $skipped.Add($set.name)
            $failed = $true
            break
        }

        if ($resp.data.Count -eq 0) { break }

        foreach ($card in $resp.data) {
            $allCards.Add([ordered]@{
                id         = $card.id
                name       = $card.name
                number     = $card.number
                setId      = $card.set.id
                imageSmall = $card.images.small
                rarity     = $card.rarity
            })
        }

        $fetched += $resp.data.Count
        if ($fetched -ge $resp.totalCount) { break }
        $page++
    }

    if (-not $failed) {
        Write-Host " -- $fetched cards"
    }
}

Write-Host ""
Write-Host "Total: $($allCards.Count) cards across $($sets.Count - $skipped.Count) sets" -ForegroundColor Cyan
$allCards | ConvertTo-Json -Compress | Out-File (Join-Path $seedDir "cards.json") -Encoding utf8NoBOM
Write-Host "cards.json written" -ForegroundColor Green

if ($skipped.Count -gt 0) {
    Write-Host "Skipped $($skipped.Count) sets: $($skipped -join ', ')" -ForegroundColor Yellow
}

Write-Host "Done. Run dotnet publish to embed the seed data." -ForegroundColor Green
