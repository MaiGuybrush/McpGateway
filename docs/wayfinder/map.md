# Map: 部門內多系統模組化與端點分流架構 (Intra-Department Modular Gateway)

- **Label**: `wayfinder:map`
- **Created**: 2026-09-16

## Destination

完成《ADR-014：部門內多系統模組化與端點分流架構標準》，並升級 `templates/scaffold.ps1` 與專案範本樹，支援產出 Modular Monorepo 結構（薄宿主 Host + 增量式子系統 Class Library）以及依子系統分流的 MCP 端點（`/{dept}/{system}/mcp`）。

## Notes

- **Domain**: .NET 9, ASP.NET Core, ModelContextProtocol.AspNetCore (1.4.1), Modular Monorepo Architecture, Enterprise AI Gateway.
- **Skills to consult**: `/grilling`, `/domain-modeling`, `/research`.
- **Standing preferences**:
  - CLI 工作流程：增量式擴充（先 scaffold 薄宿主，再透過 add-module 增量子系統專案）。
  - 路由規範：`/{dept}/{system}/mcp`（格式 1，部門層級統一 Ingress，內部路徑分流）。
  - 組態與 Consul：分層命名空間隔離（`McpGateway:{dept}:{system}:...`）。
  - 維護邊界：每個子系統獨立 Class Library，支援 CODEOWNERS 獨立審查。

## Decisions so far

<!-- the index — one line per closed ticket: enough to judge relevance, then zoom the link for the detail the ticket holds -->

- [[Research] 調研 ModelContextProtocol.AspNetCore 多端點工具隔離機制](001-research-mcp-aspnetcore-multi-endpoint-isolation.md) — 官方 SDK 無法原生多 Server，改採在 Stateless 模式下利用 `ConfigureSessionOptions` 依路徑動態過濾 `ToolCollection`，透過 `IMcpSubsystemRegistry` 達成 100% 嚴格工具隔離。
- [[Grilling] 規劃 McpGateway.Core 模組抽象契約與 DI 擴充規範](002-grilling-module-di-and-abstraction-contracts.md) — 採用 ASP.NET Core 慣用擴充方法模式（方案 A），Core 提供 Fluent API `AddMcpSubsystem` 封裝，組態與 Consul 採分層命名空間，工具採三段式命名規範並由 Roslyn 靜態檢核。
- [[Grilling] 規劃增量式子系統 Scaffold 指令與目錄結構規範](003-grilling-scaffold-cli-and-project-tree.md) — 提供獨立腳本 `templates/add-module.ps1`，採 `src/McpGateway.<Dept>.Host` + `src/McpGateway.<Dept>.<System>` 結構，並於執行時自動加入 `.sln`、專案參考與 `Program.cs` 錨點註冊。
- [[Task] 撰寫 ADR-014：部門內多系統模組化與端點分流標準草案](004-task-draft-adr-014.md) — 完成 [ADR-014: 部門內部多系統模組化與端點分流架構](../architecture/adr/ADR-014-intra-department-subsystem-modules.md)，確立 Monorepo、請求級別動態過濾、三段式命名與增量腳手架之正式規範。
- [[Task] 擴充 McpGateway.Analyzers 增加子系統工具三段式命名規則 (MCP003)](005-task-roslyn-subsystem-naming-rule.md) — 實作 Roslyn 診斷規則 MCP003 與 CodeFixProvider，強制檢核子系統 MCP Tool 之 {department}_{system}_{action} 三段式命名格式與前綴對齊，並完成 10 項單元與整合測試。
- [[Task] 在 McpGateway.Core 實作模組分流中介層 (IMcpSubsystemRegistry)](006-task-implement-core-subsystem-registry.md) — 實作 IMcpSubsystemRegistry 與 AddMcpSubsystem Fluent API，於 ConfigureSessionOptions 掛接路徑正則解析與請求級 ToolCollection 動態白名單過濾，達成 100% 協議層子系統工具隔離，附帶 22 項單元與整合測試。
- [[Task] 重構 templates/ 範本樹為 Modular Monorepo 結構](007-task-refactor-template-tree-for-monorepo.md) — 依據 ADR-014 重構模板樹為 `template-host/`（薄宿主 + 錨點 + 分層組態）與 `template-module/`（子系統模組 + 三段式命名 + AddSubsystem 擴充），並升級 `scaffold.ps1` 預設產出 Monorepo 方案與 Host。
- [[Task] 實作 templates/add-module.ps1 增量腳本與 Auto-wiring](008-task-implement-add-module-script.md) — 實作 `add-module.ps1` 增量子系統產生器，支援參數校驗、三段式命名轉換、子系統 Class Library 產生，並透過 `dotnet sln add`、`dotnet add reference`、`Program.cs` 錨點注入與 `appsettings.json` 自動完成端到端裝配。

## Not yet specified

- **子系統單元與整合測試範本**：增量產生子系統時，是否連同產生對應的 `tests/McpGateway.<Dept>.<System>.Tests` 測試專案與測試 Mock。
- **CODEOWNERS 與權限分離實作**：在 Git Repository 中依據 `src/McpGateway.<Dept>.<System>/` 路徑自動產生或更新 CODEOWNERS 配置。

## Out of scope

- 動態反射載入 DLL（Plugin Scanning / MEF 外掛機制）—— 已確認採用編譯期強隔離之 Class Library Monorepo，不支援無重啟熱插拔 DLL。
- 跨子系統分散式快取共用（Data Mesh）—— 不屬於部門內系統邊界模組化之範疇。
