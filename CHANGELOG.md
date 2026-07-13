# Changelog

本项目所有用户可见变化记录在此。格式参考 [Keep a Changelog](https://keepachangelog.com/)。

## [0.2.0] - 2026-07-13

### 阶段 1：模型、模拟器与完整 UI 假流程

#### 新增
- Core 领域模型：`ProbeResult`、`DiagnosticSnapshot`、`Finding`、`RepairActionDescriptor`、`ProbeError`、`DiagnosticSession`。
- 探针基础设施：`IProbe` 接口、`ProbeContext`、`ProbeOrchestrator`（并发调度、独立超时、总体超时、取消、进度事件）。
- 规则引擎：`IDiagnosisRule` 接口、`RuleRegistry`，5 条内置规则（失效代理/DNS 故障/NCSI 不一致/单站故障/网络正常）。
- 模拟环境：`FakeNetworkEnvironment`、`FakeProbe`、`FakeProbeSet`，5 个场景 fixture（L01/L02/L09/L14/L15）。
- UI 假流程：首页（症状选择器）-> 体检中（五段进度+取消）-> 结果页（结论+技术详情折叠）-> 修复确认页（影响说明）-> 修复结果页。
- 资源文件 `Strings.resx`：所有用户文案集中管理，不散落硬编码。
- 24 项测试：编排器超时/取消/总预算/异常/进度（6 项）+ 5 场景稳定性与预期验证（18 项）。

#### 变更
- `MainViewModel` 从阶段 0 占位扩展为完整四页状态机。
- `MainWindow.xaml` 重写为多页面切换布局。

#### 安全
- 无真实 Windows API 调用、无真实网络请求、无真实修复。
- 所有修复动作为假流程模拟。

## [0.1.0] - 2026-07-13

### 阶段 0：极简工程骨架

#### 新增
- 建立 C# + .NET 10 + WPF 工程骨架（`NetMedic.Core`、`NetMedic.App`、`NetMedic.Tests`）。
- WPF 单窗口欢迎界面：显示应用名称、版本、就绪状态，含"开始网络体检"占位按钮。
- Core 领域模型骨架：`ProbeStatus`、`Confidence` 枚举，`INetworkEnvironment` 接口。
- xUnit 冒烟测试（3 项），验证 Tests→Core 引用关系。
- 本地 CI 脚本（`scripts/ci.sh`）与 ready-to-enable 的 GitHub Actions 配置。
- 项目文档：TASKS、DECISIONS、威胁模型、隐私清单、环境能力、支持矩阵、已知限制、Windows QA 模板。

#### 安全
- app.manifest 设置 `asInvoker`，默认不弹 UAC。
- 全局 `TreatWarningsAsErrors` 启用。
- 无 shell 执行、无网络请求、无管理员权限、无常驻服务。

#### 已知限制
- 无任何诊断或修复能力（阶段 1 起实现）。
- 无 Windows VM 做故障注入测试（阻塞项）。
- 无 GitHub 远程 CI（本地脚本为主）。
