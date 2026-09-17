# [Task] 實作 templates/add-module.ps1 增量腳本與 Auto-wiring

- **ID**: `008`
- **Label**: `wayfinder:task`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 007 resolved)
- **Resolved**: 2026-09-17

## Question

實作 `templates/add-module.ps1` PowerShell 腳本，提供 `-Department`, `-System`, `-ToolName`, `-OutDir` 參數，自動由 `template-module/` 複製長出子專案，並透過 `dotnet sln add`、`dotnet add reference` 與錨點注入完成端到端自動裝配。

---

## Resolution

已在 [`templates/add-module.ps1`](file:///D:/Projects/.NET/McpGateway.Core/templates/add-module.ps1) 實作完成增量式子系統模組產生腳本與端到端 Auto-Wiring 自動裝配機制：

1. **參數介面與嚴格輸入驗證**：
   - 支援 `-Department`, `-System`, `-ToolName`, `-OutDir`, `-DryRun`, `-Force` 參數。
   - 包含部門與子系統名稱長度（2-20 字元）、英數字正則驗證與系統保留字防護。
   - 工具名稱自動解析：可接受 `{action}`、`{system}_{action}` 或 `{department}_{system}_{action}` 格式，自動標準化為符合 Roslyn MCP003 規則之 `{department}_{system}_{action}` 三段式全名與 PascalCase 工具類別名（如 `QueryLotTool`）。

2. **智慧方案與宿主專案定位**：
   - 支援由方案根目錄、宿主目錄或任意父層目錄（指定 `-OutDir`）執行，自動尋找對應之 `src/McpGateway.<Dept>.Host` 專案與 `.sln` 方案檔。
   - 動態解析 Host 專案所使用之 `McpGateway.Core` 版本，確保子系統與宿主版本 100% 一致。

3. **四階段 Auto-Wiring 自動裝配**：
   - **方案註冊**：呼叫 `dotnet sln add` 自動將子系統 Class Library 專案納入方案。
   - **專案參考**：呼叫 `dotnet add reference` 自動讓 Host 專案引用子系統 Class Library。
   - **進入點注入**：在 `Host/Program.cs` 頂部自動注入子系統命名空間 `using McpGateway.<Dept>.<System>;`，並於 `// __SUBSYSTEM_REGISTRATION__` 錨點處插入 `builder.Services.Add<System>Subsystem(builder.Configuration);`，同時保留錨點以利後續子系統持續追加。
   - **組態區段註冊**：自動檢查 `Host/appsettings.json`，若尚無該子系統組態則自動注入 `McpGateway:Systems:<system>` 之下游預設設定。

4. **端到端驗證**：
   - 模擬建立測試宿主並連續增量擴充 `mes` 與 `wms` 兩個子系統。
   - 驗證 `Program.cs`、`appsettings.json`、`.sln` 與專案參考皆正確裝配。
   - 執行 `dotnet restore` 與 `dotnet build`，結果為 0 警告、0 錯誤，完整通過所有 MCP Roslyn 分析器檢核。
