# [Task] 在 McpGateway.Core 實作模組分流中介層 (IMcpSubsystemRegistry)

- **ID**: `006`
- **Label**: `wayfinder:task`
- **Status**: `closed`
- **Assignee**: Antigravity
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 004 resolved)
- **Resolved**: 2026-09-16

## Question

依據 ADR-014 規範，在 `McpGateway.Core` 專案中新增 `IMcpSubsystemRegistry`、實作 `AddMcpSubsystem` Fluent API，並於 `McpGatewayHostExtensions.AddMcpGateway` 的 `ConfigureSessionOptions` 回呼中掛接路徑正則解析與 `mcpOptions.ToolCollection` 動態白名單過濾機制，附帶單元與整合測試驗證。

---

## Resolution

### 1. 核心實作架構

已於 `McpGateway.Core` 新增 `McpGateway.Core.Subsystems` 命名空間並落實以下關鍵元件：
1. **[`McpSubsystemRegistration`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/McpSubsystemRegistration.cs)**：
   - 管理子系統名稱、關聯 Tool Types 以及允許的工具名稱白名單集合。
   - 自動自工具類別與公開方法中透過反射提取標註之 `McpToolAttribute` 工具名稱。
2. **[`IMcpSubsystemRegistry`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/IMcpSubsystemRegistry.cs)** 與 **[`McpSubsystemRegistry`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/McpSubsystemRegistry.cs)**：
   - 執行緒安全之子系統登錄中心（`ConcurrentDictionary`）。
   - `FilterToolsForSubsystem(string subsystemName, McpServerPrimitiveCollection<McpServerTool> allTools)`：
     - 若子系統未登錄，回傳空集合達成 100% 嚴格隔離。
     - 若已登錄，依據白名單以及三段式命名規範（`{dept}_{system}_{action}` 或 `{system}_{action}`）動態挑選授權工具建立新的 `McpServerPrimitiveCollection`。
3. **[`McpSubsystemBuilder`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/McpSubsystemBuilder.cs)** 與 **[`McpSubsystemServiceCollectionExtensions`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/McpSubsystemServiceCollectionExtensions.cs)**：
   - 提供 `services.AddMcpSubsystem("mes", subsystem => { subsystem.WithTools<MesTool>(); })` Fluent API。
   - 同步向底層 MCP Server (`AddMcpServer().WithTools(...)`) 登記型別，並於 DI 容器中注入 `IMcpSubsystemRegistry` 單例。
   - 支援 `WithTools<T>()`、`WithTools(Type)`、`WithToolsFromAssembly(Assembly)`、`WithToolNames(...)` 等豐富擴充方法。
4. **[`SubsystemRouteParser`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Subsystems/SubsystemRouteParser.cs)**：
   - 解析請求內容，優先讀取 ASP.NET Core RouteValues（`system`），並輔以路徑分割與正則表達式解析 `/{dept}/{system}/mcp` 或 `/{system}/mcp`。
   - 自動辨識非子系統端點（如 `/{dept}/mcp` 部門根路徑與 `/health/*`）。
5. **[`McpGatewayHostExtensions`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Hosting/McpGatewayHostExtensions.cs)**：
   - 在 `AddMcpGateway()` 的 `WithHttpTransport(httpOptions => ...)` 中配置 `httpOptions.ConfigureSessionOptions`，在每個 Stateless 請求進入時動態執行子系統解析與 `mcpOptions.ToolCollection` 替換。
   - 在 `MapMcpGateway()` 自動掛載標準子系統端點：`/{dept}/{system}/mcp`。

### 2. 測試驗證成果

於 `tests/McpGateway.Core.IntegrationTests/Subsystems/` 實作以下完整測試套件：
- **[`SubsystemRouteParserTests.cs`](file:///D:/Projects/.NET/McpGateway.Core/tests/McpGateway.Core.IntegrationTests/Subsystems/SubsystemRouteParserTests.cs)**：驗證路徑正則、RouteValues、大小寫無關與非子系統路徑判定（8 項測試通過）。
- **[`McpSubsystemRegistryTests.cs`](file:///D:/Projects/.NET/McpGateway.Core/tests/McpGateway.Core.IntegrationTests/Subsystems/McpSubsystemRegistryTests.cs)**：驗證屬性提取、命名規範比對、集合過濾與 DI 整合（12 項測試通過）。
- **[`McpSubsystemIntegrationTests.cs`](file:///D:/Projects/.NET/McpGateway.Core/tests/McpGateway.Core.IntegrationTests/Subsystems/McpSubsystemIntegrationTests.cs)**：啟動 `TestServer`，端到端驗證端點連線與 SessionOptions 動態白名單隔離（2 項測試通過）。
- **全方案測試**：`dotnet test` 執行 40 項測試全部通過（Core.IntegrationTests: 30 passed, Analyzers.Tests: 10 passed）。
