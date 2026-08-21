# Spec: UAC API Key 認證與 Consul 動態路由 (ADR-013)

> **關聯 ADR**：ADR-013 — 導入企業集中式 UAC API Key 認證、Consul K/V 動態路由容錯與啟動自我驗證機制  
> **狀態**：Ready for Agent

---

## Problem Statement

`McpGateway.Core` 目前的 `ApiKeyValidator` 採用自訂 `POST /validate` 協定，與企業現行由 **UAC API**（User Access Control API）集中管理的 Personal/Service API Key 機制不相容。
此外，UAC API 端點地址目前須以靜態組態配置，無法受益於企業集中式 **Consul K/V**（`ApiUrls.ProductionOa`）的動態服務發現與多節點高可用（HA Failover）能力。
若某個 Department Gateway（例如 `McpGateway.Report`）在 UAC 中未完成系統名稱（system name）登記，將導致所有呼叫於執行期失敗，而無啟動時的防護機制。

---

## Solution

將 `McpGateway.Core` 的 API Key 認證機制全面升級為標準 UAC API Key 協定：

- 透過 `GET /api-keys/identity?system={system}` + `X-Api-Key` Header 驗證身分。
- 透過 Consul K/V `ApiUrls.ProductionOa` 動態解析 UAC API 節點清單，支援多節點 HA Failover。
- 於 Gateway 啟動時呼叫 `GET /api-keys/systems` 自我校驗系統名稱合法性，失敗時阻斷啟動。
- 以 `apikey:{HEX_SHA256}` 為快取鍵（預設 TTL 30 分鐘）保護 API Key 明文、降低 UAC API 負載。
- 驗證成功後同時注入 `ToolContext`（HttpContext.Items）與 `ClaimsPrincipal`（HttpContext.User）供下游使用。
- 實作 `UacApiHealthCheck` 以便 Readiness 探針（`/health/ready`）即時監控 UAC API 可用性。

---

## User Stories

1. 作為 **AI Agent 開發者**，我希望使用企業 UAC API Key 呼叫 MCP Gateway，使我不需要維護自訂驗證憑證或協定。
2. 作為 **AI Agent 開發者**，我希望能透過 `X-Api-Key` Header 傳遞金鑰，使呼叫方式符合企業統一規範。
3. 作為 **AI Agent 開發者**，我希望也能透過 `Authorization: Bearer <key>` 傳遞金鑰（Fallback），使現有使用 Bearer Token 的用戶端無需立即改動。
4. 作為 **AI Agent 開發者**，我希望收到清晰的 `401 Unauthorized` 回應當我提供無效或已撤銷的 API Key，使我能即時得知授權失敗的原因。
5. 作為 **企業安全管理員**，我希望所有 API Key 的驗證均透過 UAC API 集中管理，使金鑰簽發、授權與生命週期由統一平台控制。
6. 作為 **系統管理員**，我希望 Gateway 啟動時自動校驗系統名稱是否已於 UAC 登記，使未完成登記的 Gateway 無法上線，防止帶病啟動。
7. 作為 **系統管理員**，我希望 UAC API 節點地址可由 Consul K/V 動態提供，使我更新服務地址時不需要重新部署 Gateway。
8. 作為 **系統管理員**，我希望當主要 UAC API 節點（5xx / 逾時）失敗時 Gateway 能自動切換至備用節點，使單一節點故障不影響認證服務。
9. 作為 **系統管理員**，我希望當 UAC API 同時受 401/403 回應時 Gateway 立即拒絕而不輪詢其他節點，避免無意義的重試。
10. 作為 **系統管理員**，我希望能設定 `FallbackUacApiUrls`，使本地或離線開發環境可在無 Consul 時仍能驗證 API Key。
11. 作為 **平台運維工程師**，我希望 `/health/ready` 端點能反映 UAC API 的可達性，使 Kubernetes Readiness Probe 能依此決定是否路由流量至 Gateway。
12. 作為 **開發者**，我希望能透過 `Auth:Enabled: false` 組態完全停用驗證，使本機單機開發時無需任何 UAC 連線。
13. 作為 **開發者**，我希望 API Key 驗證結果以 SHA-256 雜湊值為快取鍵快取 30 分鐘，使重複請求無需每次呼叫 UAC API，降低延遲。
14. 作為 **審計日誌使用者**，我希望驗證成功後 `HttpContext.User` 中包含 `ClaimsPrincipal`（以 EmpId 為 NameIdentifier），使 AuditLogger 能記錄完整的使用者識別資訊。
15. 作為 **Tool 開發者**，我希望驗證成功後 `HttpContext.Items["ToolContext"]` 中包含 `UserId`（EmpId）、`Role`（Name）與 `Department`，使下游 Tool 實作可直接取用呼叫者身分。
16. 作為 **部門 Gateway 維護者**，我希望在 `McpGatewayOptions.Auth` 中以 `SystemName` 欄位指定 UAC 系統名稱，使每個部門 Gateway 能獨立設定自己的系統識別碼。
17. 作為 **部門 Gateway 維護者**，我希望能透過 `ConsulKey` 與 `ConsulUrls` 欄位自訂 Consul 查詢目標，使不同環境（Dev/Staging/Prod）可使用不同的 Consul 叢集。
18. 作為 **部門 Gateway 維護者**，我希望能透過 `CacheTtlMinutes` 欄位調整快取有效期，使各部門可依其安全要求自訂金鑰快取時間。
19. 作為 **平台工程師**，我希望 Consul K/V 連線失敗時 Gateway 能 Fallback 至靜態 `FallbackUacApiUrls`，使 Consul 中斷不直接導致整個認證機制失效。
20. 作為 **安全稽核員**，我希望快取鍵使用 SHA-256 雜湊而非明文 API Key，使記憶體或 Redis 中永不出現原始 API Key。

