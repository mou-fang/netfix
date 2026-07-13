# 阶段 0 设计：极简工程骨架（轻量全能版）

> 对应任务书：§12 阶段 0  
> 任务书版本：v2.0（轻量化重构版）  
> 编写日期：2026-07-13  
> 状态：待实现

## 1. 背景与范围

本项目原计划采用 Tauri 2 + React + Rust 多 crate 架构（v1.0 任务书），后改为 **C# + .NET 10 LTS + WPF** 轻量架构（v2.0 任务书）。旧方案从未产生任何代码（工作目录原为空），因此不存在旧代码备份问题。本设计直接从空目录按新架构开始。

阶段 0 范围严格限定为"极简工程骨架"：三个项目能构建、能跑基础测试、WPF 空壳能启动、文档和 CI 就位。**不实现任何探针、规则、修复或真实诊断逻辑**——这些属于阶段 1 及以后。

### 1.1 明确排除（阶段 0 不做）

- 症状选择器、正式诊断流程 UI、Mock 数据（阶段 1）
- 任何探针、规则、修复动作实现
- UAC Worker 模式、命名管道（阶段 4）
- JSON 历史、报告、脱敏（阶段 6）
- 代码签名、安装包、单文件发布（阶段 7）

## 2. 技术栈锁定

| 层 | 选型 | 版本 |
|---|---|---|
| 语言 | C# | 13（随 .NET 10） |
| Runtime | .NET 10 LTS | SDK 10.0.301+（最新 10.0.x） |
| UI | WPF / XAML | .NET 10 内置 |
| MVVM | CommunityToolkit.Mvvm | 最新稳定版（唯一生产 NuGet 依赖） |
| 测试 | xUnit | 最新稳定版 |
| JSON | System.Text.Json | BCL 内置 |
| 存储 | JSON 文件 | 无数据库 |

不引入：Prism、ReactiveUI、MediatR、Autofac、Serilog、EF、日志框架、DI 容器、主题框架。

## 3. 仓库结构

```text
newnetfix/
├─ NetMedic.sln
├─ src/
│  ├─ NetMedic.Core/              # net10.0，跨平台纯逻辑
│  │  ├─ NetMedic.Core.csproj
│  │  └─ Abstractions/            # 接口占位（仅最小骨架）
│  ├─ NetMedic.App/               # net10.0-windows，WPF
│  │  ├─ NetMedic.App.csproj
│  │  ├─ App.xaml / App.xaml.cs
│  │  ├─ Views/
│  │  │  └─ MainWindow.xaml / .cs
│  │  ├─ ViewModels/
│  │  │  └─ MainViewModel.cs
│  │  └─ Resources/
│  └─ (无更多生产项目)
├─ tests/
│  └─ NetMedic.Tests/             # net10.0，xUnit
│     ├─ NetMedic.Tests.csproj
│     └─ SmokeTests.cs
├─ docs/
│  ├─ threat-model.md             # 任务书 §9 最小化
│  ├─ privacy-data-inventory.md   # 任务书 §9.3 清单
│  ├─ env-capability.md           # 环境与阻塞项
│  ├─ windows-support-matrix.md   # Win11 x64 主支持
│  ├─ known-limitations.md        # 任务书 §0.2
│  ├─ windows-qa.md               # 任务书 §0.2（阶段0为空模板）
│  └─ superpowers/specs/          # 本设计文档所在
├─ scripts/
│  ├─ ci.sh                       # 本地 CI 主入口
│  └─ install-hooks.sh            # git hooks 安装
├─ .gitignore
├─ .editorconfig
├─ TASKS.md
├─ DECISIONS.md
├─ CHANGELOG.md
└─ README.md
```

## 4. 项目详情

### 4.1 NetMedic.Core（`net10.0`）

- 跨平台纯逻辑：模型、调度接口、规则接口、报告接口、脱敏接口。
- 阶段 0 只放最小骨架：一个 `Abstractions/` 目录，含 `INetworkEnvironment` 空接口（供阶段 1 扩展），以及一个占位 `CoreMarker` 类供 Tests 引用验证引用关系。
- 不依赖任何 Windows API。

### 4.2 NetMedic.App（`net10.0-windows`）

