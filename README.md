# 网络急救箱 NetMedic

轻量、全能的 Windows 网络急救工具。先诊断、后修复、修复后验证、必要时回滚。

## 当前状态

**阶段 0（极简工程骨架）已完成。** 尚无诊断或修复能力，仅工程骨架就绪。

## 技术栈

- C# / .NET 10 LTS / WPF
- CommunityToolkit.Mvvm（唯一生产 MVVM 依赖）
- xUnit（测试）
- JSON 文件存储（无数据库）

## 项目结构

```
NetMedic.slnx
├─ src/NetMedic.Core/     # 跨平台纯逻辑（net10.0）
├─ src/NetMedic.App/      # WPF 应用（net10.0-windows）
└─ tests/NetMedic.Tests/  # xUnit 测试（net10.0）
```

## 构建

```bash
dotnet build NetMedic.slnx -c Debug
```

## 运行

```bash
dotnet run --project src/NetMedic.App
```

## 测试

```bash
dotnet test NetMedic.slnx
```

## 本地 CI

```bash
bash scripts/ci.sh
```

## 格式检查

```bash
dotnet format NetMedic.slnx --verify-no-changes
```

## 文档

- [任务跟踪](TASKS.md)
- [技术决策](DECISIONS.md)
- [变更记录](CHANGELOG.md)
- [环境能力](docs/env-capability.md)
- [威胁模型](docs/threat-model.md)
- [隐私清单](docs/privacy-data-inventory.md)
- [支持矩阵](docs/windows-support-matrix.md)
- [已知限制](docs/known-limitations.md)
- [Windows QA](docs/windows-qa.md)
- [阶段 0 设计](docs/superpowers/specs/2026-07-13-phase-0-startup-design.md)

## 阶段路线

| 阶段 | 内容 |
|---|---|
| 0 | 极简工程骨架 ✅ |
| 1 | 模型、模拟器与完整 UI 假流程 |
| 2 | 快速只读体检 |
| 3 | 诊断规则和普通用户结论 |
| 4 | 低风险修复与同 EXE 提权 |
| 5 | 中风险修复与深度体检 |
| 6 | 应用级检查、报告与历史 |
| 7 | 轻量发布与公开测试 |

详见 [TASKS.md](TASKS.md)。
