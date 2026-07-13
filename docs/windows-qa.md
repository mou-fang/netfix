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

### 2026-07-13 阶段 2.1 探针语义修正验证

#### 测试环境
- 机器：本机 Windows 11 (10.0.26100) x64
- .NET SDK：10.0.301
- 网络状态：正常（有代理/无代理两种状态均验证）

#### 单元测试（默认运行，不依赖公网）

```
dotnet test NetMedic.slnx -c Debug
-> 通过 52，失败 0，跳过 8（集成测试）
```

#### 集成测试（NETMEDIC_INTEGRATION_TESTS=1）

```
dotnet test --environment NETMEDIC_INTEGRATION_TESTS=1
-> 通过 60，失败 0，跳过 0
```

#### 健康目标验证

| 目标 | 分类 | 关闭重定向 | 正文验证 | 用途 |
|---|---|---|---|---|
| www.msftconnecttest.com/connecttest.txt | NcsiContentCheck | 是 | "Microsoft Connect Test" | 认证门户检测 |
| www.cloudflare.com/cdn-cgi/trace | IndependentHttps | 否 | "ip=" | 独立 HTTPS 直连/代理验证 |
| connectivitycheck.gstatic.com/generate_204 | GlobalServicePath | 否 | （204 状态码） | 全球路径辅助，失败不判定断网 |

#### WEB-02/03 真实执行证据

| 探针 | use_proxy | request_made | 系统无代理时行为 |
|---|---|---|---|
| WEB-02 | false | true | 直连 HTTPS 请求真实发出 |
| WEB-03（有代理） | true | true | 系统代理 HTTPS 请求真实发出 |
| WEB-03（无代理） | true | false | Skipped + reused_from=WEB-02 |

#### PRX-02 WinHTTP 读取方式

- API：`WinHttpGetDefaultProxyConfiguration`（winhttp.dll）
- 不使用注册表推断
- 证据字段：`proxy_layer=WinHTTP`、`winhttp_access_type`、`winhttp_proxy`

#### TARGET-01 HTTP/HTTPS/端口测试

| 输入 | scheme | port | is_tls | tls_performed |
|---|---|---|---|---|
| www.cloudflare.com（无协议） | https | 443 | true | true |
| http://www.msftconnecttest.com/... | http | 80 | false | false |
| https://www.cloudflare.com:8443 | https | 8443 | true | true |

#### SYS-01 域加入检测方式

- API：`NetGetJoinInformation`（netapi32.dll）
- 不使用环境变量比较
- 证据字段：`join_status`（如 `NetSetupWorkgroupName(WORKGROUP)` 或 `NetSetupDomainName(DOMAIN)`）

#### NET-01 多网卡处理

- 有网关的候选接口列表保留
- 唯一候选 = 主接口（Pass）
- 多个有网关候选 = "多活动接口"（Warning/Medium），不随意选择

#### 测试环境
- 机器：本机 Windows 11 (10.0.26100) x64
- .NET SDK：10.0.301
- 网络状态：正常（有线/Wi-Fi 连接，可访问互联网）

#### 全量探针运行结果

| 探针 ID | 状态 | 耗时 | 需管理员 | 说明 |
|---|---|---|---|---|
| SYS-01 | Pass | <1ms | 否 | 检测到 Windows 版本、非 RDP 会话 |
| NET-01 | Pass | 34ms | 否 | 检测到活动网卡 |
| NET-02 | Pass | <1ms | 否 | 有有效 IPv4 地址 |
| NET-03 | Pass | <1ms | 否 | 有默认网关 |
| DNS-01 | Pass | <1ms | 否 | 检测到 DNS 服务器配置 |
| DNS-02 | Pass | 153ms | 否 | 系统解析健康域名成功 |
| PRX-01 | Pass | <1ms | 否 | 读取 WinINET 代理（当前为关闭） |
| PRX-02 | Pass | <1ms | 否 | WinHTTP 代理为直连 |
| PRX-03 | Pass | <1ms | 否 | PAC 未配置 |
| PRX-04 | Skipped | <1ms | 否 | 无活动代理，跳过端口检测 |
| WEB-01 | Pass | <1ms | 否 | NCSI 辅助信号 |
| WEB-02 | Pass | 189ms | 否 | 直连 HTTPS 成功 |
| WEB-03 | Pass | <1ms | 否 | 系统代理 HTTPS 成功 |
| WEB-04 | Pass | <1ms | 否 | 无认证门户 |
| TARGET-01 | Pass | ~1s | 否 | 有效 URL 被接受 |

总耗时：约 2 秒（14 探针并发，maxConcurrency=2）。
无超时、无异常、无管理员权限需求。

#### 超时验证
- 每个探针有独立超时（3-8 秒），编排器有总预算（30 秒）。
- 测试验证：单探针超时返回 `Error + PROBE_TIMEOUT`，总预算超时返回 `Skipped`，外部取消返回 `Skipped`。

#### 权限要求
- 所有 14 个探针均不需要管理员权限。
- 阶段 0 的 app.manifest `asInvoker` 生效，启动不弹 UAC。

#### URL 安全验证
- 恶意输入（凭据/换行/管道/引号/非 HTTP 协议/localhost）被正确拒绝。
- 有效 URL 被正确规范化为主机名。

#### 未验证的故障场景（需要 Windows VM）

以下场景未在真实故障环境下验证，需在 Windows VM 中通过故障注入测试：

