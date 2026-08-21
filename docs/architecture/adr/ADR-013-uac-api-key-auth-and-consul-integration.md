# ADR-013: 導入企業集中式 UAC API Key 認證、Consul K/V 動態路由容錯與啟動自我驗證機制

## 狀態
**✅ 已採用 (Accepted)**（2026-08-20）

---

## 背景 (Context)

在企業各部門部署之 MCP Gateway（如 `McpGateway.Report`、`McpGateway.Equipment` 等）中，AI Agent（如 `cim-agent`）與下游系統呼叫 MCP Tools 時需要嚴謹且標準化的身分驗證。

`McpGateway.Core` 既有實作存在以下問題：
1. **驗證協定過時**：Core 原有的 `ApiKeyValidator` 採用自訂 POST `/validate` 協定，與企業內部由 **UAC API**（User Access Control API）統一管理的 Personal/Service API Key 機制不相容。
2. **端點位址未納入全域 Consul 治理**：企業各系統服務 URL 統一維護於 Consul K/V 鍵值 `ApiUrls.ProductionOa`（包含 `UacApi`, `AppManagementApi`, `RemoteWolApi` 等），若以靜態組態指定 UAC API 位址，將無法享有集中治理與動態變更優勢。
3. **缺乏節點高可用容錯 (HA Failover)**：企業 UAC API 叢集部署於多台主機（如 `tnvcimweb1` 與 `tnvcimweb2`），閘道需要具備自動重試與容錯能力。
4. **未防範啟動期未註冊風險**：若 Gateway 未於 UAC 註冊對應的系統名稱（如 `mcp-report`），將導致運行期間所有呼叫均報錯；需於啟動期進行自我診斷阻斷上線。

---

## 決策 (Decision)

我們決定在 `McpGateway.Core` 重構並升級標準 **UAC API Key 認證架構**：

### 1. 企業集中式 Consul K/V `ApiUrls.ProductionOa` 整合
- 透過 Consul K/V 讀取 `ApiUrls.ProductionOa`，解析其 JSON 物件中的 `UacApi` 節點清單（字串陣列）。
- 支援多節點容錯輪詢（Failover Loop）：遇 5xx 或逾時時自動嘗試下一節點；遇 401/403 則立即返回未授權。
- 提供 `FallbackUacApiUrls`（如 `http://hp08239p.cminl.oa/uacapitest`）支援本地與離線開發測試。

### 2. 啟動階段系統合法性自我檢查 (Startup Validation)
- 於應用程式啟動時呼叫 UAC API `GET /api-keys/systems`。
- 確認合法系統清單中包含指定之系統名稱（例如 `mcp-report`），若不存在或 UAC 叢集皆無法連線則拋出異常阻斷啟動。

### 3. 雙重 Header 解析與 SHA256 雜湊快取
- 中介軟體（`ApiKeyAuthenticationMiddleware`）優先讀取 `X-Api-Key`，若未提供則 Fallback 讀取 `Authorization: Bearer <key>`。
- 採用 SHA256 運算雜湊值作為快取鍵（`apikey:{HEX_SHA256}`），預設快取 30 分鐘，保護 UAC API 負載並防止明文暫存。

### 4. 雙軌身分上下文注入
- UAC API 驗證成功回傳 `{ "empId": "...", "name": "..." }` 後，建立 `ToolContext(userId: empId, department: options.Department, role: name, ...)` 注入 `HttpContext.Items["ToolContext"]`。
- 同步建構 `ClaimsPrincipal`（以 `EmpId` 為 `NameIdentifier`），提供 AuditLogger 完整審計能力。

### 5. 可觀測性與健康檢查
- 實作 `UacApiHealthCheck` 掛載於 Gateway Readiness 探針。
- 支援 `Auth:Enabled: false` 組態開關便於單機除錯。

---

## 影響與後果 (Consequences)

### 正面影響 (Positive)
1. **企業安全標準合規**：全面對接企業現行 UAC API Key 機制，簡化金鑰簽發、授權與生命週期管理。
2. **高可用性與自愈能力**：Consul 動態端點搭配多節點自動容錯，杜絕單點故障。
3. **極致效能與安全**：SHA256 雜湊快取降低驗證延遲至毫秒級，且無明文洩漏風險。
4. **防禦性啟動**：啟動期即時校驗 UAC 系統名稱，避免帶病上線。

### 負面影響 / 代價 (Trade-offs & Mitigation)
1. **依賴 UAC API 叢集健康**：若 UAC API 全數中斷且快取未命中，將導致驗證失敗。
   * *緩解*：Consul 多節點容錯 + 本機記憶體快取 + 降級 503 提示。
