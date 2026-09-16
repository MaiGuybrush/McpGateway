# [Task] 撰寫 ADR-014：部門內多系統模組化與端點分流標準草案

- **ID**: `004`
- **Label**: `wayfinder:task`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None
- **Resolved**: 2026-09-16

## Question

將 Ticket 001, 002, 003 取得的結論與決策，綜合撰寫為正式架構決策紀錄 `docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md`，涵蓋 Monorepo 結構、路由規範、DI 契約與 Consul 隔離標準，完成架構團隊簽核標準之草案。

---

## Resolution

正式架構決策紀錄已完成撰寫並存放於：
[ADR-014: 部門內部多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/architecture/adr/ADR-014-intra-department-subsystem-modules.md)

### 決策重點摘要
1. **專案拓撲**：Modular Monorepo，劃分為 `src/McpGateway.<Dept>.Host/` 與各系統獨立 `src/McpGateway.<Dept>.<System>/` Class Library。
2. **端點與工具隔離**：路由標準為 `/{dept}/{system}/mcp`，利用 Stateless 模式下之 `ConfigureSessionOptions` 與 `IMcpSubsystemRegistry` 於請求級別動態過濾 `ToolCollection`。
3. **模組 DI 契約**：採用 ASP.NET Core 慣用擴充方法，Core 提供 `services.AddMcpSubsystem("system", ...)` Fluent API 簡化註冊。
4. **組態標準**：Consul 與 `appsettings.json` 採 `McpGateway:Systems:<system>` 分層命名隔離。
5. **命名與靜態檢核**：三段式命名 `{dept}_{system}_{action}`，由 `McpGateway.Analyzers`（MCP003）實施編譯期強制檢核。
6. **增量腳手架**：規範 `templates/add-module.ps1` 命令列介面與三階段自動裝配流程（.sln, reference, Program.cs）。