| 场景 | 说明 | 阻塞阶段 |
|---|---|---|
| APIPA 地址 | 需要制造 DHCP 失效获取 169.254 地址 | 阶段 3+ |
| DNS 服务器不可达 | 需要修改 DNS 指向无效地址 | 阶段 3+ |
| 失效本地代理 | 需要设置 127.0.0.1:端口代理后关闭代理程序 | 阶段 4 |
| PAC 不可达 | 需要配置无效 PAC URL | 阶段 4 |
| 认证门户 | 需要连接酒店/校园 Wi-Fi | 阶段 5+ |
| NCSI 不一致 | 需要模拟 NCSI 失败但 HTTPS 正常 | 阶段 3+ |
| VPN 残留路由 | 需要连接后断开 VPN | 阶段 5+ |
| IPv6 黑洞 | 需要 IPv6 不可达但 IPv4 正常的网络 | 阶段 5+ |

**重要**：以上场景未通过真实故障验证。阶段 2 不在当前主力电脑上制造故障。Windows VM 必须在阶段 3 完成前配置。

---

## 阶段 3 测试记录

### GitHub Actions CI 运行记录

GitHub 仓库：`mou-fang/netfix`，CI 已实际触发并多次通过。

| Run ID | 提交 | 结论 | 时间 |
|---|---|---|---|
| 29243264893 | 阶段3.2: 用户结果页与最终契约收尾 | success | 2026-07-13T10:35:07Z |
| 29231480882 | 阶段3.1: 真实探针对齐与用户结论闭环 | success | 2026-07-13T07:15:26Z |
| 29227803373 | 阶段3: 诊断规则和普通用户结论 (代码完成, 真实验证阻塞) | success | 2026-07-13T06:02:06Z |
| 29226994733 | 阶段2.4: 真实测试与遗漏修正 | success | 2026-07-13T05:43:02Z |
| 29226589565 | 阶段2.3: 语义与资源安全收尾 | success | 2026-07-13T05:33:15Z |

### Windows Runner 说明

GitHub Actions `windows-latest` Runner 是 github-hosted 的 **Windows Server**，不是 Windows 11 桌面 VM。它用于构建和运行测试，**不能用于故障注入测试**（无法修改网络配置、制造 DHCP 失效、设置失效代理等）。故障注入需要独立的 Windows 11 VM。

### 默认单元测试计数（无集成测试，不依赖公网）

| TFM | 通过 | 跳过 | 总计 |
|---|---|---|---|
| net10.0 | 144 | 0 | 144 |
| net10.0-windows | 154 | 8 | 162 |

- net10.0：跨平台 Core 逻辑 + 规则引擎 + 模拟场景测试，Linux CI 运行同一组。
- net10.0-windows：增加 Windows 探针相关测试；8 项集成测试默认跳过（需 `NETMEDIC_INTEGRATION_TESTS=1`）。

### 阶段 3 诊断规则验证（模拟环境）

10 个场景 fixture 全部通过，覆盖 10 条规则 + InconclusiveRule 兜底：

| 场景 | 预期规则 | 可信度 | 修复动作 |
|---|---|---|---|
| L01_Healthy | finding.network_healthy | High | null |
| L02_DeadLocalProxy | finding.dead_local_proxy | High | null |
| L09_DnsFailure | finding.dns_failure | High | null |
| L14_NcsiMismatch | finding.ncsi_mismatch | High | null |
| L15_SingleSiteIssue | finding.target_unreachable | High | null |
| L20_WinHttpProxyConfig | finding.winhttp_proxy_config | Medium | null |
| L21_PacUnreachable | finding.pac_unreachable | High | null |
| L22_ApipaDhcp | finding.apipa_dhcp | High | null |
| L23_CaptivePortal | finding.captive_portal | High | null |
| L24_ExternalService | finding.target_unreachable | High | null |

所有 Finding 的 `RecommendedActionId = null`；`ExecutableRepairActions` 为空集。

### 故障注入限制（无真实故障验证）

以下故障场景**未在真实环境下验证**，GitHub Actions Windows Runner 无法进行故障注入：

| 场景 | 说明 | 阻塞阶段 |
|---|---|---|
| 真实代理失效 | 需设置 127.0.0.1:端口代理后关闭代理程序 | 阶段 4 |
| 真实 PAC 不可达 | 需配置无效 PAC URL | 阶段 4 |
| 真实 DNS 服务器不可达 | 需修改 DNS 指向无效地址 | 阶段 3+ |
| 真实 APIPA 地址 | 需制造 DHCP 失效获取 169.254 地址 | 阶段 3+ |
| 真实认证门户 | 需连接酒店/校园 Wi-Fi | 阶段 5+ |
| 真实 NCSI 不一致 | 需模拟 NCSI 失败但 HTTPS 正常 | 阶段 3+ |
| 真实 VPN 残留路由 | 需连接后断开 VPN | 阶段 5+ |
| 真实 IPv6 黑洞 | 需 IPv6 不可达但 IPv4 正常的网络 | 阶段 5+ |

### 阻塞项

- **Windows VM 尚未配置。** 阶段 3 诊断规则仅在模拟环境验证，真实故障注入需 Windows 11 VM。GitHub Actions Windows Runner 不是故障 VM。
- **阶段 4 阻塞。** 修复功能（`IRepairAction` 实现）在 Windows VM 配置前不得开始。
