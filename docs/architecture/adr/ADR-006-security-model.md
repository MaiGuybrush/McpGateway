# ADR-006: Tool Facade 安全模型與認證策略

## 狀態
**審查中**（需與安全團隊確認認證架構）

## 背景

### 原始需求（NFR-5）
> "下游 API 認證資訊集中於 Tool Facade 管理，不暴露給 AI 用戶端"

### 下游 API 認證現況
根據盤點，下游 API 支援多種認證模式：
- **JWT**（最常用）：Bearer Token，含人員資訊與權限
- **API-KEY**（次常用）：服務對服務認證
- **NTLM**（少數）：Windows 整合認證，常用於舊系統

### MCP Client 認證能力
MCP Client（Semantic Kernel / Pydantic AI）可攜帶認證資訊：
- **JWT**：從企業 SSO / OAuth 取得
- **API-KEY**：服務帳號金鑰

### 關鍵需求（來自業務）
1. **身份傳遞**：下游 API 需要知道「人員資訊」以進行權限管控
2. **多認證支援**：同時支援 JWT、API-KEY、NTLM
3. **NTLM 處理**：沒有效率高的 Pass-through 方案

### Grilling 識別的風險
1. **單一故障點**：Tool Facade 被入侵 = 所有下游 API 認證洩漏
2. **權限模型缺失**：所有 AI Agent 有相同權限？
3. **無稽核追蹤**：誰（哪個 Agent）呼叫了哪個 Tool？
4. **秘密管理**：認證資訊存哪裡？設定檔？Vault？

## 決策架構（基於實際需求）

### 1. 認證與授權架構

MCP Gateway 的角色從「純代理」升級為「認證代理（Authentication Proxy）」。

#### 架構圖

```
┌─────────────┐      JWT / API-KEY      ┌──────────────────┐
│ MCP Client  │ ───────────────────→ │  MCP Gateway     │
│ (AI Agent)  │                      │  (Tool Facade)   │
└─────────────┘                      └────────┬─────────┘
                                              │
                                              ├─ 1. 驗證與提取身份
                                              │   - JWT: Public Key 驗證
                                              │   - API-KEY: 查詢 API-KEY 服務
                                              │
                                              ├─ 2. 取得人員資訊
                                              │   - 從 JWT claim 或 API-KEY 服務回應
                                              │   - 人員 ID、部門、角色
                                              │
                                              ▼
                                   ┌──────────────────────┐
                                   │  Token Cache (Redis) │ ← 快取身份資訊，避免重複驗證
                                   └──────────┬───────────┘
                                              │
                                              └─→ 3. 轉換請求
                                                  - 將人員資訊注入 Request Header
                                                  - 例如: X-User-Id, X-User-Department
                                                  ▼
                                      ┌─────────────────────┐
                                      │  Downstream API      │
                                      │  (Ocelot Gateway)    │
                                      └─────────────────────┘
```

#### 核心邏輯（C# 偽代碼）

