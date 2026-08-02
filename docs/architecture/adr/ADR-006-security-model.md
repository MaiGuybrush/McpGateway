# ADR-006: Tool Facade 安全模型與認證策略

## 狀態

**✅ 已批准**（2026-08-01 安全團隊速件）
**⚠️ 需 Delta 確認**（2026-08-02，因 ADR-009 拓撲變更）

---

## ⚠️ 拓撲變更通知（2026-08-02）

> 本 ADR 於 2026-08-01 獲安全團隊批准時，假設 **Tool Facade 為單一服務**。
> [ADR-009](ADR-009-department-gateway-split.md) 已將架構改為 **`McpGateway.Core` package + N 個部門服務**。
>
> **原核心決策（認證代理架構、Delegating Pattern、Token Cache、K8s Secret）全部維持不變**，
> 但下列六項因多服務拓撲而需調整，且需安全團隊做 **delta 確認**（非全案重審）。

### Delta 清單

| # | 項目 | 原假設（單一服務） | 變更後（N 服務 + Core package） | 影響 |
|---|------|-------------------|------------------------------|------|
| Δ1 | 實作落點 | 單體服務內 | **`McpGateway.Core`，僅一份實作** | ✅ 改善：安全審核面積不變，部門專案無法漏裝 |
| Δ2 | 攻擊面 | 1 個服務端點 | N 個服務端點 | ⚠️ 面積變大，但**爆炸半徑縮小**（見下） |
| Δ3 | 共用元件風險 | 無 | Core 出 CVE 同時影響全部部門 | ⚠️ **新風險** → 由 ADR-009 D3 三層防護緩解 |
| Δ4 | 稽核日誌 | 無部門維度 | 需新增 `Department` 欄位 | ⚠️ 需改 schema |
| Δ5 | Token Cache | 單服務容量規劃 | N 服務共用 Redis，需鍵命名空間 | ⚠️ 需重算容量 |
| Δ6 | NTLM 系統帳號 | 單一組 | 各部門一組 vs 共用一組（待決） | ⚠️ 需安全團隊裁示 |

### Δ2 詳述：攻擊面 vs 爆炸半徑

多服務拓撲對安全是**淨改善**，理由：

```
單一服務被入侵：
  攻擊者取得  →  全公司所有 Tool 的下游存取能力
                 全部 NTLM 系統帳號

部門服務被入侵（採各部門獨立 NTLM 帳號時）：
  攻擊者取得  →  僅該部門 Tool 的下游存取能力
                 僅該部門的 NTLM 帳號
  其他部門    →  不受影響
```

原「單一故障點」風險（本 ADR 原列為 高機率／極高影響）在多服務拓撲下**進一步降低**。

### Δ3 詳述：共用元件的新風險

這是 ADR-009 引入的**唯一新增安全風險**：Core package 若含認證漏洞，N 個部門服務全數受影響，且各部門獨立發版代表修補進入生產的時間不可控。

緩解由 ADR-009 D3 提供三層防護：

1. **浮動版本範圍** `[1.2,2.0)` — 部門下次 rebuild 自動吃到 patch/minor
2. **CI 閘門** — Core 版本低於 `MIN_SUPPORTED` 直接 fail build，不靠部門自律
3. **執行期遙測** `mcpgw_core_version{dept="..."}` — 平台團隊可見哪個部門落後，可主動催辦

**安全團隊需確認**：`MIN_SUPPORTED` 由誰維護、安全修補發布後多久內必須全數升級（建議 SLA：Critical 7 天、High 30 天）。

---

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

### 【ADR-009 Δ5】N 個部門服務共用同一座 Redis

**決策：所有部門服務共用一座 Redis，鍵不加部門前綴。**

理由：

- 快取內容是**身分驗證結果**（這個 JWT 對應哪個人），不是**授權決策**（這個人能不能用某工具）。身分與部門無關，跨部門共用完全正確。
- 跨部門重用是淨效益：使用者的 JWT 經 `report` gateway 驗證後，該使用者的 agent 呼叫 `spc` gateway 時直接命中快取，省下一次 JWK 驗證。
- 加部門前綴反而使同一個 Token 被驗證 N 次、快取 N 份，浪費容量且增加認證服務負載。

⛔ **禁止**放入共用快取的內容：任何**授權決策**結果（如「agent X 可否呼叫 tool Y」）。授權與部門、工具相關，混入共用命名空間會造成跨部門權限洩漏。若未來需快取授權決策，須另用 `authz:{dept}:{...}` 命名空間並獨立 TTL。

**容量規劃修正**：

