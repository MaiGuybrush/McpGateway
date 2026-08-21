# 交接文件：McpGateway.Core 導入 UAC API Key 認證與 Consul 動態路由 (ADR-013)

**建立時間**：2026-08-20  
**目標專案**：`McpGateway.Core`（通用核心庫）  
**對應業務專案**：`McpGateway.Report`（製造報表 Gateway）  
**關聯架構決策**：`docs/architecture/adr/ADR-013-uac-api-key-auth-and-consul-integration.md`  
**參考實作**：`d:\projects\ECimApi/Helpers/ApiKeyAuthService.cs`  
**測試用 UAC 服務**：`http://hp08239p.cminl.oa/uacapitest/` (Swagger: `http://hp08239p.cminl.oa/uacapitest/api-docs/v1/swagger.json`)

---

## 1. 任務背景與目標 (Background & Objective)

目前 `McpGateway.Core` 既有的 `ApiKeyValidator` 與 `ApiKeyAuthenticationMiddleware` 是基於舊版自訂 POST `/validate` 協定。為符合企業安全規範，需將核心驗證升級為 **UAC API**（Personal/Service API Key）集中認證機制，並與企業集中式 **Consul K/V**（`ApiUrls.ProductionOa`）整合動態端點發現與容錯。

---

## 2. 核心架構決策要點 (Key Architectural Decisions)

1. **認證協定**：
   - 呼叫 UAC API `GET /api-keys/identity?system={system}`，透過 Header `X-Api-Key: {key}` 傳入金鑰。
   - 回傳 `200 OK` 格式為 `{ "empId": "string", "name": "string" }`。
   - 遇 `401/403` 判定未授權，直接返回 `401 Unauthorized`，不重試其他節點。
2. **端點發現與容錯 (Consul K/V & HA)**：
   - 透過 Consul K/V 讀取 `ApiUrls.ProductionOa`（或依環境組態），解析 JSON 物件中的 `UacApi` 節點清單（例如 `["http://tnvcimweb1.../Apigateway/UacApi", "http://tnvcimweb2.../Apigateway/UacApi"]`）。
   - 調用失敗（5xx / 逾時）自動輪詢下一個 UAC API 節點；支援本機 `FallbackUacApiUrls`。
3. **啟動合法性自我檢查 (Startup Validation)**：
   - 啟動階段向 UAC API 發送 `GET /api-keys/systems`（內部網段免驗證），確認指定的系統名稱（例如 `mcp-report`）存在於合法清單中。若不存在或連線全數失敗則終止啟動。
4. **Header 提取順序**：
   - 優先讀取 `X-Api-Key`，若未提供則 Fallback 讀取 `Authorization: Bearer <key>`。
5. **SHA-256 安全快取**：
   - 使用 `apikey:{HEX_SHA256}` 作為快取鍵（預設 TTL 30 分鐘），避免明文儲存敏感 Key。
6. **雙軌身分上下文注入**：
   - 驗證成功後注入 `HttpContext.Items["ToolContext"]`（`UserId`=EmpId, `Role`=Name, `Department`）以及 `HttpContext.User`（`ClaimsPrincipal`，NameIdentifier 為 EmpId）。
7. **可觀測性**：
   - 實作 `UacApiHealthCheck`（掛載於 `/health/ready`）與 `Auth:Enabled: false` 單機開發開關。

---

## 3. McpGateway.Core 修改與實作範圍 (Detailed Scope of Changes)