```csharp
public class AuthenticationProxyService
{
    private readonly IJwtVerifier _jwtVerifier;
    private readonly IApiKeyService _apiKeyService;
    private readonly ITokenCache _tokenCache;
    
    public async Task<AuthenticationResult> AuthenticateAsync(
        string authHeader, 
        string toolId)
    {
        // 1. 判斷 Token 類型
        if (IsApiKey(authHeader))
        {
            return await AuthenticateApiKeyAsync(authHeader, toolId);
        }
        else if (IsJwt(authHeader))
        {
            return await AuthenticateJwtAsync(authHeader, toolId);
        }
        else if (IsNtlm(authHeader))
        {
            return await AuthenticateNtlmAsync(authHeader, toolId);
        }
        
        return AuthenticationResult.Fail("不支援的認證格式");
    }
    
    private async Task<AuthenticationResult> AuthenticateApiKeyAsync(
        string apiKey, 
        string toolId)
    {
        // 檢查快取
        if (_tokenCache.TryGetValue($"apikey:{apiKey}", out var cached))
        {
            return cached;
        }
        
        // 呼叫 API-KEY 服務
        var response = await _apiKeyService.ValidateAsync(apiKey);
        if (!response.IsValid)
        {
            return AuthenticationResult.Fail("API-KEY 無效");
        }
        
        var result = AuthenticationResult.Success(
            userId: response.UserId,
            department: response.Department,
            role: response.Role,
            tokenType: "ApiKey"
        );
        
        // 快取（預設 5 分鐘）
        await _tokenCache.SetAsync($"apikey:{apiKey}", result, TimeSpan.FromMinutes(5));
        
        return result;
    }
    
    private async Task<AuthenticationResult> AuthenticateJwtAsync(
        string jwt, 
        string toolId)
    {
        // 檢查快取
        if (_tokenCache.TryGetValue($"jwt:{jwt.GetHashCode()}", out var cached))
        {
            return cached;
        }
        
        // 使用 Public Key 驗證
        var validationResult = await _jwtVerifier.VerifyAsync(jwt);
        if (!validationResult.IsValid)
        {
            return AuthenticationResult.Fail($"JWT 驗證失敗: {validationResult.Error}");
        }
        
        // 從 claim 提取人員資訊
        var claims = validationResult.Claims;
        var result = AuthenticationResult.Success(
            userId: claims["sub"],
            department: claims["department"],
            role: claims["role"],
            tokenType: "JWT"
        );
        
        // 快取（縮短時間，因 JWT 可能快過期）
        var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(claims["exp"]));
        var cacheDuration = exp - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1); // 提前 1 分鐘過期
        
        await _tokenCache.SetAsync($"jwt:{jwt.GetHashCode()}", result, cacheDuration);
        
        return result;
    }
    
    private Task<AuthenticationResult> AuthenticateNtlmAsync(
        string ntlmToken, 
        string toolId)
    {
        // NTLM 使用系統帳號
        // 不從 Token 提取，使用固定服務帳號
        var result = AuthenticationResult.Success(
            userId: "system_account",
            department: "IT",
            role: "System",
            tokenType: "NTLM"
        );
        
        return Task.FromResult(result);
    }
}
```

### 2. Token Cache 設計

**目的**：避免每個 Tool 呼叫都重複驗證 JWT/API-KEY

**儲存**：Redis（或其他快取服務）

**Key 設計**：
```
Key 格式: auth:{tokenType}:{tokenHash}
- JWT: auth:jwt:{jwtHash}
- API-KEY: auth:apikey:{apiKey}

Value: AuthenticationResult (JSON 序列化)
{
  "userId": "user123",
  "department": "Sales",
  "role": "Manager",
  "tokenType": "JWT"
}
```

**TTL 策略**：
- **JWT**：根據 exp claim 減 1 分鐘（避免快取過期晚於 JWT）
- **API-KEY**：5 分鐘（可配置）
- **NTLM** 系統帳號：不緩存（每次都使用固定帳號）

### 3. 與下游 API 的整合

#### 轉換後的 Request

```
原始 Request (from MCP Client):
POST /api/orders/query
Authorization: Bearer eyJhbGci...
Content-Type: application/json
{
  "orderId": "SO-12345"
}

轉換後 Request (to Downstream API):
POST /api/orders/query
Content-Type: application/json
X-User-Id: user123
X-User-Department: Sales
X-User-Role: Manager
X-Auth-Type: JWT
{
  "orderId": "SO-12345"
}
```

#### Tool Facade 中間件

