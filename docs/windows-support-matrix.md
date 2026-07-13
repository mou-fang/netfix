# Windows 支持矩阵

> 对应任务书 §3.1。

## V1 支持范围

| 系统 | 架构 | 支持级别 | 说明 |
|---|---|---|---|
| Windows 11（当前受支持版本） | x64 | 主支持 | 首发平台 |
| Windows 11 | ARM64 | 后续 | 核心稳定后单独发布 |
| Windows 10 22H2 | x64 | 尽力兼容 | 显示结束常规支持提示 |

## 不支持

- Windows 10 早期版本、Windows 8/7：不支持。
- Android / macOS / Linux：V1 不开发（任务书 §3.1）。

## 实施时核对

Windows 11 各版本支持期会变化，发布前必须重新核对微软生命周期页面：
- https://learn.microsoft.com/en-us/lifecycle/products/windows-11-home-and-pro

不把过期版本写死在代码逻辑中。
