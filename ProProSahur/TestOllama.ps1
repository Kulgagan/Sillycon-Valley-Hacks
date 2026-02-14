# Test Ollama API - same call Pro Pro Sahur makes
$body = @{
    model = "llama3.2"
    prompt = "You are Pro Pro Sahur, a judgmental Tung Tung Sahur character. The user was on YouTube instead of working. Invent a FRESH, CREATIVE insult - surprise with something unexpected: absurd metaphors, niche references, site-specific roasts, or bizarre comparisons. Include 'Pro Pro Sahur' naturally in the insult. NEVER generic. One short sentence, max 25 words. Output ONLY the insult, no quotes."
    stream = $false
    options = @{ temperature = 0.9; top_p = 0.95 }
} | ConvertTo-Json -Depth 5

Write-Host "=== Ollama API Request ===" -ForegroundColor Cyan
Write-Host $body
Write-Host "`n=== Sending to http://localhost:11434/api/generate ===" -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -ContentType "application/json" -Body $body
    Write-Host "`n=== Ollama API Response (full) ===" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10
    Write-Host "`n=== Extracted 'response' field (what Pro Pro Sahur uses) ===" -ForegroundColor Yellow
    Write-Host $response.response
} catch {
    Write-Host "`n=== Error ===" -ForegroundColor Red
    Write-Host $_.Exception.Message
    Write-Host "`nMake sure Ollama is running: ollama serve"
    Write-Host "And model is pulled: ollama pull llama3.2"
}
