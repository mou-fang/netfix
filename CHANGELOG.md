# Changelog

本项目所有用户可见变化记录在此。格式参考 [Keep a Changelog](https://keepachangelog.com/)。

## [0.3.0] - 2026-07-13

### 阶段 2：快速只读体检

#### 新增
- Core 数据模型重构：`FakeNetworkEnvironment` 改用分组对象（`SystemState`/`AdapterState`/`DnsState`/`ProxyState`/`WebState`），停止无限增加布尔字段（ADR-012）。
- Core 安全工具：`UrlNormalizer`（URL 安全规范化，拒绝凭据/换行/命令字符/异常长度）、`HealthTargetCatalog`（两个独立健康目标）。
- Windows 真实只读探针（14 个）：SYS-01、NET-01~03、DNS-01~02、PRX-01~04（WinINET/WinHTTP/PAC/本地端口分别记录）、WEB-01~04（NCSI/直连HTTPS/系统代理HTTPS/认证门户）、TARGET-01（URL 安全+DNS/TCP/TLS）。
- `WindowsProbeSet` 构建器、`WindowsNetworkEnvironment` 实现。
- `--verify-probes` 命令行验证模式。
- 8 项 Windows 真实探针验证测试 + 20 项 URL 规范化测试。
- UI 增加目标网站输入框。

#### 变更
- 测试项目改为 `net10.0-windows` 以引用 Windows 探针。
- `MainViewModel.StartCheckupAsync` 改用真实 Windows 探针替换 Fake。

#### 安全
- 所有探针只读、非管理员可运行、有独立超时和取消支持。
- NCSI、Ping、单一健康地址均不单独决定"是否断网"。
- WinINET、WinHTTP、PAC 和本地代理端口分别记录。
- TARGET-01 只允许 HTTP/HTTPS，安全规范化用户输入。
- 不上传诊断信息，不添加设备标识。
- 无真实修复、无管理员动作、无系统设置修改。

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
