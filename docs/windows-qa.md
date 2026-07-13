# Windows 实机/VM 测试记录

> 对应任务书 §0.2 和 §11.3。记录每次 Windows 实测的证据。

## 测试环境

| 项 | 值 |
|---|---|
| 机器 | 本机 Windows 11 (10.0.26100) x64 |
| .NET SDK | 10.0.301 |
| Windows VM | 无（阻塞项） |

## 阶段 0 测试记录

### 2026-07-13 阶段 0 构建+启动验证

| 项目 | 命令 | 结果 |
|---|---|---|
| 全量构建 | `dotnet build NetMedic.slnx -c Debug` | 通过，0 警告 0 错误 |
| 单元测试 | `dotnet test NetMedic.slnx -c Debug` | 3/3 通过 |
| 格式检查 | `dotnet format NetMedic.slnx --verify-no-changes` | 通过，无格式问题 |
| WPF 启动 | `dotnet run --project src/NetMedic.App` | 通过，窗口正常显示 |

### 阻塞项

- 无 Windows VM，无法执行任务书 §11.3 要求的故障注入测试（阶段 2+ 需要）。
- 无 Windows CI Runner，当前仅本机手动验证。