| 項目 | 原規劃（單一服務） | 修正後（N 部門共用） |
|------|------------------|-------------------|
| Token 數 | 1,000 | 1,000 × **不變**（同一 Token 只存一份） |
| 容量 | 1 MB | ~1 MB + 20% buffer |
| 連線數 | 1 服務 × pool | **N 服務 × pool** ← 需重估 `maxclients` |

⚠️ **實際需重估的是連線數，不是容量**。原規劃未考慮 N 個服務各自持有連線池。部署前需與 DevOps 確認 Redis `maxclients` 足夠涵蓋（部門數 × 連線池大小 × 副本數）。

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

**【ADR-009 更新】此風險進一步降低**：多部門服務拓撲下，單一部門 gateway 被入侵時，攻擊者僅取得該部門的 NTLM 系統帳號與該部門下游存取能力（採 Δ6 選項 A 時），其他部門不受影響。詳見文件開頭「Δ2 攻擊面 vs 爆炸半徑」。

**⚠️ 但引入新風險**：`McpGateway.Core` 為 N 個服務共用元件，其漏洞影響面為全部部門。緩解見「Δ3 共用元件的新風險」。

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

### 【ADR-009 Δ6】各部門 NTLM 系統帳號：獨立 vs 共用

多服務拓撲下，NTLM 系統帳號有兩種配置。**建議採「各部門獨立」，但需安全團隊裁示**（涉及 AD 帳號管理成本）。

| 面向 | A. 各部門獨立帳號（建議） | B. 全部門共用一組 |
|------|------------------------|-----------------|
| 入侵爆炸半徑 | ✅ 僅該部門下游權限 | ❌ 全公司舊系統存取權 |
| 下游稽核可辨識性 | ✅ 舊系統日誌看得出是哪個部門 | ❌ 全部顯示同一帳號 |
| 最小權限原則 | ✅ 每帳號只授該部門所需 | ❌ 需授聯集權限（過寬） |
| AD 帳號管理成本 | ⚠️ N 個帳號、N 組 90 天 Rotate | ✅ 1 個帳號 |
| Rotate 作業量 | ⚠️ N 次 | ✅ 1 次 |

**建議 A 的關鍵理由**：選項 B 的「授聯集權限」直接違反本 ADR 已承諾的最小權限原則 —— `report` gateway 會持有存取 SPC 舊系統的能力，即使它一支相關工具都沒有。

```yaml
# 選項 A：每部門一份 Secret
apiVersion: v1
kind: Secret
metadata:
  name: mcpgw-report-secrets      # spc → mcpgw-spc-secrets
  namespace: mcp-gateway
type: Opaque
data:
  NTLM_SERVICE_ACCOUNT: <base64>  # svc_mcpgw_report
  NTLM_SERVICE_PASSWORD: <base64>
```

**Rotate 作業量緩解**：N 組帳號的 90 天 Rotate 可由平台團隊以自動化腳本統一執行，不需各部門自行處理。若 DevOps 評估自動化不可行，再退回選項 B 並接受權限過寬的風險登記。

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

    // ── 因 ADR-009 多服務拓撲新增（Δ4）──────────────────────
    public string Department { get; set; }       // 哪個部門 gateway（report / spc / qc）
    public string CoreVersion { get; set; }      // McpGateway.Core 版本，供事故回溯
    // ────────────────────────────────────────────────────

    public Dictionary<string, object> Parameters { get; set; } // 參數（需 PII 遮蔽）
    public bool Success { get; set; }
    public int HttpStatusCode { get; set; }
    public long DurationMs { get; set; }
    public string ErrorMessage { get; set; }
}
```

**為何 `Department` 需獨立欄位**：ADR-009 D6 已強制工具名帶部門前綴（`report_get_status`），部門理論上可從 `ToolName` 字串推導。但稽核查詢需 `GROUP BY` 與索引，字串前綴比對無法有效利用索引，故獨立成欄。

**為何需 `CoreVersion`**：Core 出安全事故時，需快速篩出「哪些部門、在哪段期間、跑的是有漏洞的版本」。此欄位讓事故回溯不必依賴部署紀錄。

**日誌索引建議**（原設計為 `AgentId, ToolName, Timestamp`）：
```
新增索引：Department + Timestamp     ← 部門別稽核報表
         CoreVersion               ← 安全事故回溯
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

-- 【ADR-009 新增】部門別使用量與成功率（部門 owner 的月報）
SELECT department,
       COUNT(*) as call_count,
       AVG(CASE WHEN success THEN 1 ELSE 0 END) as success_rate,
       PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ms) as p95_ms
FROM audit_logs
WHERE timestamp BETWEEN '2026-08-01' AND '2026-08-31'
GROUP BY department

-- 【ADR-009 新增】安全事故回溯：哪些部門在什麼期間跑過有漏洞的 Core 版本
SELECT department, core_version,
       MIN(timestamp) as first_seen,
       MAX(timestamp) as last_seen,
       COUNT(*) as affected_calls
