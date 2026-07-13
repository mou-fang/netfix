# Changelog

本项目所有用户可见变化记录在此。格式参考 [Keep a Changelog](https://keepachangelog.com/)。

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