| 模組 / 項目 | 檔案路徑 | 修改 / 新增說明 |
|---|---|---|
| **組態模型** | `src/McpGateway.Core/Configuration/McpGatewayOptions.cs` | 於 `AuthOptions` 擴充：`SystemName`（預設依 Department 或自訂）、`ConsulKey`（預設 `"ApiUrls.ProductionOa"`）、`ConsulUrls`（字串陣列）、`FallbackUacApiUrls`（字串陣列）、`CacheTtlMinutes`（預設 30）。 |
| **動態端點解析器** | `src/McpGateway.Core/Auth/UacApiEndpointResolver.cs`<br>`src/McpGateway.Core/Auth/IUacApiEndpointResolver.cs` *(新增)* | 負責自 Consul K/V `ApiUrls.ProductionOa` 解析 `UacApi` 清單，並在 Consul 異常時回退至 `FallbackUacApiUrls`。 |
| **UAC API 驗證器** | `src/McpGateway.Core/Auth/ApiKeyValidator.cs` *(重構)* | 實作 `IApiKeyValidator`：<br>1. SHA-256 雜湊快取查找。<br>2. 透過 `IHttpClientFactory` 呼叫 `GET /api-keys/identity?system={system}`。<br>3. 多節點容錯重試（Failover loop）。<br>4. 實作 `ValidateSystemRegisteredAsync()` 檢查 `/api-keys/systems`。 |
| **驗證中介軟體** | `src/McpGateway.Core/Auth/ApiKeyAuthenticationMiddleware.cs` *(重構)* | 1. 優先提取 `X-Api-Key`，Fallback 提取 `Authorization: Bearer <key>`。<br>2. 驗證成功後注入 `ToolContext` 與 `ClaimsPrincipal`。<br>3. 支援 `Auth.Enabled: false`。 |
| **啟動驗證器** | `src/McpGateway.Core/Validation/UacSystemStartupValidator.cs` *(新增)* | 實作 `IStartupValidator`，於 Gateway 啟動時校驗系統名稱是否存在於 UAC API `/api-keys/systems`。 |
| **健康檢查** | `src/McpGateway.Core/Observability/UacApiHealthCheck.cs` *(新增)* | 註冊於 Gateway Readiness 檢查 (`/health/ready`)，探測 UAC API 節點可用性。 |
| **DI 與管線擴充** | `src/McpGateway.Core/Hosting/McpGatewayHostExtensions.cs` | 註冊 `IUacApiEndpointResolver`、`IApiKeyValidator`、`UacSystemStartupValidator`、`UacApiHealthCheck`，並於 `MapMcpGateway` 掛載 Middleware。 |
| **單元測試** | `tests/McpGateway.Core.Tests/Auth/ApiKeyValidatorTests.cs`<br>`tests/McpGateway.Core.Tests/Auth/ApiKeyAuthenticationMiddlewareTests.cs` | 撰寫完整單元測試覆蓋多節點容錯、快取命中/未命中、Header 提取優先權、401 拒絕及啟動驗證情境。 |

---

## 4. 資料合約與格式範例 (Data Contracts)

### 4.1 Consul K/V `ApiUrls.ProductionOa` JSON 格式
```json
{
  "AppManagementApi": [
    "http://tnvcimweb1.cminl.oa/ApiGateway/AppMgrApi",
    "http://tnvcimweb2.cminl.oa/ApiGateway/AppMgrApi"
  ],
  "RemoteWolApi": [
    "http://tnvcimweb1.cminl.oa/apigateway/wakeupapi",
    "http://tnvcimweb2.cminl.oa/apigateway/wakeupapi"
  ],
  "UacApi": [
    "http://tnvcimweb1.cminl.oa/Apigateway/UacApi",
    "http://tnvcimweb2.cminl.oa/Apigateway/UacApi"
  ]
}
```

### 4.2 UAC API 請求與回應
- **端點**：`GET {baseUrl}/api-keys/identity?system=mcp-report`
- **Request Header**：`X-Api-Key: {rawKey}`
- **Response (200 OK)**：
  ```json
  {
    "empId": "12345678",
    "name": "Guy Mai"
  }
  ```

---

## 5. 測試接縫與驗證策略 (Testing Seams)

- **最高接縫**：`ApiKeyAuthenticationMiddleware` 整合測試與 `WebApplicationFactory` 端到端測試。
- **外部依賴隔離**：使用 Mock `HttpMessageHandler` 模擬 Consul K/V 及 UAC API（200 OK, 401 Unauthorized, 500 Failover）。

---

## 6. 建議使用的 Skills (Suggested Skills)

後續接手開發與測試的 Agent 建議調用以下 Skills：
- `/test`：執行單元測試並驗證邊界條件（`dotnet test`）。
- `/code-review`：審查重構後的 `ApiKeyValidator` 與 `ApiKeyAuthenticationMiddleware` 是否符合並發安全、快取隔離與例外處理準則。
