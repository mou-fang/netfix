# NetMedic 任务跟踪

> 任务书：网络急救箱：轻量全能版 Agent AI 开发总任务书 v2.0

## 阶段总览

| 阶段 | 名称 | 状态 |
|---|---|---|
| 0 | 极简工程骨架 | 已完成 |
| 1 | 模型、模拟器与完整 UI 假流程 | 未开始 |
| 2 | 快速只读体检 | 未开始 |
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

- Windows VM / CI Runner：无。阶段 2+ 的故障注入测试需要，当前为阻塞项。
- Linux 真实环境验证：当前在 Windows 开发，Linux Core 测试可运行性由 TFM 保证，待连 GitHub 后由 GH Actions Linux Job 验证。
