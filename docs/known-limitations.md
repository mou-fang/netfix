# 已知限制

> 对应任务书 §0.2。记录当前不能检测或不能安全修复的问题。

## 当前能力（阶段 3 完成后）

- **有快速只读探针**：14 个真实 Windows 只读探针（SYS/NET/DNS/PRX/WEB/TARGET），检测网络状态。
- **有诊断规则**：10 条内置规则 + InconclusiveRule 兜底，生成普通用户可读的结论。
- **无真实修复能力**：`ExecutableRepairActions` 集合为空，所有规则 `RecommendedActionId = null`。不显示修复按钮，不执行任何系统修改。阶段 4 起逐个实现真实 `IRepairAction`。

## 当前限制

- **无 Windows VM 做故障注入测试**：诊断规则和探针仅在正常状态和模拟环境验证，未在真实故障环境下测试（阻塞项）。
- **8 项集成测试默认跳过**：依赖公网和真实 Windows 探针的集成测试通过 `NETMEDIC_INTEGRATION_TESTS=1` 环境变量触发，默认 CI 不运行。
- **无历史记录**：尚未实现本地存储（阶段 6）。
- **无报告导出**：尚未实现报告与脱敏（阶段 6）。
- **无深度探针**：无 UAC Worker、无 VPN/路由/IPv6 深度探针（阶段 5）。
- **无 DNS 缓存探针**：无 DNS-03/DNS-04，DnsFailureRule 不区分缓存异常与服务器不可达。
- **无伪成功**：阶段 1 的 RepairResult_FakeSuccess 已删除，修复确认/修复结果页不可达。

## 待验证事项（不得标记为已通过）

以下三项必须保留，直到有实际证据：

- ~~**NetMedic.Core 尚未在真实 Linux 环境运行测试。**~~ **已验证**：GitHub Actions Linux CI（ubuntu-latest）已实际运行并通过，net10.0 144 项测试全绿。
- ~~**GitHub Actions 尚未实际执行。**~~ **已验证**：GitHub 仓库 `mou-fang/netfix` 已创建，Actions 多次成功触发（Run ID: 29243264893 等）。
- **Windows VM 尚未配置。** GitHub Actions Windows Runner 是 github-hosted 的 Windows Server，不是 Windows 11 桌面 VM，不能用于故障注入测试。必须在阶段 4（修复功能）之前解决，否则无法做故障注入测试和快照恢复。

## 后续阶段将持续更新的固有限制（任务书规定的安全边界）

- 不提供 VPN、代理节点或封锁绕过。
- 不自动卸载 VPN 驱动、虚拟网卡、安全软件。
- 不自动执行 Winsock/TCP-IP/全网络重置（只引导）。
- 不修改企业网络、组策略、静态 IP。
- 不登录或修改路由器后台。
- 不抓包、不做 HTTPS 中间人、不安装根证书。
- 不承诺清除病毒。