FROM audit_logs
WHERE core_version < '1.2.4'          -- 含修補的最低版本
GROUP BY department, core_version

-- 【ADR-009 新增】跨部門 agent 盤點：哪些 agent 同時使用多個部門
SELECT agent_id, COUNT(DISTINCT department) as dept_count,
       STRING_AGG(DISTINCT department, ', ') as departments
FROM audit_logs
GROUP BY agent_id
HAVING COUNT(DISTINCT department) > 1
```

## 與相關 ADRs 的整合

- **ADR-003**：授權資訊可能影響設定檔格式
- **ADR-004**：稽核日誌需記錄 Tool 版本
- **ADR-005**：授權需支援多版本同時
- **[ADR-009](ADR-009-department-gateway-split.md)**：**拓撲變更來源**。認證實作落於 Core（Δ1）、攻擊面與爆炸半徑（Δ2）、共用元件風險與三層防護（Δ3）、稽核日誌部門維度（Δ4）、Redis 共用（Δ5）、NTLM 帳號配置（Δ6）

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

---

## 🔴 Delta 確認清單（2026-08-02，因 ADR-009 拓撲變更）

**性質**：非全案重審。原核心決策不變，僅確認下列六項因多服務拓撲產生的差異。

### 需安全團隊裁示（阻斷 Sprint 3）

- [ ] **Δ6**：NTLM 系統帳號採「各部門獨立」（建議）或「全部門共用」？
      → 建議 A；若因 AD 帳號管理成本選 B，須登記「權限過寬」風險
- [ ] **Δ3**：Core 安全修補的強制升級 SLA
      → 建議 Critical 7 天、High 30 天；`MIN_SUPPORTED` 由平台團隊維護
- [ ] **Δ2**：確認多服務拓撲的攻擊面評估（N 個端點）可接受

### 需 DevOps 確認（阻斷 Sprint 1）

- [ ] **Δ5**：Redis `maxclients` 是否足以涵蓋 N 個服務的連線池
      → 容量不變（~1MB），需重估的是**連線數**
- [ ] **Δ6**：N 組 NTLM 帳號的 90 天 Rotate 能否自動化

### 需法務/合規確認（阻斷上線，不阻斷開發）

- [ ] **Δ4**：稽核日誌新增 `Department`、`CoreVersion` 欄位是否影響既有合規承諾

### 無需外部確認（已於 ADR-009 定案）

- [x] **Δ1**：認證實作直接落於 `McpGateway.Core`，僅一份實作
- [x] **Δ4**：稽核日誌 schema 新增欄位（本文件已更新）
- [x] **Δ5**：共用 Redis、鍵不加部門前綴、禁止快取授權決策（本文件已更新）

** 是否阻斷開發 **：
- ❌ ** 否 **（可先實作核心邏輯，認證服務用 Stub）
- 建議先實作 JWT + Redis Cache（最常用）
- NTLM 可在 Sprint 2 實作

** 實作時程 **：
- ** 開始日期 **：2026-08-02
- ** Sprint 1 **：JWT + Token Cache（3 人天）
- ** Sprint 2 **：API-KEY + 錯誤處理（3 人天）
- ** Sprint 3 **：NTLM + 整合測試（3 人天）
- ** 目標完成 **：2026-08-21（2 週）

### 🔴 【ADR-009 Δ1】實作落點：直接寫入 `McpGateway.Core`

**本 ADR 的全部認證與稽核實作，必須直接落在 `McpGateway.Core` package，不得先寫進單體服務再抽離。**

原因：

| 若寫進單體再抽離 | 若直接寫入 Core |
|----------------|---------------|
| 額外 5–8 人天重構 | 0 |
| **需重走安全審核**（架構變動） | 已含在本次 delta 確認 |
| 單體時期的捷徑會硬化成邊界 | 無 |

此為 ADR-009 D7「MVP 即採用 Core 結構」的直接後果。結構成本約 2 人天，應併入 Sprint 1 前置作業。

**對本 ADR 三個 Sprint 的影響**：工作內容與人天估算**不變**，僅變更程式碼放置位置與專案結構。

**新增前置作業（Sprint 0，約 2 人天）**：
- [ ] 建立 `McpGateway.Core` 專案骨架
- [ ] 內部 NuGet feed 發布流程（阻斷項，見 ADR-009 待決事項 #2）
- [ ] stdio → ASP.NET Core Streamable HTTP（ADR-001 已決定，此處落地）

---
*批准日期：2026-08-01*
*批准人：安全團隊*

---
*最後更新：2026-07-31*
*狀態：待安全團隊審核*
*審核會議：2026-08-02（週五）*