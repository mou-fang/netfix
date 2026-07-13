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
- 状态：已采纳（已更新：GitHub 仓库和 Actions 已实际运行）
- 决策：提供 `scripts/ci.sh` 本地质量门禁 + `.github/workflows/ci.yml` CI 配置文件。本地脚本和 GitHub Actions 均为主门禁。
- 理由：任务书要求 CI 配置就位；GitHub 仓库 `mou-fang/netfix` 已创建，Actions 已实际触发并通过。
- 影响：推送 main 或 PR 时自动触发 Linux（ubuntu-latest）+ Windows（windows-latest）双平台 CI。

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

## ADR-014：健康目标分类与 NCSI 正文验证

- 日期：2026-07-13
- 状态：已采纳
- 决策：引入 `HealthTargetCategory` 枚举，将健康目标分为三类：NCSI 正文验证（认证门户）、独立 HTTPS、全球服务路径。NCSI 目标关闭自动重定向并验证 "Microsoft Connect Test" 正文。全球服务路径目标失败不判定断网。
- 理由：不同目标有不同的语义和失败影响；单一目标失败不能判定断网。
- 影响：WEB-01 改用 NCSI 正文验证；WEB-04 辅助使用全球路径目标。

## ADR-015：WEB-03 无代理时返回 Skipped 而非冒充 Pass

- 日期：2026-07-13
- 状态：已采纳
- 决策：系统无代理时 WEB-03 返回 `Skipped` + `reused_from=WEB-02` 证据，不发出伪请求。
- 理由：不以 `<1ms Pass` 冒充独立 HTTPS 请求；明确区分真实请求与复用。
- 影响：WEB-03 证据包含 `request_made` 字段标记是否实际发出请求。

## ADR-016：PRX-02 使用 WinHTTP API 而非注册表推断

- 日期：2026-07-13
- 状态：已采纳
- 决策：WinHTTP 代理通过 `WinHttpGetDefaultProxyConfiguration` API 读取，不通过 WinINET 注册表项推断。
- 理由：WinINET 和 WinHTTP 是独立的代理配置层，注册表推断不可靠。
- 影响：所有代理探针在证据中标记 `proxy_layer` 字段（WinINET/WinHTTP/PAC/LocalPort）。

## ADR-017：TARGET-01 分阶段记录 DNS/TCP/TLS/HTTP

- 日期：2026-07-13
- 状态：已采纳
- 决策：TARGET-01 返回 `NormalizedTarget`（方案+主机+端口+TLS），DNS/TCP/TLS/HTTP 各阶段分别记录。http 默认 80 不执行 TLS，https 默认 443 执行 TLS，尊重显式端口，默认补全 HTTPS。
- 理由：不只返回"URL被接受"；分层记录便于定位故障层。
- 影响：`UrlNormalizer.Normalize` 替换 `NormalizeToHost` 为主接口。

## ADR-018：SYS-01 使用 NetGetJoinInformation 检测域加入

- 日期：2026-07-13
- 状态：已采纳
- 决策：域加入状态使用 `NetGetJoinInformation` API，不比较用户名/域名/环境变量。
- 理由：环境变量比较不可靠（WORKGROUP 机器的用户域名也可能与机器名不同）。
- 影响：SYS-01 证据包含 `join_status` 字段，值为 API 返回的状态。

## ADR-019：NET-01 多网卡候选列表

- 日期：2026-07-13
- 状态：已采纳
- 决策：多张网卡同时活动时保留候选列表，根据默认网关给出主接口。无法唯一确定时返回"多活动接口"（Warning），不随意选择第一张。
- 理由：简单选取第一张 Up 网卡可能导致误判。
- 影响：NET-01 证据包含 `candidates_with_gateway`、`primary_adapter` 字段。

## ADR-020：测试拆分为单元测试和集成测试