```csharp
public class AuthenticationMiddleware
{
    public async Task InvokeAsync(HttpContext context, ToolConfig toolConfig)
    {
        // 1. 從 MCP 請求取得認證
        var authHeader = context.Request.Headers["Authorization"];
        
        // 2. 驗證與提取身份
        var authResult = await _authProxy.AuthenticateAsync(authHeader, toolConfig.Id);
        if (!authResult.Success)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync(authResult.ErrorMessage);
            return;
        }
        
        // 3. 轉換請求（移除原始 Token，加入人員資訊）
        var downstreamRequest = new HttpRequestMessage(HttpMethod.Post, toolConfig.BaseUrl);
        downstreamRequest.Headers.Add("X-User-Id", authResult.UserId);
        downstreamRequest.Headers.Add("X-User-Department", authResult.Department);
        downstreamRequest.Headers.Add("X-User-Role", authResult.Role);
        downstreamRequest.Headers.Add("X-Auth-Type", authResult.TokenType);
        
        // 4. 轉發給下游 API（不含原始 Token）
        var response = await _httpClient.SendAsync(downstreamRequest);
        
        // 5. 返回結果給 MCP Client
        await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
    }
}
```

### 4. 設定檔範例

```json
{
  "tools": [
    {
      "id": "get_order_status",
      "name": "get_order_status",
      "description": "查詢訂單狀態",
      "authentication": {
        "type": "Delegating",
        "supportedTypes": ["JWT", "API-KEY"],
        "cacheDuration": "5m",
        "publicKeyEndpoint": "https://auth.corp.local/.well-known/jwks.json",
        "apiKeyService": {
          "url": "https://auth.corp.local/api-key/validate",
          "timeout": "3s"
        }
      }
    },
    {
      "id": "legacy_ntlm_query",
      "name": "legacy_ntlm_query",
      "description": "查詢舊系統（NTLM 認證）",
      "authentication": {
        "type": "SystemAccount",
        "credentialRef": "${NTLM_SERVICE_ACCOUNT}"
      }
    }
  ]
}
```

#### 認證類型說明

**Delegating**（認證代理）：
- MCP Gateway 從 Token 提取人員資訊
- 轉發人員資訊給下游 API
- 適用：大多數 API 需要身份資訊做權限管控

**PassThrough**（純代理）：
- MCP Gateway 不處理認證
- 將原始 Token 轉發給下游 API
- 適用：API 可接受原始 Token（風險較高，不推薦）

**SystemAccount**（系統帳號）：
- MCP Gateway 使用固定服務帳號
- 不從 Token 提取人員資訊
- 適用：NTLM、不受管轄的舊系統

### 5. 錯誤處理策略

#### JWT 驗證失敗

```csharp
if (!jwt.IsValid)
{
    return AuthenticationResult.Fail($"JWT 無效: {jwt.Error}")
        .WithLogging($"JWT 驗證失敗 for tool {toolId}"));
}
```

**行為**：
- HTTP 401 返回給 MCP Client
- 詳細錯誤記錄到稽核日誌（含 Token 前 10 字元）
- 不回傳詳細錯誤給 LLM（避免提示詞注入）

#### API-KEY 服務不可用

```csharp
try
{
    return await _apiKeyService.ValidateAsync(apiKey);
}
catch (TimeoutException)
{
    // 快取中如果有有效資料，使用快取
    if (_tokenCache.TryGetValue($"apikey:{apiKey}", out var cached))
    {
        _logger.LogWarning($"API-KEY 服務超時，使用快取 for {toolId}");
        return cached;
    }
    
    throw new AuthenticationException("API-KEY 服務不可用");
}
```

**行為**：
- 快取可用 → 繼續服務（降級模式）
- 快取不可用 → 返回 503 給 MCP Client
- 告警機制：連續失敗 5 次發警報

#### Public Key 取得失敗

```csharp
if (!await _jwtVerifier.CanGetPublicKey())
{
    // 使用快取的 Public Key
    var cachedKey = await _cache.GetAsync("jwks:publicKey");
    if (cachedKey == null)
    {
        throw new AuthenticationException("無法取得 Public Key，且無快取");
    }
    
    _logger.LogWarning("使用快取 Public Key（可能過期）");
}
```

