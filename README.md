# QQ Jev HUD

> A non-intrusive, real-time **intent & risk HUD** for your QQ chats, powered by [TypeSafe Jev](https://typesafe.ai) (the System One structured-judgment model).
>
> **English** · [中文](README.zh-CN.md)

QQ Jev HUD is a **read-only** companion app for Windows QQ. It watches the currently open chat, and when a new incoming message appears it reads the recent visible conversation, asks [TypeSafe Jev](https://typesafe.ai) to make structured judgments (intent, relationship risk, whether a serious reply is expected), and renders the result as a small grey annotation card anchored next to that message — like "a Jev verdict under each message." It **never generates replies, never types into QQ, and never sends anything**.

## Highlights

- **Non-intrusive** — no injection, no hooking, no modification of QQ, no access to QQ's database. It only reads the visible window.
- **Local OCR** — visible text is read with local [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR); screenshots stay in memory and are never written to disk.
- **Real-time structured judgments** — per-message intent, relationship risk (1–10), and whether a serious reply is expected, with calibrated probabilities & confidence.
- **Click-through overlay** — cards are a transparent, topmost, mouse-transparent layer that never steals focus or blocks the input box.
- **Privacy first** — no chat history is stored; your API key lives in Windows Credential Manager and is never committed; logs carry only anonymous state codes.
- **Offline mock mode** — run with `QQJEVHUD_MOCK=1` for deterministic local judgments (no key, no network).

## How it works

1. Locate the QQ chat window (Win32) and wait for the frame to settle.
2. Capture the window and read the visible messages with PaddleOCR (local).
3. Detect new incoming (left-side) messages and de-duplicate.
4. Send the current message + recent context to TypeSafe Jev (one call asks several typed questions in parallel).
5. Render a per-message card (intent / risk / urgency / advice) on the click-through overlay.

## Tech stack

- .NET 8 + WPF (overlay, tray, Win32 window tracking)
- Local OCR: PaddleOCR (Python worker over a JSON-lines pipe)
- Structured judgments: TypeSafe Jev (`POST https://api.typesafe.ai/v1/systemone`)
- Tests: xUnit

## Getting started

### Prerequisites

- Windows 10/11
- .NET 8 SDK
- Python 3.12 (x64)
- (optional) a [TypeSafe](https://typesafe.ai) API key — only needed for real judgments

### Install

```powershell
git clone https://github.com/evan26ma/qq-jev-hud.git
cd qq-jev-hud
.\setup.ps1      # installs the .NET 8 SDK, a project Python env, and PaddleOCR
```

### Provide your TypeSafe API key (optional — real judgments only)

The key is stored in **Windows Credential Manager** (target `QQJevHud.TypeSafe.ApiKey`) and is **never** written to the repo, source, logs, or config.

```powershell
# Option 1 — write it with cmdkey
cmdkey /generic:QQJevHud.TypeSafe.ApiKey /user:QQJevHud /pass:<YOUR_TYPESAFE_API_KEY>

# Option 2 — write it via the app
echo '<YOUR_TYPESAFE_API_KEY>' | .\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --set-typesafe-key
```

Without a key (or with `QQJEVHUD_MOCK=1`) the HUD runs in offline mock mode. After configuring a key, `--verify-typesafe` runs a quick health check.

### Run

```powershell
.\start.cmd
```

Open a QQ chat and use the tray icon to start/pause analysis. `Ctrl+Alt+J` pauses or resumes.

## Privacy & security

- **Read-only** — the app cannot send messages and never modifies QQ.
- Screenshots and chat text are processed in memory only and never persisted.
- Diagnostic logs contain only anonymous state codes (no message text, no contacts).
- Your API key is read from Windows Credential Manager at runtime; it is **never** committed to this repository, source code, logs, or config files.
- Only the current visible message + minimal context is sent to the configured model provider — and only while analysis is enabled.

## Development

```powershell
dotnet build .\QQJevHud.sln
dotnet test  .\QQJevHud.sln
```

> Tests marked `Category=Integration` exercise the local PaddleOCR worker; the decision-layer test also calls the live Jev API with a synthetic test message. For the fast offline suite: `dotnet test --filter Category!=Integration`.

## Project layout

```
src/QQJevHud/          WPF app (tray, overlay, window capture, OCR client, decision providers)
Services/              shared controller (linked into the app)
ocr/                   PaddleOCR worker (JSON-lines)
tests/QQJevHud.Tests/  xUnit tests
docs/                  ADRs, visual spec, getting started, design notes
```

## Disclaimer

This tool surfaces **probabilistic guesses** about a conversation — not facts, and not a psychological assessment. Chinese nuance, sarcasm, and stickers can mislead it. Treat it as a gentle second opinion, never as ground truth, and never as a substitute for genuine communication.
