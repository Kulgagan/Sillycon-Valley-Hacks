# Pro Pro Sahur - Install all dependencies (run from repo root)
# Requires: PowerShell 5.1+ (Windows)
# Does: .NET 8 check, Python 3 check, pip install piper-tts+pathvalidate, optional Windows Piper exe + default voice

$ErrorActionPreference = "Continue"  # don't exit on pip stderr etc.
Write-Host "`n=== Pro Pro Sahur - Dependency Setup ===" -ForegroundColor Cyan

# --- .NET 8 SDK ---
Write-Host "`n[1/4] Checking .NET 8 SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = $null
    try { $dotnetVersion = (dotnet --version 2>$null) } catch { }
    if (-not $dotnetVersion) { $dotnetVersion = "" }
    if ($dotnetVersion -match "^8\.\d+") {
        Write-Host "  OK - .NET $dotnetVersion" -ForegroundColor Green
    } else {
        Write-Host "  Need .NET 8. Current: $dotnetVersion" -ForegroundColor Red
        Write-Host "  Install: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
        Write-Host "  Or run: winget install Microsoft.DotNet.SDK.8 --accept-package-agreements" -ForegroundColor White
        $script:needDotnet = $true
    }
} catch {
    Write-Host "  .NET not found." -ForegroundColor Red
    Write-Host "  Install: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    Write-Host "  Or run: winget install Microsoft.DotNet.SDK.8 --accept-package-agreements" -ForegroundColor White
    $script:needDotnet = $true
}

# --- Python 3 (for Piper TTS) ---
Write-Host "`n[2/4] Checking Python 3 (for voice)..." -ForegroundColor Yellow
$pythonCmd = $null
foreach ($name in @("python", "python3", "py")) {
    try {
        $v = & $name --version 2>&1
        if ($v -match "Python 3\.(\d+)") {
            $minor = [int]$Matches[1]
            if ($minor -ge 8) {
                $pythonCmd = $name
                Write-Host "  OK - $name ($v)" -ForegroundColor Green
                break
            }
        }
    } catch { }
}
if (-not $pythonCmd) {
    Write-Host "  Python 3.8+ not found." -ForegroundColor Red
    Write-Host "  Install: https://www.python.org/downloads/ (check 'Add Python to PATH')" -ForegroundColor White
    Write-Host "  Or run: winget install Python.Python.3.12 --accept-package-agreements" -ForegroundColor White
    $script:needPython = $true
}

# --- Piper TTS (pip packages) ---
Write-Host "`n[3/4] Installing Piper TTS (Python)..." -ForegroundColor Yellow
if ($pythonCmd) {
    try {
        & $pythonCmd -m pip install --upgrade pip -q 2>$null
        & $pythonCmd -m pip install piper-tts pathvalidate -q
        Write-Host "  OK - piper-tts and pathvalidate installed" -ForegroundColor Green
    } catch {
        Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Run manually: $pythonCmd -m pip install piper-tts pathvalidate" -ForegroundColor White
        $script:needPip = $true
    }
} else {
    Write-Host "  Skipped (install Python first, then run this script again)" -ForegroundColor Yellow
}

# --- Windows Piper (standalone exe + default voice from Hugging Face) ---
Write-Host "`n[4/4] Windows Piper + voice model..." -ForegroundColor Yellow
$localAppData = [Environment]::GetFolderPath("LocalApplicationData")
$piperBase = Join-Path $localAppData "ProProSahur\piper"
# Zip extracts as piper/ containing piper.exe (or piper_windows_amd64/piper/ depending on release)
$piperExePath = Join-Path $piperBase "piper\piper.exe"
$piperExeAlt = Join-Path $piperBase "piper_windows_amd64\piper\piper.exe"
$piperZipUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip"
$voiceOnnxUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx"
$voiceJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx.json"

# Resolve actual exe path (may be piper\piper.exe or piper.exe under piperBase)
$resolvedExe = $null
if (Test-Path $piperExePath) { $resolvedExe = $piperExePath }
elseif (Test-Path $piperExeAlt) { $resolvedExe = $piperExeAlt }

