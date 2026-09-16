# [Task] 實作 templates/add-module.ps1 增量腳本與 Auto-wiring

- **ID**: `008`
- **Label**: `wayfinder:task`
- **Status**: `open`
- **Assignee**: Unassigned
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**:
  - [重構 templates/ 範本樹為 Modular Monorepo 結構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/007-task-refactor-template-tree-for-monorepo.md)

## Question

實作 `templates/add-module.ps1` PowerShell 腳本，提供 `-Department`, `-System`, `-ToolName`, `-OutDir` 參數，自動由 `template-module/` 複製長出子專案，並透過 `dotnet sln add`、`dotnet add reference` 與錨點注入完成端到端自動裝配。
