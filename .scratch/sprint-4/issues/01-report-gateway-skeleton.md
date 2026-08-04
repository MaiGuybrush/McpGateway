# 01 — 建立 Report 部門 Gateway 骨架

**What to build:** `McpGateway.Report` 專案骨架，引用 `McpGateway.Core` NuGet package，空 `Program.cs` 註冊 Core，編譯通過並可本地啟動。

**Blocked by:** None — 可立即開始

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `src/McpGateway.Report/McpGateway.Report.csproj` 建立，`<TargetFramework>net9.0</TargetFramework>`
- [ ] 引用 `McpGateway.Core` package（從本地 artifacts 或內部 NuGet feed）
- [ ] `Program.cs` 呼叫 `AddMcpGateway` + `RunMcpGatewayAsync`，department name = "report"
- [ ] `dotnet build` 成功
- [ ] `dotnet run` 可啟動，log 顯示 "McpGateway.Report starting"
- [ ] 設定檔範本（`appsettings.json`）含必要欄位：`Department`, `JwksUrl`, `RedisConnectionString`