---

## Implementation Decisions

### 1. 新增組態屬性至 `AuthOptions`

`McpGatewayOptions.Auth`（`AuthOptions` 類別）需新增以下欄位：

| 屬性 | 型別 | 預設值 | 說明 |
|---|---|---|---|
| `SystemName` | `string` | 繼承自 `Department` | UAC 系統名稱，用於 `/api-keys/identity?system=` 與 `/api-keys/systems` 查詢 |
| `ConsulKey` | `string` | `"ApiUrls.ProductionOa"` | Consul K/V 查詢鍵名 |
| `ConsulUrls` | `string[]` | `[]` | Consul Agent 地址清單 |
| `FallbackUacApiUrls` | `string[]` | `[]` | Consul 不可達時的備用 UAC API 節點清單 |
| `CacheTtlMinutes` | `int` | `30` | API Key SHA-256 快取有效期（分鐘） |

現有 `ApiKeyServiceUrl` 與 `ApiKeyTimeoutSeconds` 欄位應保留但降階為備用（當 Consul 未設定時使用）。

---

### 2. 新增 `IUacApiEndpointResolver` 與 `UacApiEndpointResolver`（新增模組）

負責：
1. 從 Consul K/V 讀取 `ApiUrls.ProductionOa` JSON，解析 `UacApi` 節點陣列。
2. Consul 連線失敗時 Fallback 至 `FallbackUacApiUrls`。
3. 以 `Singleton` 生命週期配合適當快取（Consul K/V 結果不需要每次請求重新查詢）。

介面合約：
```
IUacApiEndpointResolver
  Task<IReadOnlyList<string>> ResolveEndpointsAsync()
```

---

### 3. 重構 `ApiKeyValidator`（`IApiKeyValidator`）

從舊版 `POST /validate` 協定切換至新版 UAC API 協定：

**驗證流程（來自原型，trim 至決策關鍵部分）：**

```
cache_key = "apikey:" + HEX(SHA256(rawKey))

1. 查快取（cache_key）→ 命中則直接返回 ToolContext
2. 從 IUacApiEndpointResolver 取得端點清單
3. foreach endpoint in endpoints:
   a. GET {endpoint}/api-keys/identity?system={SystemName}
      Header: X-Api-Key: {rawKey}
   b. 200 OK → 解析 { "empId": "...", "name": "..." }
              → 建立 ToolContext，寫入快取，返回
   c. 401/403  → 立即返回 null（不重試）
   d. 5xx/逾時 → 繼續下一節點
4. 所有節點失敗 → 拋出 HttpRequestException (503)
```

