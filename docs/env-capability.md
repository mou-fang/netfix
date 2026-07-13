# NetMedic 环境能力报告

> 更新日期：2026-07-13

## 开发环境

| 工具 | 版本 | 状态 |
|---|---|---|
| 操作系统 | Windows 11 (10.0.26100) x64 | 可用 |
| .NET SDK | 10.0.301 | 已安装 |
| .NET 运行时 | WindowsDesktop 10.0.9 / NETCore 8.0.19 | 已安装 |
| WPF | .NET 10 内置 | 可用 |
| VS 2022 Build Tools | 17.x（含 C++ + Win11 SDK） | 已安装（非本阶段必需，后续可选） |
| Git | 可用 | 已初始化仓库 |

## 验证能力

### 可在本机完成
- `dotnet build` 构建三个项目（Core / App / Tests）。
- `dotnet test` 运行 Core 冒烟测试。
- `dotnet format` 代码格式检查。
- WPF 空壳 `dotnet run` 启动验证（本机 Windows 11）。

### 阻塞项

| 项 | 说明 | 影响 |
|---|---|---|
| Windows VM | 无隔离的 Windows VM | 阶段 2+ 的故障注入测试（改网络后恢复快照）无法执行 |
| Windows CI Runner | 无远程 CI | 只能本地验证；连 GitHub 后 GH Actions 生效 |
| Linux 环境 | 当前仅 Windows 开发机 | Core 测试跨平台性由 net10.0 TFM 保证，但未在真实 Linux 验证 |

## 关于"Linux 上 Windows-targeting 编译不算 Windows 实测"

任务书 §0 第 10 条和 §16 明确：Linux 可以编译 Core 和运行 Core 测试，但 WPF、Windows API、UAC 和真实网络修复必须在 Windows 验证。当前阶段 0 在本机 Windows 11 验证构建和启动，符合要求。后续阶段涉及真实网络修改时，必须使用 Windows VM。
