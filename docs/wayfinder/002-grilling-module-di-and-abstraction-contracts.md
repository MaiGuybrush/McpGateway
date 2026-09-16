# [Grilling] 規劃 McpGateway.Core 模組抽象契約與 DI 擴充規範

- **ID**: `002`
- **Label**: `wayfinder:grilling`
- **Status**: `closed`
- **Assignee**: `Antigravity`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 001 resolved)
- **Resolved**: 2026-09-16

## Question

在 `McpGateway.Core` 中應如何定義子系統模組的標準註冊介面或擴充模式？是定義介面（如 `IMcpModule`）透過反射組裝，還是遵循 ASP.NET Core 慣例提供 `Add<System>Module(IServiceCollection, IConfiguration)` 與 `Map<System>Mcp(WebApplication, string routePrefix)`？各系統的 Options 綁定與 Consul Key 解析如何整合？

---

## Resolution

### 1. 模組註冊與組裝模式（方案 A）
- 採用 **ASP.NET Core 慣例擴充方法（Extension Method）**，各子系統 Class Library 專案（如 `McpGateway.Mfg.Mes`）提供：
  ```csharp
  public static IServiceCollection AddMesSubsystem(
      this IServiceCollection services, 
      IConfiguration configuration);
  ```
- 宿主（`Host/Program.cs`）作為 Composite Root 顯式呼叫 `builder.Services.AddMesSubsystem(...)`，達成**零反射、編譯期型別安全、除錯透明**。

### 2. Core Fluent 註冊 API（選項 1）
- `McpGateway.Core` 提供標準擴充：
  ```csharp
  services.AddMcpSubsystem("mes", subsystem =>
  {
      subsystem.WithTools<MesLotQueryTool>()
               .WithTools<MesDefectReportTool>();
  });
  ```
- 底層自動完成 `AddMcpServer().WithTools(...)` 註冊，並向 `IMcpSubsystemRegistry` 登記白名單，子系統工程師無須處理路徑分流細節。

### 3. 組態與 Consul KV 分層命名規範
- **`appsettings.json`**：
  ```json
  "McpGateway": {
    "Department": "mfg",
    "Systems": {
      "mes": {
        "Downstream": { "BaseUrl": "http://mes-service.corp.local" }
      }
    }
  }
  ```
- **Consul KV**：
  `McpGateway/{department}/systems/{system}/Downstream/...`
- 子系統 Options 預設綁定 `McpGateway:Systems:<system>` 節區。

### 4. 工具命名規範與 Roslyn 檢核
- 工具命名標準升級為**三段式**：`"{dept}_{system}_{action}"`（例如 `mfg_mes_query_lot`）。
- 現有 `McpGateway.Analyzers` 需升級增加診斷規則（如 `MCP003`），於編譯期檢核子系統 Tool 名稱必須包含子系統前綴，防止全域衝突。
