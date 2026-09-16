# [Task] 擴充 McpGateway.Analyzers 增加子系統工具三段式命名規則 (MCP003)

- **ID**: `005`
- **Label**: `wayfinder:task`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 004 resolved)
- **Resolved**: 2026-09-16

## Question

在 `McpGateway.Analyzers` 專案中新增 Roslyn 靜態分析診斷規則（MCP003），檢核位於子系統專案或使用 `AddMcpSubsystem` 註冊的 MCP Tool 方法其屬性名稱必須嚴格符合 `"{department}_{system}_{action}"` 三段式命名規範，若違規則於編譯期發出 Warning/Error。

---

## Resolution

已在 [`src/McpGateway.Analyzers`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Analyzers) 專案中實作完成 Roslyn 靜態分析診斷規則 `MCP003`、快速修復提供者與單元測試套件：

1. **診斷分析器 [`McpSubsystemToolNamingAnalyzer.cs`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Analyzers/McpSubsystemToolNamingAnalyzer.cs)**：
   - 規則代碼：`MCP003`（預設層級：`Warning`，可設定為 `Error`）。
   - **子系統邊界識別機制**：自動解析組件名稱（`McpGateway.<Dept>.<System>`）、命名空間（`McpGateway.<Dept>.<System>...`）、MSBuild 屬性（`McpDepartment`, `McpSubsystem`）或 `[McpSubsystem]` 特性標註，自動排除 `Host`, `Core`, `Tests`, `Shared` 等非子系統專案。
   - **工具方法命名檢核**：
     - 檢查 `[McpServerTool]` / `[McpTool]` 之 `Name` 屬性或建構子參數。
     - 必須嚴格符合小寫蛇形命名規範（`^[a-z0-9_]+$`）。
     - 必須包含至少三段（`{department}_{system}_{action}`）。
     - 當所處子系統已知時，前兩段必須嚴格吻合 `{department}_{system}_` 前綴。
     - 若未明確宣告名稱（使用預設方法名）或以底線結尾，亦於編譯期立即診斷攔截。
   - **`AddMcpSubsystem` 註冊呼叫檢核**：支援語法節點分析，當在宿主端呼叫 `services.AddMcpSubsystem("sys", s => s.WithTools<TTool>())` 時，同步檢驗被註冊之 `TTool` 內部所有 MCP Tool 是否吻合該子系統命名規範。

2. **快速修復 [`McpSubsystemToolNamingCodeFixProvider.cs`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Analyzers/McpSubsystemToolNamingCodeFixProvider.cs)**：
   - 支援 IDE 一鍵修復（Quick Fix）。
   - 當字面常數不符規範時，依據目前專案之部門與子系統自動補齊或校正前綴（例如將 `"mes_query_lot"` 自動重構為 `"mfg_mes_query_lot"`）。
   - 當遺漏 `Name` 命名時，自動插入 `Name = "mfg_mes_xxx"` 具名引數。

3. **單元測試套件 [`tests/McpGateway.Analyzers.Tests`](file:///D:/Projects/.NET/McpGateway.Core/tests/McpGateway.Analyzers.Tests/McpSubsystemToolNamingAnalyzerTests.cs)**：
   - 涵蓋 10 項全面測試案例（合法命名、建構子參數、非子系統略過、遺漏部門前綴、子系統名稱不匹配、不足三段、大寫字母、未宣告名稱、`AddMcpSubsystem` 語法樹檢驗、CodeFix 自動修復驗證）。
   - 全數通過測試（10/10 Passed），並已將專案納入 [`McpGateway.Core.sln`](file:///D:/Projects/.NET/McpGateway.Core/McpGateway.Core.sln)。