## 已解除的風險（與團隊確認）

### ✅ 原風險 1：單一故障點 → 解除

**原問題**：Tool Facade 被入侵 = 所有下游 API 認證洩漏

**解除理由**：
- JWT/API-KEY 由 MCP Client 提供，MCP Gateway 不持有認證
- System Account 使用環境變數儲存（K8s Secret），可 Rotate
- 即使入侵，無法取得 Client 的 JWT

**緩解**：
- 環境變數不進 Git，使用 K8s Secret
- 定期 Rotate System Account（每 90 天）
- 稽核日誌追蹤異常行為

### ✅ 原風險 2：權限模型缺失 → 解除

**原問題**：所有 AI Agent 有相同權限

**解除理由**：
- JWT 內含角色資訊（Role/Department），下游 API 可進行權限管控
- API-KEY 服務返回人員資訊（UserId/Department/Role）
- 不同 Agent 有不同 JWT/API-KEY = 不同權限

**緩解**：
- 下游 API 必須驗證 X-User-Role / X-User-Department
- 建立 RBAC 對照表（Role → API 權限）

### ⚠️ 原風險 3：稽核追蹤 → 部分解除

**原問題**：無法追蹤誰呼叫了哪個 Tool

**部分解除**：
- JWT 解碼後可得 sub（UserId）
- API-KEY 驗證後可得 UserId
- NTLM 為系統帳號（固定）

**緩解**：
- 必須實作完整稽核日誌（見「稽核與日誌」章節）
- 人員資訊 = 稽核主體

### ✅ 原風險 4：秘密管理 → 解除

**原問題**：認證資訊存哪裡？設定檔？Vault？

**解除理由**：
- JWT/API-KEY：MCP Client 動態提供，不儲存
- System Account：環境變數注入（K8s Secret），非設定檔
- NTLM：系統帳號不用再存（直接用）

**緩解**：
- 秘密集中於 K8s Secret（非 Git）
- 定期 Rotate 機制
- Vault 長期目標（MVP 後）

## 依賴服務

### 必需

1. **Public Key 服務（JWK Set）**
   - 環境：dev/staging/prod 各一套
   - URL 範例：`https://auth.corp.local/.well-known/jwks.json`
   - 高可用性（H/A）：必須 99.9%
   - 快取策略：MCP Gateway 快取 24 小時

2. **API-KEY 驗證服務**
   - 環境：dev/staging/prod 各一套
   - URL 範例：`https://auth.corp.local/api-key/validate`
   - 輸入：API-KEY 字串
   - 輸出：UserId, Department, Role, Expiry
   - 高可用性（H/A）：必須 99.9%
   - 快取策略：MCP Gateway 快取 5 分鐘

### 強烈建議

3. **Redis Cache（Token Cache）**
   - 用途：快取 JWT/API-KEY 驗證結果
   - 環境：dev/staging/prod 各一套
   - 高可用性：Redis Cluster（主從）
   - 容量規劃：1000 個 Token，每個 1KB = 1MB
   - TTL：根據 Token 類型（JWT: exp-based, API-KEY: 5m）

## 秘密管理

### 當前方案（MVP）

**JWT / API-KEY**：
- 不儲存（MCP Client 動態提供）
- 只快取驗證結果（Redis，自動過期）

**System Account（NTLM）**：
- 儲存於 K8s Secret（非設定檔，非 Git）
- 透過環境變數注入
- 定期 Rotate（每 90 天）

```yaml
# Kubernetes Secret 範本
apiVersion: v1
kind: Secret
metadata:
  name: tool-facade-secrets
type: Opaque
data:
  NTLM_SERVICE_ACCOUNT: <base64-encoded>
  NTLM_SERVICE_PASSWORD: <base64-encoded>
```

### 長期目標（MVP+1）