- WPF 单窗口空壳。
- `MainViewModel` 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`。
- `StartCheckupCommand` 用 `CommunityToolkit.Mvvm.Input.RelayCommand`，点击后设置 `StatusMessage = "功能将在后续阶段实现。"`。
- `MainWindow` 绑定 ViewModel，显示：应用名 "网络急救箱"、版本号、"已就绪"状态、一个"开始体检"按钮、状态消息区域。
- 不做症状选择器、不做诊断流程。

### 4.3 NetMedic.Tests（`net10.0`）

- xUnit。
- 阶段 0 仅一个冒烟测试：验证 `NetMedic.Core.CoreMarker` 存在（证明测试项目引用 Core 正确）。
- 后续阶段在此添加规则、调度、脱敏测试。

## 5. CI 与本地质量门禁

用户当前无 GitHub，采用本地 CI 为主：

- `scripts/ci.sh`：`dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。在 Windows Git Bash 下运行。
- `scripts/install-hooks.sh`：安装 pre-commit 钩子，提交前运行 `dotnet format --verify-no-changes` 和 `dotnet build`。
- 额外提供 `.github/workflows/ci.yml` 作为 ready-to-enable 文件（Linux 跑 Core 测试 + Windows 构建 solution），用户连上 GitHub 后即可用，但本地脚本是主门禁。

## 6. 安全基线（阶段 0 最小要求）

阶段 0 不实现修复，但必须建立基线：

- 无任何 shell 执行、无进程启动、无网络请求。
- WPF 空壳不请求管理员权限（app.manifest `requestedExecutionLevel="asInvoker"`）。
- 无常驻服务、无开机启动、无托盘。
- `docs/threat-model.md` 记录任务书 §9.1 的输入安全、UAC 安全要点（为后续阶段预留）。

## 7. 文档交付物

| 文件 | 内容 | 来源 |
|---|---|---|
| TASKS.md | 阶段 0 任务拆分与状态 | §0.2 |
| DECISIONS.md | 技术栈、MVVM 依赖、CI 策略、空壳范围决策 | §0.2 |
| CHANGELOG.md | 阶段 0 变更 | §0.2 |
| docs/threat-model.md | 最小威胁模型 | §9 |
| docs/privacy-data-inventory.md | 隐私数据清单 | §9.3 |
| docs/env-capability.md | 环境检查：.NET 10 SDK 已装；Windows Runner/VM 标阻塞 | §阶段0任务8 |
| docs/windows-support-matrix.md | Win11 x64 主，Win10 22H2 尽力 | §3.1 |
| docs/known-limitations.md | 当前限制（阶段 0 无诊断能力） | §0.2 |
| docs/windows-qa.md | Windows QA 模板（阶段 0 待填） | §0.2 |
| README.md | 构建与测试说明 | - |

## 8. 验收门槛对照（任务书 §阶段0）

| 门槛 | 满足方式 |
|---|---|
| 三个项目构建关系正确 | `dotnet build` 成功，Tests 引用 Core |
| Core 测试能在 Linux 运行 | Core 目标 net10.0；测试跨平台（实际在 Windows 验证，Linux 可行性由 TFM 保证） |
| WPF App 在 Windows Runner 构建 | `dotnet build` App 成功 |
| 无 Node、Rust、Tauri、数据库、服务依赖 | csproj 无相关引用，无 PackageReference 超出 CommunityToolkit.Mvvm + xUnit |
| 生产 NuGet 依赖不超过必要范围 | 仅 CommunityToolkit.Mvvm |

### 已知阻塞项

- **Windows WPF 实际运行验证**：当前无 Windows CI Runner 或 Windows VM。`dotnet build` 验证可编译性，但任务书 §阶段0验收要求"WPF App 在 Windows Runner 构建"——当前在本机 Windows 11 上可构建和启动，但缺乏隔离的 Windows VM 做故障注入测试。此为阶段 2+ 的阻塞项，阶段 0 在本机验证启动即可。
- **Linux CI**：当前开发机为 Windows，Linux Core 测试的可运行性由 `net10.0` TFM 保证，但未在真实 Linux 环境验证。`.github/workflows/ci.yml` 已配置 Linux Job，连上 GitHub 后自动验证。

## 9. 完成后的阶段 0 报告

按任务书 §15 模板提交，包含：已完成、轻量化检查、测试（Linux Core / Windows 构建）、系统修改与安全、尚未解决、下一阶段。
