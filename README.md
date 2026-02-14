# Pro Pro Sahur

## Stay focused. Tung Tung Sahur is watching.

A Windows desktop application that monitors your active window and intervenes when you visit distracting sites or apps. **Tung Tung Sahur**—a character that rests in the bottom-right corner of your screen—scolds you with GPT-generated roasts, speaks them aloud via Piper TTS, walks to the offending tab, and closes it. The goal is simple: reduce distractions and nudge you back to productive work.

## Overview

Pro Pro Sahur helps you stay accountable during work or study sessions. When you open YouTube, Discord, Reddit, TikTok, or other blocklisted sites, the app detects it within seconds. In the backend, GPT generates a playful insult whilst Piper converts it to natural speech, and the character "walks" toward the tab before closing it with Ctrl+W (browsers) or Alt+F4 (desktop apps). A cooldown between attacks prevents constant interruptions while still keeping you on track.

The blocklist is configurable and matches against window titles. You can add or remove sites and apps, adjust the check interval, and set the attack cooldown. Configuration lives in `%LocalAppData%\ProProSahur\config.json`. The app runs in the system tray; right-click the icon to exit.

## Features

- Real-time window title monitoring for distracting sites and apps
- GPT-powered insult generation with a consistent character persona
- Piper TTS for natural-sounding voice output (with Windows TTS fallback)
- Animated character that walks to the offending tab before closing it
- Configurable blocklist (YouTube, Twitter, Reddit, Discord, Steam, etc.)
- System tray integration for minimal footprint
- Cooldown and check-interval settings

## Prerequisites

- **Windows 10/11** — WPF, system tray, and Win32 APIs are Windows-only
- **.NET 8.0 SDK**
- **Python 3.8+** (for Piper TTS)
- **GPT API access** — OpenAI or compatible API (e.g., Ark Labs) for insult generation

## Installation

### 1. Install Dependencies

From the repository root:

```powershell
.\Install-Dependencies.ps1
```

This script checks or installs .NET 8, Python 3, and the Piper TTS packages (`piper-tts`, `pathvalidate`). It also downloads the Windows Piper executable and the Ryan (en_US) voice model from Hugging Face into `%LocalAppData%\ProProSahur\piper\`.

### 2. Configure GPT

Edit `%LocalAppData%\ProProSahur\config.json` and add your API key:

- **Ark Labs (GPT-compatible):** Set `ArkApiKey` and `LlmProvider: "ark"` (default).

### 3. Configure Piper (Optional)

Piper TTS is set up automatically by `Install-Dependencies.ps1`. The app uses Python Piper first; if that fails, it falls back to the Windows Piper executable. Config is written to `config.json` during install. If Piper is unavailable, the app uses Windows TTS.

## Development

### Run the Application

```powershell
cd ProProSahur
dotnet run
```

Tung Tung Sahur appears in the bottom-right corner. Open a blocklisted site (e.g., YouTube or Discord) to trigger a scold and tab close.

### Build a Standalone Executable

```powershell
cd ProProSahur
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `bin\Release\net8.0-windows\win-x64\publish\ProProSahur.exe`

### Exit

Right-click the Pro Pro Sahur icon in the system tray (bottom-right of the taskbar) → **Exit**.

## Usage

1. Run the app with `dotnet run` (or the published executable).
2. Tung Tung Sahur rests in the bottom-right corner.
3. Switch to a blocklisted site or app (YouTube, Discord, Reddit, etc.).
4. The app detects it, GPT generates a roast, Piper speaks it, and the character closes the tab.
5. Right-click the tray icon to exit.

## Contributors

- Megh Mistry
- Kulgagan Bajwa
- Mohammad Naqvi

## References

- [Piper TTS](https://github.com/rhasspy/piper) — Neural text-to-speech
- [Piper Voices (Hugging Face)](https://huggingface.co/rhasspy/piper-voices) — Voice models