新增方法：
```
Task<bool> ValidateSystemRegisteredAsync()
```
於 `GET /api-keys/systems` 確認 `SystemName` 存在於合法清單。

---

### 4. 重構 `ApiKeyAuthenticationMiddleware`

- **Header 提取順序**：優先讀取 `X-Api-Key`；若缺失則讀取 `Authorization: Bearer <key>`。
- **Auth.Enabled: false**：若停用則跳過所有驗證，直接呼叫 `_next`。
- **身分注入**（驗證成功後）：
  - `HttpContext.Items["ToolContext"]` = `ToolContext(userId: empId, role: name, department: options.Department, ...)`
  - `HttpContext.User` = 含 `NameIdentifier: empId` Claim 的 `ClaimsPrincipal`

---

### 5. 新增 `IAsyncStartupValidator` 介面，並新增 `UacSystemStartupValidator` 實作

**決策**：新增 `IAsyncStartupValidator` 介面（不修改現有 `IStartupValidator`），`UacSystemStartupValidator` 實作此介面。`RunMcpGatewayAsync` 統一 `await` 所有 `IAsyncStartupValidator`。

原因：現有 `IStartupValidator.Validate()` 為同步方法，在啟動路徑上以 `.GetAwaiter().GetResult()` 包裝非同步 HTTP 呼叫有死鎖風險，且會將例外包裝在 `AggregateException` 中降低可讀性。新增非同步介面可完全避免此問題，且完全向後相容（現有同步 validator 繼續實作舊介面）。

介面合約：
```csharp
public interface IAsyncStartupValidator
{
    Task<IReadOnlyList<ValidationError>> ValidateAsync(CancellationToken ct = default);
}
```

啟動呼叫方式（`RunMcpGatewayAsync` 中）：
```csharp
var asyncValidators = app.Services.GetServices<IAsyncStartupValidator>();
foreach (var v in asyncValidators)
{
    var errors = await v.ValidateAsync();
    if (errors.Any()) throw new StartupValidationException(errors);
}
```

`UacSystemStartupValidator` 職責：
- 呼叫 UAC API `GET /api-keys/systems`（內部網段免驗證）。
- 若回傳清單不含 `SystemName`，或所有節點均無法連線，則回傳 `ValidationError` 清單（由呼叫端拋出 `StartupValidationException` 阻斷啟動）。

---

### 6. 新增 `UacApiHealthCheck`

- 實作 `IHealthCheck`，探測 `IUacApiEndpointResolver` 解析的第一個可用端點。
- 掛載至 `/health/ready`（`ready` tag），取代或補充現有 `GatewayReadinessHealthCheck`。

---

### 7. DI 與管線更新（`McpGatewayHostExtensions`）

- 以 `Singleton` 註冊 `IUacApiEndpointResolver`。
- 更新 `IApiKeyValidator` 的 `HttpClient` 工廠（不再設定靜態 `BaseAddress`，因端點由 resolver 動態決定）。
- 於 `AddMcpGateway()` 中新增 `UacSystemStartupValidator` 的呼叫掛鉤。
- 於 `AddHealthChecks()` 中新增 `UacApiHealthCheck`（`ready` tag）。

---

## Testing Decisions

### 什麼是好的測試？

- 測試**外部可觀察行為**，而非實作細節（不直接測試 SHA-256 計算邏輯或 Consul JSON 解析）。
- 測試**HTTP 邊界**：以 Mock `HttpMessageHandler`（或 WireMock）模擬 UAC API 和 Consul K/V 回應。
- 每個測試只驗證一個行為。

### 主要測試模組

**接縫：`ApiKeyAuthenticationMiddleware` 整合測試（最高優先）**