- 日期：2026-07-13
- 状态：已采纳
- 决策：普通 `dotnet test` 默认只运行可重复的单元/模拟测试（52 项），不依赖公网。真实公网和 Windows 探针测试标记为 Integration（8 项），通过 `NETMEDIC_INTEGRATION_TESTS=1` 环境变量触发。
- 理由：CI 和开发时不应依赖公网可用性；真实验证需显式触发。
- 影响：`IntegrationFact`/`IntegrationTheory` 自定义特性；报告分别列出结果。

## ADR-021：计划修复动作与可执行修复动作分离

- 日期：2026-07-13
- 状态：已采纳
- 背景：任务书计划了多个修复动作 ID（FIX-PRX-01、FIX-DNS-01 等），但阶段 3 尚无真实 `IRepairAction` 实现。需要一个机制记录将来计划，同时不误报当前可执行能力。
- 决策：`PlannedRepairActionIds` 记录任务书计划将来实现的动作 ID（仅记录，不代表当前可执行）；`ExecutableRepairActions` 为空集合，直到阶段 4 有真实 `IRepairAction` 实现并完成事务/快照/复检/安全测试后才逐个加入。不得根据 `PlannedRepairActionIds` 判断是否显示修复按钮。
- 理由：避免用户看到不可执行的修复按钮；明确区分"计划"与"当前能力"。
- 影响：所有规则的 `RecommendedActionId = null`；`SupportedRepairActions`（旧名）标记为 Obsolete，指向 `ExecutableRepairActions`。

## ADR-022：生产环境不使用伪成功

- 日期：2026-07-13
- 状态：已采纳
- 背景：阶段 1 假流程中有 `RepairResult_FakeSuccess` 和修复确认/修复结果页面。阶段 3 进入生产诊断后，伪成功会误导用户。
- 决策：删除 `RepairResult_FakeSuccess`；修复确认页（RepairConfirm）和修复结果页（RepairResult）在阶段 3 不可达。用户结果页不显示修复按钮。
- 理由：生产环境中不向用户展示虚假的成功反馈；没有真实修复能力时不应引导用户进入修复流程。
- 影响：`AppPage.RepairConfirm` 和 `AppPage.RepairResult` 在阶段 3 无导航入口；阶段 4 实现真实修复后重新启用。

## ADR-023：DNS 路径失败 ≠ DNS 缓存异常

- 日期：2026-07-13
- 状态：已采纳
- 背景：`DnsFailureRule` 检测 DNS-02 失败（系统 DNS 解析失败 + NET-03 网关正常）。早期实现曾推荐 FIX-DNS-01（清缓存），但服务器不可达与缓存污染是不同的问题。
- 决策：`DnsFailureRule` 不推荐 FIX-DNS-01。清除 DNS 缓存需要直接的缓存异常证据（如缓存了过期/错误记录），未来由 `DnsCacheAnomalyRule` 处理。当前阶段无 DNS 缓存探针（DNS-03/DNS-04），不推荐任何缓存相关修复。
- 理由：DNS 服务器不可达时清缓存无意义；错误地推荐不相关修复会损害用户信任。
- 影响：`DnsFailureRule.RecommendedActionId = null`；`PlannedRepairActionIds` 保留 FIX-DNS-01 供将来缓存异常规则使用。

## ADR-024：阶段 4 按动作逐个开启修复能力，非字符串白名单

- 日期：2026-07-13
- 状态：已采纳
- 背景：阶段 3.3 曾考虑通过字符串白名单批量开启修复动作。但这绕过了单个动作的安全验证。
- 决策：阶段 4 每个 `IRepairAction` 实现必须完成完整的事务（transaction）、快照（snapshot）、复检（verify）和回滚（rollback）流程，通过安全测试后，其动作 ID 才被加入 `ExecutableRepairActions`。不能提前批量开启。
- 理由：每个修复动作的风险和回滚需求不同；批量开启会跳过逐动作安全验证，违反任务书 §7 安全要求。
- 影响：`ExecutableRepairActions` 从空集合开始，阶段 4 按动作完成进度逐步增长；规则代码通过检查 `ExecutableRepairActions.Contains(actionId)` 决定是否设置 `RecommendedActionId`。
