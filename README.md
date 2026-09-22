# QQ Jev HUD

> A non-intrusive, real-time **intent & risk HUD** for your QQ chats, powered by [TypeSafe Jev](https://typesafe.ai) (the System One structured-judgment model).
>
> **English** · [中文](README.zh-CN.md)
>
> 📖 **[Full user guide](docs/USAGE.md)** — install, API keys, reading the card, candidate replies, tones, troubleshooting.

QQ Jev HUD is a **read-only** companion app for Windows QQ. It watches the currently open chat and, when a new incoming message appears, reads the recent visible conversation and asks [TypeSafe Jev](https://typesafe.ai) (or any OpenAI-compatible LLM) for structured judgments — rendered as a card anchored beside the message.

More importantly, it doesn't stop at telling you what the other person means: it **drafts candidate replies in the tones you choose**, and you **copy one or fill it into QQ** — **sending is always your call**. It never sends anything for you and never modifies QQ.

## Highlights

- **"We draft it, you send it"** — the judgment card carries a **「怎么回 ▾」** row: click the card to expand four candidates in place, **each labelled with the outcome Jev predicts** (soothed / lands well / feels brushed off / may get worse) and a risk level, ranked so the most promising comes first. **Click one to fill it into QQ** — sending stays your call.
- **Guided home screen** — on launch: what the tool does plus a 4-step readiness checklist (Jev / replies / QQ / start), with a button that jumps straight to whatever is unconfigured. Sample cards and a sample choice panel can be previewed with no QQ and no network.
- **Non-intrusive** — no injection, no hooking, no modification of QQ, no access to QQ's database. It only reads the visible window.
- **Local OCR** — visible text is read with local [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR); screenshots stay in memory and are never written to disk.
- **Rich judgment card** — each card quotes **who said what** (the original message, with the speaker's name in group chats), then gives subtext, emotion, real intent (★), tone distance, what they expect, relationship state, and how to reply (★) with probability bars. The quote keeps the card identifiable even when a busy group scrolls past it.
- **Bring your own engine** — TypeSafe Jev **or** any OpenAI-compatible LLM (OpenAI / DeepSeek / Ollama / vLLM…); cards and candidates look identical either way.
- **Relationship modes** — a general profile (friends/colleagues/groups) and an intimate-relationship profile.
- **Contact background cards** — drop `notes/<contact>.md` with your agreements, sore spots and recent context; it takes effect on save.
- **Cards travel with the chat window** — pinned to the QQ window and following it as it moves or resizes; the area outside a card stays click-through, so the chat remains fully usable.
- **Privacy first** — no chat history stored; keys live in Windows Credential Manager and are never committed.
- **Offline mock mode** — `QQJEVHUD_MOCK=1` runs the whole pipeline locally (no key, no network).

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
git clone https://github.com/Evan26Ma/qq-jev-hud.git
cd qq-jev-hud
.\setup.ps1      # installs the .NET 8 SDK, a project Python env, and PaddleOCR
.\create-shortcut.ps1   # optional: create a desktop shortcut
```

### Configure the engine & API key

Easiest: tray menu → **设置…** to pick the judgment engine (Jev / OpenAI-compatible LLM / mock), enter the Base URL + model + API key, and tune the cards & privacy. Keys live only in **Windows Credential Manager** (`QQJevHud.TypeSafe.ApiKey` / `QQJevHud.OpenAI.ApiKey`) and are **never** written to the repo, source, logs, or config.

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

When a new message arrives you get a **judgment card** over QQ plus a **choice panel** — judgment and replies in one place, every option labelled with its predicted outcome. Tray → 怎么回… reopens the panel.

Full illustrated guide: **[docs/USAGE.md](docs/USAGE.md)** —
[home screen](docs/USAGE.md#4-home-screen) ·
[reading the card](docs/USAGE.md#6-reading-the-judgment-card) ·
[the choice panel](docs/USAGE.md#7-the-choice-panel-pick-a-reply-by-its-outcome) ·
[contact background cards](docs/USAGE.md#8-contact-background-cards) ·
[troubleshooting](docs/USAGE.md#12-troubleshooting) ·
[FAQ](docs/USAGE.md#13-faq)

### Preview the UI (no QQ needed)

```powershell
.\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --preview-card     # rich judgment card
.\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --preview-replies  # candidate replies
```

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