if ($resolvedExe) {
    Write-Host "  OK - Windows Piper already at: $resolvedExe" -ForegroundColor Green
} else {
    try {
        $zipPath = Join-Path $env:TEMP "piper_windows_amd64.zip"
        Write-Host "  Downloading Windows Piper (~22 MB)..." -ForegroundColor Gray
        Invoke-WebRequest -Uri $piperZipUrl -OutFile $zipPath -UseBasicParsing -UserAgent "ProProSahur/1.0"
        if (-not (Test-Path $piperBase)) { New-Item -ItemType Directory -Path $piperBase -Force | Out-Null }
        Expand-Archive -Path $zipPath -DestinationPath $piperBase -Force
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        # Zip extracts to piper_windows_amd64/ or piper/; find piper.exe
        if (Test-Path $piperExePath) { $resolvedExe = $piperExePath }
        elseif (Test-Path $piperExeAlt) { $resolvedExe = $piperExeAlt }
        else {
            $found = Get-ChildItem -Path $piperBase -Filter "piper.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { $resolvedExe = $found.FullName }
        }
        if ($resolvedExe) {
            Write-Host "  OK - Extracted to: $piperBase" -ForegroundColor Green
        } else {
            Write-Host "  Extracted but piper.exe not found under: $piperBase" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Skipped - $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  Voice will use Python Piper (already installed above). Or download manually: $piperZipUrl" -ForegroundColor Gray
    }
}

# Download voice model from Hugging Face if needed
if ($resolvedExe) {
    $piperDir = Split-Path $resolvedExe -Parent
    $onnxPath = Join-Path $piperDir "en_US-ryan-high.onnx"
    if (-not (Test-Path $onnxPath)) {
        try {
            Write-Host "  Downloading voice model (Ryan, high) from Hugging Face..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $voiceOnnxUrl -OutFile $onnxPath -UseBasicParsing -UserAgent "ProProSahur/1.0"
            Invoke-WebRequest -Uri $voiceJsonUrl -OutFile (Join-Path $piperDir "en_US-ryan-high.onnx.json") -UseBasicParsing -UserAgent "ProProSahur/1.0"
            Write-Host "  OK - Voice model installed" -ForegroundColor Green
        } catch {
            Write-Host "  Voice download failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  OK - Voice model already present" -ForegroundColor Green
    }

    # Update config.json with Piper path
    $configDir = Join-Path $localAppData "ProProSahur"
    $configPath = Join-Path $configDir "config.json"
    if (-not (Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }
    try {
        $config = if (Test-Path $configPath) {
            Get-Content $configPath -Raw | ConvertFrom-Json
        } else {
            [PSCustomObject]@{}
        }
        $config | Add-Member -NotePropertyName PiperExecutablePath -NotePropertyValue $resolvedExe -Force
        $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8 -NoNewline
        Write-Host "  Config updated: PiperExecutablePath = $resolvedExe" -ForegroundColor Gray
    } catch {
        Write-Host "  Could not update config: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# --- Optional: Ollama ---
Write-Host "`n[Optional] Ollama (local LLM)..." -ForegroundColor Gray
try {
    $null = Get-Command ollama -ErrorAction Stop
    Write-Host "  Ollama is installed. Use LlmProvider: `"ollama`" in config." -ForegroundColor Green
} catch {
    Write-Host "  Ollama not installed. Optional - install from https://ollama.ai for local insults." -ForegroundColor Gray
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
if ($script:needDotnet) {
    Write-Host "  - Install .NET 8 SDK, then run this script again if needed." -ForegroundColor Yellow
}
if ($script:needPython) {
    Write-Host "  - Install Python 3.8+, then run this script again to install Piper TTS." -ForegroundColor Yellow
}
if (-not $script:needDotnet -and (-not $script:needPython -and -not $script:needPip)) {
    Write-Host "  All set! Run the app:" -ForegroundColor Green
    Write-Host "    cd ProProSahur" -ForegroundColor White
    Write-Host "    dotnet run" -ForegroundColor White
} else {
    Write-Host "  Fix any missing items above, then run:" -ForegroundColor Yellow
    Write-Host "    cd ProProSahur" -ForegroundColor White
    Write-Host "    dotnet run" -ForegroundColor White
}
Write-Host ""
