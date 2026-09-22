# QQ Jev HUD — User Guide

The complete user manual: installation, configuration, daily use, settings reference and troubleshooting.

> In one line: it **read-only** watches your QQ chat → recognizes text locally with OCR → sends *the other person's message* to a judgment service → floats a card beside it telling you **what they mean**, plus candidate replies in the tones you pick. **You always press send.**

---

## Contents

- [1. Install](#1-install)
- [2. Configure API keys](#2-configure-api-keys)
- [3. Launch](#3-launch)
- [4. Home screen](#4-home-screen)
- [5. Daily use](#5-daily-use)
- [6. Reading the judgment card](#6-reading-the-judgment-card)
- [7. Choosing a reply inside the card](#7-choosing-a-reply-inside-the-card)
- [8. Contact background cards](#8-contact-background-cards)
- [9. Relationship modes](#9-relationship-modes)
- [10. Settings reference](#10-settings-reference)
- [11. Privacy](#11-privacy)
- [12. Troubleshooting](#12-troubleshooting)
- [13. FAQ](#13-faq)

---

## 1. Install

**Requirements**: Windows 10/11, Python 3.12 (x64). The setup script installs the .NET 8 SDK if missing.

```powershell
git clone https://github.com/Evan26Ma/qq-jev-hud.git
cd qq-jev-hud
.\setup.ps1
```

`setup.ps1` installs the .NET 8 SDK (if needed), creates a private Python env (`.venv`), and installs local PaddleOCR. First run takes 5–15 minutes (model download).

Optionally create a desktop shortcut:

```powershell
.\create-shortcut.ps1
```

---

## 2. Configure API keys

Two services, configure as needed:

| Service | Role | Required? |
| --- | --- | --- |
| **TypeSafe Jev** | Judgment: intent, emotion, risk, how to reply | For real judgments |
| **OpenAI-compatible LLM** | Generation: writes candidate replies per tone | For candidate replies |

Neither is required — with no keys the app runs in **offline mock mode** so you can walk the whole flow.

> **Keys live only in Windows Credential Manager** — never in settings files, source, logs or Git. To remove them: `cmdkey /delete:QQJevHud.TypeSafe.ApiKey`.

### 2.1 Configure in the UI (recommended)

Right-click the tray icon → **设置…** (Settings):

1. Under **决策服务** pick "Jev（TypeSafe）" → paste your Jev API key → **保存 Key**
2. Click **验证** — it should report the service is available
3. For candidate replies, pick "OpenAI 兼容 LLM", enter **Base URL** / **model** / **API key**, save and verify

### 2.2 Configure from the command line

```powershell
$exe = ".\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe"

echo "<YOUR_TYPESAFE_KEY>" | & $exe --set-typesafe-key
echo "<YOUR_LLM_KEY>"      | & $exe --set-openai-key
```

Or use the built-in Windows command:

```powershell
cmdkey /generic:QQJevHud.TypeSafe.ApiKey /user:QQJevHud /pass:<YOUR_KEY>
```

### 2.3 OpenAI-compatible endpoints

The app speaks the standard `/chat/completions` API, so anything compatible works:

| Service | Base URL | Model example |
| --- | --- | --- |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| DeepSeek | `https://api.deepseek.com/v1` | `deepseek-chat` |
| Ollama (local) | `http://localhost:11434/v1` | `qwen2.5:14b` |
| vLLM / other | your endpoint + `/v1` | your model id |

> Local servers like Ollama don't check the key — any non-empty value works.

---

## 3. Launch

Double-click the **QQ Jev HUD** desktop shortcut, or:

```powershell
.\start.cmd
```

It's a **tray app** with no main window: after launch the tray icon appears and analysis starts automatically.

**Key requirement**: QQ must be in the foreground for cards to appear. The app only draws cards while QQ is in front, and hides them when you switch away — so it never covers the app you're actually using.

### Useful launch flags

```powershell
$exe = ".\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe"

& $exe --preview-card      # render a sample judgment card, no QQ needed
& $exe --preview-replies   # show the reply panel with sample data, no QQ needed
& $exe --open-settings     # launch and open Settings immediately
& $exe --verify-typesafe   # check the Jev key (exit code 0 = working)
```

---

## 4. Home screen

Launching opens the **home screen** (tray → 打开主界面). It is the app's introduction and readiness check:

- A one-line explanation of what the tool does, plus live status
- **开始使用** — a 4-step checklist, each with a tick and a "go configure / start" button:
  1. Configure the judgment service (Jev)
  2. Configure candidate replies (optional)
  3. Open a QQ chat window (QQ is detected automatically)
  4. Start analysis
- **它能做什么** — each capability in one line
- **先看看效果** — renders a sample judgment card / choice panel (**no QQ, no network**)

Closing the home screen does not quit the app; it keeps running in the tray.

---

## 5. Daily use

1. Open QQ and go to the chat you want to watch
2. Tray → **自动分析已开启** (start analysis)
3. The current visible messages become the **baseline** — old messages get no cards
4. When a **new incoming (left-side) message** arrives, a judgment card appears beside it within ~1–3 s, and the candidate-reply panel opens

### Tray menu

| Item | Action |
| --- | --- |
| 自动分析已开启 | Start / resume analysis |
| 暂停自动分析 | Stop reading the screen and clear in-memory cards |
| 重新校准聊天区域 | Manually box the message area (automatic by default; use if placement looks off) |
| 候选回复… | Open the candidate-reply panel |
| 设置… | Open the control panel / settings |
| 退出 | Quit |

### Hotkey

`Ctrl + Alt + J` — pause / resume (global, works without switching back to QQ)

### What it handles automatically

- **Session resets on chat switch** — no content from the previous conversation bleeds in
- **New messages only** — each message is judged once, no duplicate cards
- **Waits for a stable frame** — ~1 s of stillness before OCR, so half-loaded bubbles aren't misread

---

## 6. Reading the judgment card

The card floats over the QQ window and is mouse-transparent (never blocks clicks or steals focus). Read it bottom-up:

```
┌─────────────────────────────────────────┐
│ Jev · 对方真实意图                       │  ← card topic
│ 零：我昨天买的M7五级弹，4400一发…        │  ← ★ quote: WHO said WHAT
│ 她此刻最希望你怎样回应？                  │  ← the question being answered
│ ★ 真实意图                               │  ← ★ = read these first
│   自然分享        54% ███████████        │
│   表达情绪        28% ██████             │
│ ★ 怎么回                                 │
│   轻松接话        67% █████████████      │
│   先问清楚        18% ████               │
│ 潜台词                                   │
│   含蓄表达不满    42% █████████          │
│ 情绪状态                                 │
│   平静            38% ████████           │
│ 建议：认真回应重点…        低风险 · 2/10  │  ← advice + risk level
└─────────────────────────────────────────┘
```

### The quoted line (`零：我昨天买的M7…`)

**This solves "a busy group scrolls faster than the card can follow."** Every card quotes the message it is judging, with the speaker's name in group chats. Even after the conversation scrolls on, the quote tells you which message the card belongs to.

- Group chat: `sender: message text`
- 1:1 chat: `contact name: message text`
- Truncated past 52 characters with an ellipsis

### The ★ dimensions

★ marks the two **headline** judgments: `真实意图` (what they mean) and `怎么回` (how to respond). Those two are usually enough.

### Dimensions

| Dimension | Meaning |
| --- | --- |
| **★ 真实意图** | Their main communicative intent (wants a concrete answer / checking if you care / venting emotion / just sharing / asking for help / making a request) |
| **★ 怎么回** | Best response mode (engage the point / soothe first / give a concrete plan / keep it light / ask to clarify / set a boundary) |
| 潜台词 | Unspoken subtext (testing if you care / veiled dissatisfaction / hinting at a wish / no subtext) |
| 情绪状态 | Mood: calm / happy / expectant / down / anxious / angry / cold |
| 语气亲疏 | Tone distance, 5 levels from distant-polite to very close |
| 对方期待 | What they expect: a concrete answer / emotional support / company / a plan / nothing in particular |
| 关系状态 | Relationship state: on track / tense / easing / deadlocked / drifting |

Each dimension shows its **top two options** with probability bars — blue for the top choice, grey for the runner-up.

### Risk level

The bottom-right badge, e.g. "低风险 · 2/10", is relationship risk: **1–3 low** (normal), **5–7 worth attention** (respond carefully), **8–10 high** (may escalate).

### Card placement

When space is tight the app tries, in order: **below the message** → **beside it** → **right-hand side rail** → a small **dot marker** (hover for a tooltip). So cards aren't always glued to their message — **use the quoted line to match them up**.

> Judgments are **probabilistic guesses**, not facts, and not a psychological assessment. Treat them as a second opinion.

---

## 7. Choosing a reply inside the card

The replies **live inside the card** — no separate window. The judgment card starts collapsed with a
**「怎么回 ▾」** row at the bottom; click it to expand four candidates in place, click **「收起 ▴」** to collapse.

```
┌──────────────────────────────────────────┐
│ Jev · 对方真实意图                        │
│ 零：我昨天买的M7五级弹，4400一发…          │  ← who said what
│ ★ 真实意图  自然分享 54%  表达情绪 28%      │
│ ★ 怎么回    轻松接话 67%  先问清楚 18%      │
│ 建议 轻松接话，别把话题聊死      低风险 2/10 │
│ ────────────────────────────────────────  │
│ 怎么回 ▾                                  │  ← click to expand
└──────────────────────────────────────────┘
                    ↓ after clicking
┌──────────────────────────────────────────┐
│ … (unchanged above)                       │
│ 收起 ▴                                    │
│ ① 理科直男                                 │
│   确实贵，你是想囤还是自己用？              │
│   → 满意、顺利接住 58%  低风险             │  ← Jev's predicted outcome
│ ② 阴阳怪气  → 被安抚、情绪缓和 62%  低风险   │
│ ③ 高情商话术 …                            │
│ ④ 自然接话  → 觉得被敷衍 41%  需要留意      │
└──────────────────────────────────────────┘
```

> While candidates are being drafted the row reads 「正在想怎么回…」, then becomes 「怎么回 ▾」.

### Every option carries its predicted outcome

This is the key difference from a plain reply assistant: instead of handing you text and leaving you
to guess which is good, **Jev predicts how the other person would react** to each one — with a risk level.

| Predicted reaction | Meaning |
| --- | --- |
| 被安抚、情绪缓和 | They feel understood; their mood improves |
| 满意、顺利接住 | They accept it and the conversation flows on |
| 无明显变化 | They react flatly; the topic continues |
| 觉得被敷衍 | They feel the answer was perfunctory |
| 可能更不满 | They may get more upset — it could escalate |

All candidates are evaluated in a single request, so a full set costs about as much as one judgment.

### Ordering

Options are ranked by **"most likely to go well"**, not raw probability: good reaction first
(soothe/land it > neutral > brushed-off/upset), then lower risk, then probability. So "soothed, 55%"
ranks above "brushed off, 70%".

### Click to fill it in

**Click any option** → the text goes into QQ's input box. It **never sends**. The card shows the result
underneath; if QQ can't be focused it **falls back to copying** and tells you to paste with `Ctrl+V`.

### The green frame shows what is being read

The region being read is outlined in **green**: it clears the session list on the left, starts below the
chat header, ends above the input box, and runs to the chat panel's right edge — the real conversation,
not a fixed fraction of the window.

- While identifying the session the frame sits on the header, then moves to the messages
- When there is no room for a card, **the message itself is outlined in green** (far clearer than the old dot)
- Turn it off under Settings → 卡片显示 → 「用绿框标出正在识别的区域」

> The region is **detected from the picture** (message bubbles are brighter than the chat background, so
> it works in light and dark themes) — no manual calibration needed. If a particular chat is detected
> poorly, tray → 重新校准聊天区域 lets you box it yourself.
>
> **Note**: reading pauses while the home/settings window is open (that window would otherwise be
> captured), and resumes when you close it.

### The card travels with the chat window

Cards are **pinned to the QQ window**: move or resize QQ and they follow, instead of being left behind.
Switching conversations clears them.

---

## 8. Contact background cards

Write background notes for a contact or group and both the judgment and the candidate replies will use them — **the single most effective accuracy improvement**.

1. Create a file in `notes/` named after the **chat title**, e.g. `notes/tea.md`
2. Write a few lines:

```markdown
# Tea

- Agreements: weekend afternoons are ours — don't schedule anything else.
- Sore spots: don't bring up overtime or weight.
- Recent context: she's preparing for an exam this week and is stressed.
- How to address her: she goes by "Chacha".
```

3. **Save and it takes effect immediately** — no restart

The filename must match the chat title shown in QQ. This folder is never committed to Git; its contents stay on your machine and are only sent with a request when a new message arrives.

---

## 9. Relationship modes

Settings → 关系模式:

| Mode | For | Judgment focus |
| --- | --- | --- |
| **通用** (General) | Friends / colleagues / groups | The matter at hand, information flow, collaboration |
| **亲密关系** (Intimate) | Partners / spouses | Emotional connection, feeling valued, relationship warmth |

Switching modes changes both the judgment focus and how risk is framed.

---

## 10. Settings reference

Tray → **设置…**

### 状态 / 控制 (Status & control)

Live status plus three buttons: pause analysis / analyze current chat / recalibrate chat area.

### 决策服务 (Decision service)

Pick the judgment engine: **Jev (TypeSafe)** / **OpenAI-compatible LLM** / **offline mock**.

- Jev: enter the API key
- OpenAI-compatible: enter Base URL, model, API key
- Mock: nothing to configure (deterministic local data — good for previewing the UI)

Both have a **验证** (verify) button that sends one fixed test message (may incur a tiny API cost).

### 候选回复 (Candidate replies)

Auto-generation toggle, active tones, custom tones.

### 卡片显示 (Card display)

- Show intent probabilities
- Show relationship risk
- Show response advice
- Card opacity
- Max cards on screen (1–8)

### 隐私 (Privacy)

- **发送前脱敏敏感信息**: when on, amounts, account/card numbers, ID numbers, phone numbers and emails are replaced with `［已脱敏］` **before** anything leaves your machine

### 通用 (General)

Launch at login, view diagnostic log, hotkey reference.

---

## 11. Privacy

| Item | Detail |
| --- | --- |
| Screenshots | Processed in memory only — **never written to disk** |
| Chat text | **Not stored**, not logged |
| API keys | Windows Credential Manager only; never in settings files, source, logs or Git |
| Diagnostic log | Anonymous state codes only (e.g. `frame-settling`) — no chat content |
| What is sent | Only when analysis is on and a new message arrives: the current message + recent visible context, to the provider you configured |
| Redaction | On by default; sensitive tokens are replaced locally before sending |
| Clipboard | Read only when you explicitly ask it to analyze the clipboard |

It does **not**: inject into QQ, hook it, modify it, read QQ's database, or send any message.

---

## 12. Troubleshooting

Start with the diagnostic log:

```powershell
Get-Content "$env:LOCALAPPDATA\QQJevHud\runtime.log" -Tail 30
```

Healthy sequence: `qq-foreground` → `frame-settling` → `baseline-ready` → `waiting-for-incoming-message`

| Symptom | Cause and fix |
| --- | --- |
| **No cards appear** | QQ must be in the foreground. Click the QQ chat window so it's genuinely frontmost (a start menu or other window on top makes the app see "background") |
| **Card drifts away from the message** | A fast group or a scroll happened. **Read the quoted line** at the top; or tray → recalibrate |
| **Stuck on "正在识别当前聊天…"** | The first OCR loads the Paddle model — wait ~15–30 s |
| **`ocr-unavailable`** | Re-run `setup.ps1`; check `.venv\Scripts\python.exe` exists |
| **"Jev 请求失败: 422"** | Request-format problem (fixed in this repo) — please file an issue if it persists |
| **"Jev 请求失败: 400"** | Usually **extra characters in the API key** (newline/space). Re-paste the key in Settings |
| **"Jev 未配置"** | Key missing or unreadable — Settings → verify |
| **No candidate replies** | The **OpenAI-compatible LLM** key must be configured separately (the judgment and generation layers are independent) |
| **"Fill into QQ" does nothing** | It fell back to copying — switch to QQ and press `Ctrl+V` |
| **Nothing after switching chats** | Expected — switching rebuilds the baseline and only new messages are analyzed |

### Cards interfering with QQ?

The overlay is **mouse-transparent**: it doesn't block clicks or steal focus. Hit `Ctrl+Alt+J` to pause any time.

---

## 13. FAQ

**Does it upload my chat history?**
Only **the current message + ~10 recent visible messages**, and only when a new message arrives while analysis is on. No history is stored, no screenshots are written to disk.

**Will it reply for me?**
No. "Fill into QQ" only **pastes** a candidate into the input box — it **never presses Enter**. What (and whether) you send is entirely your call.

**Why only incoming (left-side) messages?**
By design. Your own messages don't generate cards, which keeps the screen readable.

**Can it watch several chats at once?**
No — only the **foreground** session. Switch away and it stops; switch back and it rebuilds the baseline.

**How accurate is it?**
It's a **probabilistic reference**, not truth. Chinese nuance, sarcasm and stickers can mislead it. Filling in [contact background cards](#7-contact-background-cards) noticeably improves relevance.

**Do I have to use Jev?**
No. The judgment layer can also run on an OpenAI-compatible LLM (switch it in 决策服务). With no keys at all it runs in offline mock mode.

**How much does it cost?**
One judgment request per message. Jev is very cheap (~$0.042 per 1M input tokens); with an OpenAI-compatible provider you pay your provider's rates.

**Which QQ versions are supported?**
Windows desktop QQ (the NT/Chromium build). Other builds have different window structures and may not be located.

**How do I fully uninstall?**

```powershell
cmdkey /delete:QQJevHud.TypeSafe.ApiKey
cmdkey /delete:QQJevHud.OpenAI.ApiKey
Remove-Item -Recurse "$env:LOCALAPPDATA\QQJevHud"
# then delete the project folder
```

---

## Disclaimer

Judgments are **statistical probabilities based on literal text** — they do not represent the other person's real thoughts, and they are not a psychological assessment. Use your own judgment for anything that matters, and don't let this replace genuine communication.