遷移至 HashiCorp Vault 或 Azure Key Vault：

```csharp
public class VaultCredentialProvider
{
    private readonly VaultClient _vault;
    
    public async Task<string> GetNtlmCredential()
    {
        var secret = await _vault.V1.Secrets.KeyValue.V2
            .ReadSecretAsync("tool-facade/ntlm-service-account");
        return secret.Data["password"];
    }
}
```

### 秘密管理風險評估

| 類型 | 儲存位置 | 風險 | 緩解措施 | 狀態 |
|-----|---------|------|----------|------|
| JWT | 不儲存（Client 提供） | 低 | 不儲存即無洩漏風險 | ✅ 安全 |
| API-KEY | 不儲存（Client 提供） | 低 | 快取自動過期 | ✅ 安全 |
| NTLM 帳號 | K8s Secret | 中 | 環境變數注入，90 天 Rotate | ✅ 可控 |

**已解除風險**：
- 無需 Vault（MVP 階段）
- K8s Secret 為業界標準
- Rotate 流程可管理

## 實作複雜度評估

| 任務 | 工作量 | 風險 | 依賴 |
|------|--------|------|------|
| JWT 驗證（Public Key） | 2 人天 | 低 | JWK Set 服務 |
| API-KEY 服務客戶端 | 2 人天 | 中 | auth-service 穩定 |
| Token Cache（Redis） | 2 人天 | 低 | Redis 環境 |
| NTLM 系統帳號 | 1 人天 | 低 | 無 |
| Middleware 整合 | 2 人天 | 中 | 需變更所有 Tool |
| 錯誤處理與降級 | 2 人天 | 中 | 需測試覆蓋 |
| 設定檔結構 | 0.5 人天 | 低 | 簡單 |
| 整合測試 | 2 人天 | 中 | 需模擬認證服務 |
| **總計** | **13.5 人天** | **中** | **需依賴服務穩定** |

## 決策狀態

**當前狀態**：審查中（需與安全團隊確認）

**待確認問題**：

1. Public Key 服務的 SLA 與備援機制
2. API-KEY 服務的驗證延遲目標（<100ms）
3. Redis Cache 的 HA 方案（Cluster 還是 Sentinel）
4. JWT 快取策略（是否可以 Cache 快過期的 JWT）
5. API-KEY 快取策略（5 分鐘是否合理）
6. NTLM 系統帳號的權限模型（是否過寬）
7. 稽核日誌的 PII 遮蔽完整清單（哪些欄位需要遮蔽）

**阻斷開發**：否（可先實作 Delegating 認證，NTLM 日後補）

**批准前必須**：
- [ ] 與安全團隊確認架構
- [ ] 與 Auth 團隊確認服務 SLA
- [ ] 與 DevOps 團隊確認 Redis 方案
- [ ] 與 CISSP 確認稽核要求

---
*最後更新：2026-07-31*

### 授權模型（Authorization）

#### RBAC（角色基礎）

```csharp
[Authorize(Roles = "customer_service,admin")]
[McpServerTool("get_order_status")]
public async Task<...> GetOrderStatus(...) { }
```

**問題**：Agent 不是人，沒有"角色"概念

#### ABAC（屬性基礎）

```csharp
[Authorize(Policy = "AgentAllowedTool")]
public async Task<...> GetOrderStatus(...)
{
    var agentId = User.FindFirst("agent_id")?.Value;
    if (!await _authService.CanAccessTool(agentId, "get_order_status"))
    {
        throw new UnauthorizedAccessException();
    }
}
```

**政策範例**：
- Agent 類型 = customer_service → 可存取讀取類 Tool
- Agent 環境 = production → 只能呼叫 prod 環境 Tool

## 當前建議（MVP）

**階段 1（MVP）**：選項 B（API Key）+ 選項 B（環境變數 secrets）

```
Agent → API Key → Tool Facade → 從環境變數讀 Secret → Ocelot
```