使用 `WebApplicationFactory<TEntryPoint>` 或手動 `WebApplication.CreateBuilder()` 建立測試主機，搭配 WireMock 或 `MockHttpMessageHandler` 模擬 UAC API 端點，測試整個中介軟體管線。

| 場景 | 預期行為 |
|---|---|
| 請求帶有效 `X-Api-Key` | `200 OK`；`ToolContext` 與 `ClaimsPrincipal` 正確注入 |
| 請求帶有效 `Authorization: Bearer <key>`（無 `X-Api-Key`） | `200 OK`；正確 Fallback 提取 |
| 請求同時帶有 `X-Api-Key` 與 `Authorization` | `X-Api-Key` 優先，Bearer 被忽略 |
| 請求帶無效 API Key（UAC 回 401） | `401 Unauthorized` |
| UAC API 第一節點 5xx，第二節點 200 | 自動容錯，`200 OK` |
| 所有 UAC API 節點均失敗（5xx） | `503 Service Unavailable` |
| 快取命中（第二次相同 Key 請求） | UAC API 僅被呼叫一次（Mock 驗證呼叫次數） |
| `Auth:Enabled: false` | 跳過驗證，請求直通 |
| 請求無任何 API Key Header | `401 Unauthorized` |

**接縫：`UacApiEndpointResolver` 單元測試**

使用 Mock `HttpMessageHandler` 模擬 Consul K/V 回應。

| 場景 | 預期行為 |
|---|---|
| Consul 正常，回傳有效 `UacApi` 節點清單 | 解析正確節點清單 |
| Consul 連線失敗 | Fallback 至 `FallbackUacApiUrls` |
| Consul 回傳 JSON 缺少 `UacApi` 鍵 | Fallback 至 `FallbackUacApiUrls` |

**接縫：`UacSystemStartupValidator` 單元測試**

使用 Mock `HttpMessageHandler` 模擬 `GET /api-keys/systems` 回應。

| 場景 | 預期行為 |
|---|---|
| UAC 回傳清單包含 `SystemName` | 驗證通過，不拋出例外 |
| UAC 回傳清單不含 `SystemName` | 拋出例外阻斷啟動 |
| UAC API 全節點失敗 | 拋出例外阻斷啟動 |

### 先前案例（Prior Art）

- `tests/McpGateway.Core.IntegrationTests/Downstream/DownstreamClientIntegrationTests.cs` — 使用 `WireMock.Server` + `IAsyncLifetime` 的整合測試模式，新測試應遵循相同結構。

---

## Out of Scope

- **JWT 認證提供者**的修改：本 spec 僅針對 `API-KEY` Provider，不修改 JWT 或 NTLM 流程。
- **Department Gateway（`McpGateway.Report` 等）的業務邏輯**：組態範例會提供，但業務 Tool 不在本 spec 範圍。
- **Consul Agent 本身的部署或管理**：假設 Consul 叢集已存在且可達。
- **UAC API Key 的簽發與生命週期管理**：屬於 UAC 平台職責，不在 Gateway 範圍內。
- **Redis 快取層的升級**：現有 `ITokenCacheService` 架構不改動，SHA-256 快取鍵改動在 `ApiKeyValidator` 層實作。
- **`DownstreamClient` 重試邏輯**：本 spec 的多節點輪詢邏輯僅用於 UAC API 驗證，不修改 `DownstreamClient`。

---

## Further Notes

- **測試用 UAC 服務**：`http://hp08239p.cminl.oa/uacapitest/`（Swagger：`http://hp08239p.cminl.oa/uacapitest/api-docs/v1/swagger.json`）可用於手動驗證。
- **參考實作**：`d:\projects\ECimApi/Helpers/ApiKeyAuthService.cs` 提供企業既有 UAC API 整合的 C# 實作先例，實作時應參照。
- **快取 TTL 預設值調整**：現有 `TokenCacheOptions.ApiKeyTtlMinutes` 預設 5 分鐘；本 spec 要求新 `AuthOptions.CacheTtlMinutes` 預設 30 分鐘，兩者並存，實作時以 `AuthOptions.CacheTtlMinutes` 優先。
