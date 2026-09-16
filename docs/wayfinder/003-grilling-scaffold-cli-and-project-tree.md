# [Grilling] 規劃增量式子系統 Scaffold 指令與目錄結構規範

- **ID**: `003`
- **Label**: `wayfinder:grilling`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None
- **Resolved**: 2026-09-16

## Question

增量式模組產生器（是獨立腳本 `templates/add-module.ps1` 還是 `scaffold.ps1 -AddModule`）的參數介面應如何定義？專案目錄結構如何從現有的單一專案平鋪結構演進為 `src/McpGateway.<Dept>.Host` + `src/McpGateway.<Dept>.<System>`？腳本執行時如何自動向 `.sln` 加入專案並向 Host 加入專案參考（ProjectRef）？

---

## Resolution

### 1. 指令與介面規範（選項 A：獨立增量腳本）
- **獨立腳本**：建立 `templates/add-module.ps1`，專門用於在既有部門 Gateway 專案中增量新增子系統。
- **參數介面**：
  ```powershell
  pwsh .\add-module.ps1 -Department mfg -System mes -ToolName mes_query_lot [-OutDir <path>] [-Force]
  ```
- **輸入檢核**：
  - `Department`：必須符合英數字命名規範，且對應的 `McpGateway.<Dept>.Host` 專案必須存在。
  - `System`：子系統代號（如 `mes`, `wms`, `eap`）。
  - `ToolName`：必須以 `<system>_` 開頭（如 `mes_query_lot`）。

### 2. 目錄樹與專案結構規範
統一採用標準 Modular Monorepo 結構：
```text
McpGateway.<Dept>/
├── src/
│   ├── McpGateway.<Dept>.Host/                  # Web App 進入點 (Thin Host)
│   │   ├── Program.cs                           # 聚合各系統之 Composite Root
│   │   ├── appsettings.json                     # 包含 Systems 節區組態
│   │   └── McpGateway.<Dept>.Host.csproj
│   │
│   └── McpGateway.<Dept>.<System>/              # 子系統 Class Library (netstandard / net9.0)
│       ├── Configuration/
│       │   └── <System>Options.cs
│       ├── Services/
│       │   └── I<System>Service.cs
│       ├── Tools/
│       │   └── <System>Tool.cs                  # 實作 MCP Tools (三段式命名)
│       ├── <System>ModuleExtensions.cs          # 暴露 Add<System>Subsystem 擴充方法
│       └── McpGateway.<Dept>.<System>.csproj
└── McpGateway.<Dept>.sln
```

### 3. 自動裝配與管線連結機制 (Auto-Wiring)
執行 `add-module.ps1` 時自動執行以下三階段裝配：
1. **方案註冊**：呼叫 `dotnet sln add "src/McpGateway.<Dept>.<System>/McpGateway.<Dept>.<System>.csproj"`。
2. **專案參考**：在 `Host` 專案執行 `dotnet add "src/McpGateway.<Dept>.Host/McpGateway.<Dept>.Host.csproj" reference "src/McpGateway.<Dept>.<System>/McpGateway.<Dept>.<System>.csproj"`。
3. **進入點注入**：在 `Host/Program.cs` 預留的 `// __SUBSYSTEM_REGISTRATION__` 錨點自動插入 `builder.Services.Add<System>Subsystem(builder.Configuration);`。
