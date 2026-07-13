# NetMedic 任务跟踪

> 任务书：网络急救箱：轻量全能版 Agent AI 开发总任务书 v2.0

## 阶段总览

| 阶段 | 名称 | 状态 |
|---|---|---|
| 0 | 极简工程骨架 | 已完成 |
| 1 | 模型、模拟器与完整 UI 假流程 | 已完成 |
| 2 | 快速只读体检 | 代码完成（CI 通过） |
| 3 | 诊断规则和普通用户结论 | 代码完成，待 Windows VM 验证（阻塞） |
| 4 | 低风险修复与同 EXE 提权 | 未开始 |
| 5 | 中风险修复与深度体检 | 未开始 |
| 6 | 应用级检查、报告与历史 | 未开始 |
| 7 | 轻量发布与公开测试 | 未开始 |

---

## 阶段 0：极简工程骨架

### 任务

| # | 任务 | 状态 |
|---|---|---|
| 1 | 创建 `NetMedic.slnx` | 已完成 |
| 2 | 创建 Core / App / Tests 三个项目 | 已完成 |
| 3 | Core 目标 net10.0；App 目标 net10.0-windows 启用 WPF | 已完成 |
| 4 | 能启动的单窗口 WPF 空壳 | 已完成 |
| 5 | 格式化、nullable、warnings-as-errors、基础测试 | 已完成 |
| 6 | 本地 CI 脚本 + ready-to-enable GH Actions | 已完成 |
| 7 | 任务/决策/安全/限制/Windows QA 文档 | 已完成 |
| 8 | 记录 Windows Runner/VM 状态与阻塞项 | 已完成（见 docs/env-capability.md） |

### 验收门槛

| 门槛 | 结果 |
|---|---|
| 三个项目构建关系正确 | 通过（dotnet build 0 错误） |
| Core 测试能在 Linux 运行 | 通过（GitHub Actions Linux CI 绿，net10.0 144 项通过） |
| WPF App 在 Windows 构建 | 通过 |
| 无 Node/Rust/Tauri/数据库/服务依赖 | 通过（仅 CommunityToolkit.Mvvm + xUnit） |
| 生产 NuGet 依赖不超范围 | 仅 CommunityToolkit.Mvvm 8.4.2 |

### 阻塞项

- **Windows VM 尚未配置。** 必须在阶段 2 真实 Windows 探针和阶段 4 修复功能前解决。GitHub Actions Windows Runner 是 github-hosted 的 Windows Server，不是 Windows 11 桌面 VM，无法用于故障注入测试。

---

## 阶段 1：模型、模拟器与完整 UI 假流程

### 任务

| # | 任务 | 状态 |
|---|---|---|
| 1 | ProbeResult/DiagnosticSnapshot/Finding/RepairActionDescriptor 模型 | 已完成 |
| 2 | 探针调度、取消、独立超时、总预算 | 已完成 |
| 3 | FakeNetworkEnvironment + L01/L02/L09/L14/L15 fixture | 已完成 |
| 4 | 首页->体检中->结果->修复确认 完整假流程 | 已完成 |
| 5 | 文案使用资源文件，不散落硬编码 | 已完成 |
| 6 | 技术详情折叠 | 已完成 |

### 验收门槛

- Linux 上能测试完整规则与调度。
- Windows 上能演示从首页到结果的模拟流程。
- 取消和超时不冻结 UI。
- 取消、单探针超时、总体超时有自动测试；测试不依赖长时间 Thread.Sleep。
- L01/L02/L09/L14/L15 五场景结果稳定可重复。
- 所有用户文案在资源文件中。

---

## 阶段 2：快速只读体检

### 状态

代码完成（CI 通过）。真实 Windows 探针在本机验证通过；故障注入场景待 Windows VM。

### 任务

| # | 任务 | 状态 |
|---|---|---|
| 1 | Core 数据模型重构为分组对象（SystemState/AdapterState/DnsState/ProxyState/WebState） | 已完成 |
| 2 | 14 个真实只读 Windows 探针（SYS/NET/DNS/PRX/WEB/TARGET） | 已完成 |
| 3 | WindowsProbeSet 构建器、WindowsNetworkEnvironment 实现 | 已完成 |
| 4 | --verify-probes 命令行验证模式 | 已完成 |
| 5 | 探针语义修正（NCSI 正文验证、WinHTTP API、URL 规范化、多网卡候选） | 已完成 |
| 6 | 测试拆分为单元测试（默认）和集成测试（环境变量触发） | 已完成 |
| 7 | GitHub Actions CI 配置（Linux + Windows） | 已完成，CI 通过 |

### 阻塞项

- **Windows VM 尚未配置。** 正常状态探针已在本机验证，故障注入场景需 VM 隔离。GitHub Actions Windows Runner 是 github-hosted 的 Windows Server，不是 Windows 11 桌面 VM，不能用于故障注入。

---

## 阶段 3：诊断规则和普通用户结论

### 状态

代码完成，待 Windows VM 验证（阻塞）。CI 全绿。

### 任务

| # | 任务 | 状态 |
|---|---|---|
| 1 | 诊断规则引擎（10 条规则 + InconclusiveRule 兜底） | 已完成 |
| 2 | 用户结果页：普通语言摘要 + 引导面板（与技术详情分离） | 已完成 |
| 3 | 规则冲突处理（无重复 Finding、无矛盾首选结论） | 已完成 |
| 4 | 保护上下文降级（域/RDP/多活动网卡） | 已完成 |
| 5 | 资源完整性测试（所有 Finding key 非空） | 已完成 |
| 6 | NET-01 契约对照测试 | 已完成 |

### 阶段 3.3 修复动作修正

- **无可执行修复动作**：`ExecutableRepairActions` 集合为空，所有规则的 `RecommendedActionId = null`。
- **无伪成功**：`RepairResult_FakeSuccess` 已删除，修复确认/修复结果页在本阶段不可达。
- **不显示安全修复按钮**：用户结果页不显示修复按钮。
- **计划 vs 可执行分离**：`PlannedRepairActionIds` 记录任务书计划，`ExecutableRepairActions` 为空直到阶段 4 有真实 `IRepairAction` 实现。
- **DNS 路径异常 ≠ DNS 缓存异常**：`DnsFailureRule` 不推荐 FIX-DNS-01（清缓存）；服务器不可达不等于缓存问题，清缓存需要直接缓存异常证据（未来 `DnsCacheAnomalyRule`）。

### 阻塞项

- **Windows VM 尚未配置。** 阶段 3 代码和模拟测试已完成并通过 CI，但真实故障注入验证需 Windows VM。GitHub Actions Windows Runner 不是 Windows 11 故障 VM。
