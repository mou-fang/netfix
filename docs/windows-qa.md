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
