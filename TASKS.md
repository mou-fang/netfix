# NetMedic 任务跟踪

> 任务书：网络急救箱：轻量全能版 Agent AI 开发总任务书 v2.0

## 阶段总览

| 阶段 | 名称 | 状态 |
|---|---|---|
| 0 | 极简工程骨架 | 已完成 |
| 1 | 模型、模拟器与完整 UI 假流程 | 已完成 |
| 2 | 快速只读体检 | 已完成 |
| 3 | 诊断规则和普通用户结论 | 未开始 |
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
| Core 测试能在 Linux 运行 | TFM net10.0 保证；本机 Windows 已运行 3/3 通过 |
| WPF App 在 Windows 构建 | 通过 |
| 无 Node/Rust/Tauri/数据库/服务依赖 | 通过（仅 CommunityToolkit.Mvvm + xUnit） |
| 生产 NuGet 依赖不超范围 | 仅 CommunityToolkit.Mvvm 8.4.2 |

### 阻塞项

- **NetMedic.Core 尚未在真实 Linux 环境运行测试。** `net10.0` 目标框架不能代替实际验证。
- **GitHub Actions 尚未实际执行。** 只有 ready-to-enable 配置，从未被真实 CI 触发。
- **Windows VM 尚未配置。** 必须在阶段 2 真实 Windows 探针和阶段 4 修复功能前解决。

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
