# NetMedic 技术决策记录

## ADR-001：从 Tauri/React/Rust 切换到 C#/.NET10/WPF

- 日期：2026-07-13
- 状态：已采纳
- 背景：原 v1.0 任务书采用 Tauri 2 + React + TypeScript + Rust 多 crate 架构。v2.0 任务书明确要求改用 C# + .NET 10 LTS + WPF 轻量架构。
- 决策：完全采用 v2.0 方案，旧计划不执行。工作目录原为空，无旧代码需迁移。
- 理由：单语言、少依赖、无 WebView、无数据库、无常驻服务，符合"轻量全能"定位。
- 影响：不再需要 Rust/Node 工具链；需要 .NET 10 SDK。

## ADR-002：仅两个生产项目 + 一个测试项目

- 日期：2026-07-13
- 状态：已采纳
- 决策：`NetMedic.Core`（net10.0 跨平台逻辑）+ `NetMedic.App`（net10.0-windows WPF）+ `NetMedic.Tests`。不再拆分更多项目。
- 理由：任务书 §0 和 §8.2 明确限制项目数，避免过度工程化。
- 影响：Core 内部用文件夹组织（Diagnostics/Abstractions/...），不建更多 csproj。

## ADR-003：CommunityToolkit.Mvvm 作为唯一生产 MVVM 依赖

- 日期：2026-07-13
- 状态：已采纳
- 决策：使用 CommunityToolkit.Mvvm 8.4.2 实现ObservableObject/RelayCommand。
- 理由：任务书 §8.1 明确允许；避免手写 INPC 在阶段 1 返工；不引入 Prism/ReactiveUI 等重框架。
- 影响：阶段 0 空壳即用它绑定，阶段 1 直接扩展。

## ADR-004：本地 CI 为主，GH Actions 配置备用

- 日期：2026-07-13
- 状态：已采纳
- 决策：提供 `scripts/ci.sh` 本地质量门禁 + `.github/workflows/ci.yml` ready-to-enable 文件。本地脚本是主门禁。
- 理由：用户当前无 GitHub 仓库，但任务书要求 CI 配置就位。
- 影响：连上 GitHub 后 GH Actions 自动生效。

## ADR-005：solution 使用 .slnx 格式

- 日期：2026-07-13
- 状态：已采纳
- 决策：采用 .NET 10 默认的 `NetMedic.slnx`（XML solution 格式）而非传统 `.sln`。
- 理由：.NET 10 SDK 的 `dotnet new sln` 默认生成 .slnx；更简洁、易 diff。
- 影响：所有 dotnet 命令使用 `NetMedic.slnx`。

## ADR-006：TreatWarningsAsErrors 全局启用

- 日期：2026-07-13
- 状态：已采纳
- 决策：Core 和 App 项目均启用 `TreatWarningsAsErrors`。
- 理由：从第一天强制代码质量，避免警告堆积。
- 影响：任何警告都会阻断构建。

## ADR-007：WPF 空壳最小化，症状选择器留待阶段 1

- 日期：2026-07-13
- 状态：已采纳
- 决策：阶段 0 只做欢迎窗口（应用名/版本/就绪状态/占位按钮），不做症状选择器和 Mock 诊断流程。
- 理由：避免与阶段 1 重叠；任务书说"空壳"。
- 影响：点击"开始体检"只显示占位提示。