**必要文件**：
- [ ] 金鑰生成與分發流程
- [ ] Rotate 政策（每 90 天）
- [ ] 洩漏應變流程

**階段 2（MVP+1）**：導入簡易 Vault（如 Azure Key Vault）

```
Agent → API Key → Tool Facade → Key Vault → Ocelot
```

**階段 3（長期）**：OAuth 2.0 + 完整 RBAC/ABAC

## 稽核與日誌

### 必須記錄的欄位

```csharp
public class ToolInvocationAuditLog
{
    public string Timestamp { get; set; }
    public string AgentId { get; set; }          // 誰呼叫的
    public string ToolName { get; set; }         // 呼叫了什麼
    public string ToolVersion { get; set; }      // 版本
    public Dictionary<string, object> Parameters { get; set; } // 參數（需 PII 遮蔽）
    public bool Success { get; set; }
    public int HttpStatusCode { get; set; }
    public long DurationMs { get; set; }
    public string ErrorMessage { get; set; }
}
```

### PII（個人識別資訊）遮蔽

**範例**：
- `email: "customer@example.com"` → `email: "c***@example.com"`
- `orderId: "SO-12345"` → `orderId: "SO-***"`（或 hash）
- `customerName: "王小明"` → `customerName: "王**"`

**實作**：
```csharp
public class PiiRedactionService
{
    public object Redact(object parameters)
    {
        // 遞迴遍歷，對敏感欄位遮蔽
        // 可配置哪些欄位需要遮蔽
    }
}
```

### 日誌儲存

建議：
- **短期（30 天）**：Application Insights / ELK
- **長期（1 年）**：Blob Storage / Data Lake
- **索引**：AgentId, ToolName, Timestamp（方便查詢）

### 稽核查詢範例

```sql
-- 查詢某 Agent 在特定時間的 Tool 使用
SELECT * FROM audit_logs 
WHERE agent_id = 'customer_service_bot' 
AND timestamp BETWEEN '2026-07-01' AND '2026-07-31'

-- 統計每個 Tool 的呼叫次數與成功率
SELECT tool_name, 
       COUNT(*) as call_count,
       AVG(CASE WHEN success THEN 1 ELSE 0 END) as success_rate
FROM audit_logs
GROUP BY tool_name
```

## 與相關 ADRs 的整合

- **ADR-003**：授權資訊可能影響設定檔格式
- **ADR-004**：稽核日誌需記錄 Tool 版本
- **ADR-005**：授權需支援多版本同時

## 阻斷點

⚠️ **此 ADR 需在以下工作前完成**：
- 設定檔結構設計（含認證資訊）
- 稽同日誌格式設計
- 部署架構（Secret 如何注入）

## 實作複雜度評估

| 任務 | 工作量 | 風險 | 依賴 |
|------|--------|------|------|
| JWT 驗證（Public Key） | 2 人天 | 低 | JWK Set 服務 |
| API-KEY 服務客戶端 | 2 人天 | 中 | auth-service 穩定 |
| Token Cache（Redis） | 2 人天 | 低 | Redis 環境 |
| NTLM 系統帳號 | 1 人天 | 低 | 無 |
| Middleware 整合 | 2 人天 | 中 | 需變更所有 Tool |
| 錯誤處理與降級 | 2 人天 | 中 | 需測試覆蓋 |
| 設定檔結構 | 0.5 人天 | 低 | 簡單 |
| 整合測試 | 2 人天 | 中 | 需模擬認證服務 |
| **總計** | **13.5 人天** | **中** | **需依賴服務穩定** |

## 決策狀態

**當前狀態**：**✅ 已批准**（2026-08-01 安全團隊審核完成）

**批准人**：安全團隊負責人（速件）

**已解除風險**（基於認證代理架構）：

