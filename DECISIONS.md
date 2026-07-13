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

## ADR-008：阶段 1 使用 ListBox 单选代替 RadioButton 绑定

- 日期：2026-07-13
- 状态：已采纳
- 决策：症状选择器用 ListBox + SelectedItem 双向绑定，而非 RadioButton + ConverterParameter。
- 理由：WPF 的 ConverterParameter 不支持 Binding 表达式，RadioButton 方案需要复杂 MultiBinding。ListBox 更简洁可靠。
- 影响：UI 行为一致，代码更少。

## ADR-009：探针编排器用 SemaphoreSlim 控制并发 + 独立 CancellationTokenSource

- 日期：2026-07-13
- 状态：已采纳
- 决策：ProbeOrchestrator 用 SemaphoreSlim 限制并发数，每个探针创建链接外部取消+总体超时的独立 CTS，再 CancelAfter 设置单探针超时。
- 理由：三层取消机制（外部取消/总体预算/单探针超时）需要清晰分离；链接 CTS 可正确区分取消来源。
- 影响：测试可精确验证三种取消场景，不依赖长时间 Sleep。

## ADR-010：NetworkHealthyRule 在 TARGET-01 失败时不触发

- 日期：2026-07-13
- 状态：已采纳
- 决策：当指定目标探针 TARGET-01 失败时，NetworkHealthyRule 不返回"本机正常"，让 SingleSiteIssueRule 优先命中。
- 理由：单站失败意味着存在局部问题，不应判定为完全健康。
- 影响：L15 场景正确归类为 single_site_issue 而非 network_healthy。

## ADR-011：阶段 1 实际有 5 种 UI 状态（修正报告）

- 日期：2026-07-13
- 状态：已采纳
- 决策：修正阶段 1 报告中"四页假流程"的措辞为"五种 UI 状态"：首页、体检中、结果页、修复确认页、修复结果页。
- 理由：AppPage 枚举实际有 5 个值（Home/Checking/Result/RepairConfirm/RepairResult）。
- 影响：文档准确性与代码一致。

## ADR-012：停止无限增加布尔字段，改用分组对象

- 日期：2026-07-13
- 状态：已采纳
- 决策：FakeNetworkEnvironment 当前 30 个布尔字段保留，但禁止继续增加布尔字段。阶段 2 起新增的状态数据使用明确枚举或分组对象（AdapterState、DnsState、ProxyState、WebState）。
- 理由：30 个布尔字段已接近可维护上限，继续增加会导致组合爆炸和可读性下降。
- 影响：Core 数据模型重构为分组结构，现有 24 项测试必须保持通过。

## ADR-013：Windows VM 不阻塞阶段 2 开始

- 日期：2026-07-13
- 状态：已采纳
- 决策：阶段 2 先在当前 Windows 电脑实现和验证正常状态下的只读探针。阶段 2 最终标记完成前，必须配置 Windows VM 或明确列出未验证的异常场景。
- 理由：正常状态探针可在本机验证；故障注入需要 VM 隔离。
- 影响：阶段 2 不得通过修改当前主力电脑网络来制造故障。
