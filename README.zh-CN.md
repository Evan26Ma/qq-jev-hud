# QQ Jev HUD

> 无侵入的 QQ 聊天**意图 / 风险实时悬浮卡**，由 [TypeSafe Jev](https://typesafe.ai)（System One 结构化判断模型）驱动。
>
> [English](README.md) · **中文**
>
> 📖 **[完整使用说明](docs/USAGE.zh-CN.md)** —— 安装、配置 Key、看懂卡片、候选回复、话术、排查，都在这一篇。

QQ Jev HUD 是一个面向 Windows QQ 的**只读**伴生程序。它观察当前打开的聊天，在出现新的对方消息时读取最近可见对话，调用 [TypeSafe Jev](https://typesafe.ai)（或任何 OpenAI 兼容 LLM）做结构化判断，并把结果显示为锚定在对应消息旁的悬浮卡。

更重要的是，它不只告诉你"对方想干嘛"——还会**按你选的话术直接把候选回复写出来**，你挑一条**复制或填入 QQ 输入框**，**发送永远由你决定**。它**不会替你发送任何消息**，也从不修改 QQ。

## 特性

- **话我帮你想，发送你来定** —— 新消息一到，给出一块**选择面板**：4 个不同话术的候选，**每条都标出 Jev 预测的"可能出现的结果"**（被安抚 / 顺利接住 / 觉得被敷衍 / 可能更不满）+ 风险，并自动把更可能有好结果的排在前面。**点击即填入 QQ 输入框**，是否发送永远由你决定。
- **主界面引导** —— 启动就有功能说明 + 4 步配置检查清单（Jev / 候选回复 / QQ / 开始），哪步没配好直接给按钮跳过去；还能一键预览示例卡片和选择面板（不需要 QQ、不联网）。
- **无侵入** —— 不注入、不 Hook、不修改 QQ，不读取 QQ 数据库，只读取可见窗口。
- **本地 OCR** —— 用本地 [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) 识别可见消息；截图只在内存中处理，不落盘。
- **富维度判断卡** —— 卡片刻着**是谁说了什么**（引用原消息，群聊时带发送者昵称），再给出潜台词、情绪状态、真实意图（★）、语气亲疏、对方期待、关系状态、怎么回（★），每项带概率条；群聊刷屏、卡片与消息错位时，靠引用行也能对上号。
- **自带判断引擎** —— 可用 TypeSafe Jev，也可用任何 OpenAI 兼容 LLM（OpenAI / DeepSeek / Ollama / vLLM…）；两者输出的卡片与候选完全一致。
- **关系模式** —— 「通用（朋友/同事/群聊）」与「亲密关系（情侣/夫妻）」两套判断侧重。
- **联系人背景卡** —— `notes/<联系人>.md` 写下你们的约定、雷区和近况，保存即生效，判断与回复都会带上它。
- **点击穿透悬浮层** —— 卡片是透明、置顶、鼠标穿透的覆盖层，不抢焦点、不挡输入框。
- **隐私优先** —— 不保存聊天记录；API Key 存在 Windows 凭据管理器，绝不进仓库；日志只记匿名状态码。
- **离线 Mock 模式** —— `QQJEVHUD_MOCK=1` 用本地确定性判断跑通全流程（无需 Key、不联网）。

## 工作原理

1. 用 Win32 定位 QQ 聊天窗口，等待画面稳定。
2. 截取窗口并用本地 PaddleOCR 读取可见消息。
3. 检测新出现的对方（左侧）消息并去重。
4. 把当前消息 + 最近上下文发给 TypeSafe Jev（一次调用并行问多个类型化问题）。
5. 在点击穿透悬浮层上，为该消息渲染一张判断卡（意图 / 风险 / 是否需回应 / 建议）。

## 技术栈

- .NET 8 + WPF（悬浮层、托盘、Win32 窗口跟踪）
- 本地 OCR：PaddleOCR（Python worker，JSON-lines 管道）
- 结构化判断：TypeSafe Jev（`POST https://api.typesafe.ai/v1/systemone`）
- 测试：xUnit

## 快速开始

### 前置条件

- Windows 10/11
- .NET 8 SDK
- Python 3.12（x64）
- （可选）[TypeSafe](https://typesafe.ai) API Key —— 只有真实判断才需要

### 安装

```powershell
git clone https://github.com/Evan26Ma/qq-jev-hud.git
cd qq-jev-hud
.\setup.ps1      # 安装 .NET 8 SDK、项目专用 Python 环境和 PaddleOCR
.\create-shortcut.ps1   # 可选：建桌面快捷方式
```

### 配置判断引擎与 API Key

最简单：托盘菜单 → **设置…**，选择判断引擎（Jev / OpenAI 兼容 LLM / Mock），填写 Base URL、模型、API Key，并调卡片显示与隐私。Key 只存 **Windows 凭据管理器**（`QQJevHud.TypeSafe.ApiKey` / `QQJevHud.OpenAI.ApiKey`），**绝不**写入仓库、源码、日志或配置文件。

```powershell
# 方式一：用 cmdkey 写入凭据管理器
cmdkey /generic:QQJevHud.TypeSafe.ApiKey /user:QQJevHud /pass:<你的 TypeSafe API Key>

# 方式二：通过程序写入
echo '<你的 TypeSafe API Key>' | .\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --set-typesafe-key
```

不配 Key（或设置 `QQJEVHUD_MOCK=1`）时，HUD 以离线 Mock 模式运行。配好后可用 `--verify-typesafe` 做一次健康检查。

### 运行

```powershell
.\start.cmd
```

打开一个 QQ 聊天窗口，用托盘图标开始 / 暂停分析。`Ctrl + Alt + J` 暂停或恢复。

收到新消息后，QQ 右侧会浮出**判断卡**，同时弹出**选择面板**——判断和回复在同一处，每个选项都标着 Jev 预测的结果。托盘「怎么回…」随时可打开。

完整图文说明见 **[docs/USAGE.zh-CN.md](docs/USAGE.zh-CN.md)**：
[主界面](docs/USAGE.zh-CN.md#4-主界面) ·
[看懂判断卡](docs/USAGE.zh-CN.md#6-看懂判断卡) ·
[选择面板](docs/USAGE.zh-CN.md#7-选择面板看着结果选回复) ·
[联系人背景卡](docs/USAGE.zh-CN.md#8-联系人背景卡) ·
[排查表](docs/USAGE.zh-CN.md#12-排查) ·
[常见问题](docs/USAGE.zh-CN.md#13-常见问题)

### 预览界面（不需要 QQ）

```powershell
.\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --preview-card     # 富维度卡片
.\src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe --preview-replies  # 候选回复面板
```

## 隐私与安全

- **只读**：程序无法发送消息，也从不修改 QQ。
- 截图与聊天文本只在内存中处理，不持久化。
- 诊断日志只含匿名状态码（不含消息正文、联系人）。
- API Key 运行时从 Windows 凭据管理器读取，**绝不**提交到本仓库 / 源码 / 日志 / 配置。
- 只有在开启分析时，才会把"当前可见消息 + 必要上下文"发送给所配置的模型服务商。

## 开发

```powershell
dotnet build .\QQJevHud.sln
dotnet test  .\QQJevHud.sln
```

> 标记为 `Category=Integration` 的测试会调用本地 PaddleOCR worker，其中决策层测试还会用合成测试消息打真实 Jev API。只跑离线快测：`dotnet test --filter Category!=Integration`。

## 项目结构

```
src/QQJevHud/          WPF 主程序（托盘、悬浮层、窗口截图、OCR 客户端、判断提供方）
Services/              共享控制器（链接进主程序）
ocr/                   PaddleOCR worker（JSON-lines）
tests/QQJevHud.Tests/  xUnit 测试
docs/                  ADR、视觉规格、使用说明、设计文档
```

## 免责声明

本工具给出的是关于对话的**概率性猜测**，不是事实，也不是心理诊断。中文语境、反讽、表情包都可能造成误判。请把它当作一个温和的"第二意见"，而非定论，更不能替代真诚的沟通。