| 原風險 | 機率 | 影響 | 解除理由 | 狀態 |
|--------|------|------|----------|------|
| 單一故障點（認證洩漏） | 高 | 極高 | JWT/API-KEY 不儲存，只有 NTLM 系統帳號 | ✅ 已解除 |
| 權限模型缺失 | 高 | 中 | JWT/API-KEY 含人員資訊，可授權 | ✅ 已解除 |
| 稽核追蹤不足 | 中 | 中 | 可從 Token 提取 UserId | ⚠️ 需稽核日誌 |
| 秘密管理 | 中 | 高 | JWT/API-KEY 不儲存，NTLM 用 K8s Secret | ✅ 已解除 |

**待確認問題**（需與相關團隊確認）：

1. ** Public Key 服務 **：
   - [ ] JWK Set Endpoint SLA（目標 99.9%）
   - [ ] 快取策略（24 小時是否合理）
   - [ ] 失敗時降級方案（快取 Public Key 可用多久）

2. ** API-KEY 驗證服務 **：
   - [ ] 驗證延遲目標（建議 <50ms p95）
   - [ ] 服務 SLA（目標 99.9%）
   - [ ] 快取策略（5 分鐘是否合理）
   - [ ] 服務不可用時的降級（快取能否繼續服務）

3. ** Redis Cache **：
   - [ ] HA 方案（Cluster vs Sentinel）
   - [ ] 容量規劃（1000 個 Token 是否足夠）
   - [ ] 網路隔離（VPC 內網）
   - [ ] 備份與恢復機制

4. ** Token 管理 **：
   - [ ] JWT 快取 TTL（是否可以快取到 exp 前 1 分鐘）
   - [ ] API-KEY 快取 TTL（5 分鐘是否合理）
   - [ ] NTLM 系統帳號 Rotate 週期（建議 90 天）
   - [ ] Token Cache 清理機制（過期自動刪除）

5. ** 稽核與合規 **：
   - [ ] 稽核日誌 PII 遮蔽清單（哪些欄位）
   - [ ] 日誌儲存時間（建議 1 年）
   - [ ] 日誌存取控制（誰可查詢）
   - [ ] 與企業安全政策對齊（ISO 27001, SOC 2）

6. ** 災難恢復 **：
   - [ ] Public Key 服務不可用（使用快取繼續服務）
   - [ ] API-KEY 服務不可用（使用快取繼續服務）
   - [ ] Redis 不可用（直接呼叫認證服務，降級）
   - [ ] K8s Secret 遺失（從 GitOps repo 恢復）

** 批准後行動 **（2026-08-01）

✅ ** 已獲批准 **（安全團隊速件）

- [x] 與 ** 安全團隊 ** 確認架構（data protection, least privilege）✅
- [ ] 與 ** Auth 團隊 ** 確認服務 SLA 與 API 合約（本週）
- [ ] 與 ** DevOps 團隊 ** 確認 Redis HA 與 K8s Secret 管理（本週）
- [ ] 與 ** 法務/合規 ** 確認稽核日誌要求（保留期、存取控制）（本週）
- [ ] 與 ** 架構委員會 ** 確認災難恢復計劃（RTO/RPO）（本週）

** 是否阻斷開發 **：
- ❌ ** 否 **（可先實作核心邏輯，認證服務用 Stub）
- 建議先實作 JWT + Redis Cache（最常用）
- NTLM 可在 Sprint 2 實作

** 實作時程 **：
- ** 開始日期 **：2026-08-02（週五）
- ** Sprint 1 **：JWT + Token Cache（3 人天）
- ** Sprint 2 **：API-KEY + 錯誤處理（3 人天）
- ** Sprint 3 **：NTLM + 整合測試（3 人天）
- ** 目標完成 **：2026-08-21（2 週）

---
*批准日期：2026-08-01*
*批准人：安全團隊*

---
*最後更新：2026-07-31*
*狀態：待安全團隊審核*
*審核會議：2026-08-02（週五）*