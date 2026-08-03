# McpGateway.Core 實作規格

**版本**：v0.1（草案）
**日期**：2026-08-02
**狀態**：待 Sprint 0 SDK spike 驗證後定版
**依據**：ADR-001 ~ ADR-006、ADR-009

---

## ⚠️ 本規格的效力範圍

本規格中標示 🔬 的段落，其 API 形狀**依賴 `ModelContextProtocol` .NET SDK 的實際介面**，
而該 SDK 目前為 `0.1.0-preview.*`，且**尚未經任何可運行程式碼驗證**（見 [ADR-001](../architecture/adr/ADR-001-use-mcp-protocol.md) 狀態欄查核發現）。

| 標記 | 意義 |
|------|------|
| 🔬 | **待驗證** —— 形狀依賴 SDK 實際 API，Sprint 0 spike 後可能調整 |
| ✅ | **已定案** —— 由 ADR 決策直接推導，不依賴 SDK 細節 |

**Sprint 0 spike 完成前，不得依本規格的 🔬 段落開始正式實作。**

---

## 1. 範圍

### 1.1 範圍內

`McpGateway.Core` NuGet package 的完整對外契約：公開 API、設定結構、啟動流程、
認證管線、下游呼叫、稽核、可觀測性、錯誤處理、版本相容性規則。

以及部門專案（`McpGateway.{Dept}`）必須遵守的契約。

### 1.2 範圍外

- 各部門的具體工具邏輯（屬部門專案）
- Ocelot Gateway 與下游服務的任何變更
- Agent 端（Semantic Kernel / Pydantic AI）的實作
- Tool Catalog / 管理介面（ADR-005 Phase 3，全公司工具 > 50 才啟動）

---

## 2. 專案結構 ✅

```
src/
  McpGateway.Core/                    ← NuGet package（平台團隊維護）
    Hosting/                            AddMcpGateway / RunMcpGateway
    Tools/                              ITool / ToolBase / McpToolAttribute
    Auth/                               認證代理（ADR-006）
    Downstream/                         Ocelot HttpClient
    Validation/                         啟動驗證（ADR-004）
    Audit/                              稽核日誌 + PII 遮蔽
    Observability/                      metrics / health checks
    Projection/                         欄位投影 helper

  McpGateway.Report/                  ← 首個部門專案
    Program.cs                          3 行
    Tools/                              ITool 實作
    appsettings.json

  MockOcelotApi/                      ← 既有 mock，測試用

tests/
  McpGateway.Core.Tests/              ← Core 單元測試 + 契約測試
  McpGateway.Report.Tests/            ← 部門工具測試
  McpGateway.Core.IntegrationTests/   ← WireMock 模擬 Ocelot
```

**目標框架**：`net9.0`（.NET 9 LTS）
⚠️ 本機目前僅安裝 .NET SDK 3.1.402，**開發環境需先安裝 .NET 9 SDK**。
見 [ADR-002](../architecture/adr/ADR-002-dotnet-mcp-sdk-choice.md) 版本選擇理由。

---

## 3. Core 公開 API 🔬

### 3.1 部門專案的完整 `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddMcpGateway();                    // 讀設定 + 註冊全部橫向服務
builder.AddToolsFromAssembly<Program>();    // 掃描本組件內的 ITool 實作

await builder.Build().RunMcpGatewayAsync(); // 啟動驗證 → 掛路由 → 執行
```

**設計約束**：部門專案不得出現任何認證、稽核、傳輸相關的呼叫。
若部門需要「不裝某項橫向關切」的能力，即代表邊界劃錯，應回頭修 Core 而非開放 opt-out。

### 3.2 擴充方法簽章 🔬

```csharp
namespace McpGateway.Core;

public static class McpGatewayBuilderExtensions
{
    /// <summary>
    /// 註冊 Core 全部服務：認證代理、Token Cache、下游 HttpClient、
    /// 稽核、metrics、health checks、MCP 傳輸層。
    /// 讀取設定區段 "McpGateway"。
    /// </summary>
    public static WebApplicationBuilder AddMcpGateway(
        this WebApplicationBuilder builder,
        string configSection = "McpGateway");

    /// <summary>
    /// 掃描 TMarker 所在組件，找出所有標註 [McpTool] 的 ITool 實作並註冊。
    /// </summary>
    public static WebApplicationBuilder AddToolsFromAssembly<TMarker>(
        this WebApplicationBuilder builder);
}

public static class McpGatewayAppExtensions
{
    /// <summary>
    /// 執行啟動驗證（失敗則拋例外不啟動）→ 掛載 MCP 端點於 RoutePrefix
    /// → 掛載 health check 端點 → 執行。
    /// </summary>
    public static Task RunMcpGatewayAsync(this WebApplication app);
}
```

### 3.3 Tool 契約 🔬

```csharp
/// <summary>標註於 ITool 實作類別，宣告對 LLM 呈現的工具名稱。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class McpToolAttribute : Attribute
{
    public McpToolAttribute(string name) => Name = name;

    /// <summary>格式 {dept}_{intent}[_v{major}]，前綴由啟動驗證強制。</summary>
    public string Name { get; }

    /// <summary>ADR-005 語意化版本。MVP 可省略，MVP+1 導入。</summary>
    public string? Version { get; init; }

    public bool Deprecated { get; init; }
    public string? DeprecationReason { get; init; }
}

/// <summary>強型別工具基底。TInput/TOutput 為 record，供 SDK 產生 JSON Schema。</summary>
public abstract class ToolBase<TInput, TOutput> : ITool
    where TInput : notnull
{
    /// <summary>下游呼叫用戶端，已自動帶入認證後的身分標頭。</summary>
    protected IDownstreamClient Downstream { get; }

    /// <summary>本次呼叫的身分脈絡（ADR-006 認證結果）。</summary>
    protected ToolContext Context { get; }

    protected ILogger Logger { get; }

    public abstract Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct);
}

public sealed record ToolContext(
    string UserId,
    string Department,      // 使用者所屬部門（來自 Token），非 gateway 部門
    string Role,
    string TokenType,       // JWT / ApiKey / NTLM
    string AgentId,
    string CorrelationId);
```

**為何用泛型強型別，而非 `Dictionary<string, object>`**：
design-doc 4.2.2 要求「以 C# 類別／方法定義 Tool 的輸入輸出結構（強型別 record／class）」。
強型別讓 SDK 能自動產生 JSON Schema，且 `[Description]` 標註可掛在 record 屬性上（ADR-003）。

> ⚠️ 現有 PoC 程式碼使用 `Dictionary<string, object>` 且定義了**兩個衝突的 `ITool`**
> （`McpServerHost.cs:68`、`ToolRegistry.cs:49`），該程式碼無法編譯，不作為實作基礎。

### 3.4 工具範例（部門專案）

```csharp
[McpTool("report_get_status")]
public sealed class GetReportStatusTool : ToolBase<GetReportStatusInput, ReportStatus>
{
    public override async Task<ReportStatus> ExecuteAsync(
        GetReportStatusInput input, CancellationToken ct)
    {
        var raw = await Downstream.GetAsync<ReportJobDto>(
            $"/api/report-jobs/{input.JobId}", ct: ct);

        // 欄位投影：只回傳 Agent 需要的欄位（ADR-003 / design-doc 5.5）
        return new ReportStatus(
            Status: raw.StatusCode switch
            {
                0 => "排隊中", 1 => "產製中", 2 => "完成", _ => "失敗"
            },
            EstimatedCompletion: raw.EtaUtc,
            DownloadUrl: raw.StatusCode == 2 ? raw.BlobUrl : null);
    }
}

[Description("""
    查詢報表產製狀態，適合使用者詢問「我的報表跑好了沒」時使用。

    回傳：狀態（排隊中／產製中／完成／失敗）、預估完成時間、
    下載連結（僅狀態為完成時提供）。
    """)]
public sealed record GetReportStatusInput(
    [property: Description("報表工單編號，格式如 RPT-2026-000123")]
    string JobId);

public sealed record ReportStatus(
    string Status, DateTime? EstimatedCompletion, string? DownloadUrl);
```

---

## 4. 設定結構 ✅

`McpGateway.{Dept}/appsettings.json`：

```json
{
  "McpGateway": {
    "Department": "report",
    "RoutePrefix": "/report",

    "Ocelot": {
      "BaseUrl": "https://internal-ocelot.corp.local",
      "TimeoutSeconds": 10,
      "Retry": { "Count": 2, "BackoffMs": 200 }
    },

    "Auth": {
      "SupportedTypes": [ "JWT", "API-KEY" ],
      "JwksEndpoint": "https://auth.corp.local/.well-known/jwks.json",
      "JwksCacheHours": 24,
      "ApiKeyServiceUrl": "https://auth.corp.local/api-key/validate",
      "ApiKeyTimeoutSeconds": 3,
      "SystemAccount": { "Type": "NTLM", "CredentialSource": "Environment" }
    },

    "TokenCache": {
      "Redis": "redis.corp.local:6379",
      "ApiKeyTtlMinutes": 5,
      "JwtExpirySkewMinutes": 1
    },

    "Audit": {
      "Sink": "ApplicationInsights",
      "PiiFields": [ "email", "customerName", "phone", "address" ]
    }
  }
}
```

**設定規則**：

| 規則 | 說明 |
|------|------|
| 秘密不進設定檔 | NTLM 帳密由 K8s Secret 注入環境變數（ADR-006） |
| 環境差異靠環境變數覆寫 | `McpGateway__Ocelot__BaseUrl` 等，不維護多份 json |
| `Department` 為必填 | 缺少即啟動失敗；工具前綴驗證依賴此值 |
| `RoutePrefix` 應等於 `/{Department}` | 不一致時記 warning（允許但不建議） |

---

## 5. 啟動流程與驗證 ✅

```
1. 讀取設定 "McpGateway"，缺 Department → 拋 ConfigurationException
2. 掃描組件，蒐集所有 [McpTool] 標註的 ITool
3. 執行啟動驗證（下表，一次蒐集全部錯誤後統一拋出）
4. 驗證通過 → 向 MCP Server 註冊工具
5. 掛載 MCP 端點於 RoutePrefix
6. 掛載 /health/live 與 /health/ready
7. 上報 mcpgw_core_version metric
8. 開始接受請求
```

### 5.1 啟動驗證清單（ADR-004）

| # | 檢查項 | 失敗行為 | 來源 |
|---|--------|----------|------|
| 1 | 類別有 `[McpTool]` | fail-fast | ADR-003/004 |
| 2 | `TInput` 所有屬性有 `[Description]` | fail-fast | ADR-003/004 |
| 3 | `[McpTool].Name` 以 `{Department}_` 開頭 | fail-fast | **ADR-009 D6** |
| 4 | 工具名在本服務內唯一 | fail-fast | 本規格 |
| 5 | `Version` 若有值，須符合 SemVer | fail-fast | ADR-005 |
| 6 | 孤兒描述覆寫路徑 | warning | ADR-003 選項 C（未實作） |

**錯誤訊息格式**（必須含足夠資訊一次修完）：

```
啟動驗證失敗（3 項）：
  [檢查 3] 工具 'get_status' 未以 'report_' 開頭。
           跨部門同名衝突會導致 LLM 呼叫錯誤部門的工具（靜默錯誤）。
  [檢查 2] GetReportStatusInput.JobId 缺少 [Description] 標註。
  [檢查 4] 工具名 'report_get_status' 重複定義於
           GetReportStatusTool 與 LegacyReportStatusTool。
```

---

## 6. 認證管線 ✅（ADR-006）

### 6.1 請求流程

```
MCP 請求（含 Authorization 標頭）
   │
   ├─ 1. 判斷 Token 類型（JWT / API-KEY / NTLM）
   ├─ 2. 查 Token Cache（Redis，跨部門共用）
   │      命中 → 跳至 4
   ├─ 3. 驗證並提取身分
   │      JWT     → JWKS Public Key 驗證，取 sub/department/role
   │      API-KEY → 呼叫 API-KEY 服務
   │      NTLM    → 使用本部門系統帳號
   │      → 寫入 Cache
   ├─ 4. 建立 ToolContext
   ├─ 5. 執行工具（下游呼叫自動帶身分標頭）
   └─ 6. 寫稽核日誌
```

### 6.2 Token Cache 規則

```
Key: auth:{tokenType}:{tokenHash}        ← 不含部門，跨部門共用（ADR-006 Δ5）
TTL: JWT     → exp - now - 1min
     API-KEY → 5 分鐘（可設定）
     NTLM    → 不快取
```

⛔ **禁止**在此命名空間放入任何**授權決策**結果。授權與部門、工具相關，
混入共用空間會造成跨部門權限洩漏。未來若需快取授權，另用 `authz:{dept}:{...}`。

### 6.3 下游身分傳遞

Core 自動於每個下游請求注入，**部門工具無需也無法干預**：

```
X-User-Id: user123
X-User-Department: Sales
X-User-Role: Manager
X-Auth-Type: JWT
X-Correlation-Id: <guid>
X-Gateway-Department: report        ← 本 gateway 部門，供下游稽核
```

⚠️ 原始 Token **不轉發**至下游（ADR-006 Delegating Pattern）。

### 6.4 降級策略

| 情境 | 行為 |
|------|------|
| API-KEY 服務逾時，快取有值 | 使用快取，記 warning |
| API-KEY 服務逾時，快取無值 | 回 503 |
| JWKS 取得失敗，有快取金鑰 | 使用快取金鑰，記 warning |
| JWKS 取得失敗，無快取金鑰 | 回 503 |
| Redis 不可用 | 降級為直接呼叫認證服務（不快取），記 warning |

---

## 7. 下游呼叫 ✅

```csharp
public interface IDownstreamClient
{
    Task<T> GetAsync<T>(string path, object? query = null, CancellationToken ct = default);
    Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default);
    Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}
```

- Base URL、Timeout、重試策略由設定決定，工具不得覆寫
- 重試僅適用於**冪等**方法（GET/PUT/DELETE）與可重試狀態碼（408/429/5xx）
- POST 預設**不重試**（避免重複建單）；若某工具的下游確實冪等，於工具內顯式處理

---

## 8. 稽核日誌 ✅（ADR-006）

```csharp
public sealed class ToolInvocationAuditLog
{
    public DateTimeOffset Timestamp { get; init; }
    public string AgentId { get; init; }
    public string ToolName { get; init; }
    public string? ToolVersion { get; init; }
    public string Department { get; init; }        // ADR-009 Δ4
    public string CoreVersion { get; init; }       // ADR-009 Δ4
    public string CorrelationId { get; init; }
    public Dictionary<string, object?> Parameters { get; init; }  // 已 PII 遮蔽
    public bool Success { get; init; }
    public int? HttpStatusCode { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
```

**PII 遮蔽**：依 `Audit:PiiFields` 設定遞迴遍歷參數物件，命中欄位名即遮蔽。

```
email        → c***@example.com
customerName → 王**
phone        → 09**-***-123
其他字串     → 保留前 2 字元 + ***
```

⚠️ 遮蔽發生在**寫入日誌前**，原始值不得進入任何 sink。

---

## 9. 錯誤處理 ✅

### 9.1 回傳給 LLM 的錯誤

| 情境 | 回傳內容 | 詳細資訊 |
|------|----------|----------|
| 參數驗證失敗 | 明確說明哪個參數不合法、期望格式 | 可完整回傳（有助 LLM 自我修正） |
| 下游 4xx | 語意化訊息（如「查無此訂單編號」） | **不回傳**下游原始錯誤 |
| 下游 5xx / 逾時 | 「下游服務暫時無法使用，請稍後再試」 | **不回傳**堆疊或內部位址 |
| 認證失敗 | 「認證失敗」 | **不回傳**原因細節 |
| 未預期例外 | 「工具執行失敗」+ CorrelationId | **不回傳**任何內部資訊 |

⚠️ **安全要求（ADR-006）**：詳細錯誤一律只進稽核日誌，不回傳給 LLM。
下游錯誤訊息可能含內部路徑、SQL 片段、其他使用者資料，且 LLM 會將其複述給終端使用者，
構成資訊洩漏與提示詞注入風險。

### 9.2 CorrelationId

每次工具呼叫產生一組，同時出現於：回傳給 LLM 的錯誤訊息、稽核日誌、下游請求標頭。
使用者回報問題時可據此串連完整鏈路。

---

## 10. 可觀測性 ✅

### 10.1 Metrics

| Metric | 標籤 | 用途 |
|--------|------|------|
| `mcpgw_core_version` | `dept`, `version` | **ADR-009 D3 第 3 層** —— 版本漂移可見度 |
| `mcpgw_tool_calls_total` | `dept`, `tool`, `status` | 使用量與成功率 |
| `mcpgw_tool_duration_seconds` | `dept`, `tool` | 端到端延遲（histogram） |
| `mcpgw_downstream_duration_seconds` | `dept`, `tool` | 下游耗時（隔離 gateway 自身開銷） |
| `mcpgw_auth_failures_total` | `dept`, `reason` | 認證異常偵測 |
| `mcpgw_token_cache_total` | `dept`, `result`(hit/miss) | 快取效益 |

### 10.2 Health Checks

```
GET /health/live   → 行程存活（永遠 200，除非 process 已死）
GET /health/ready  → 相依檢查：Redis 可連線 + JWKS 可取得
                     未就緒回 503，供 K8s 暫停導流
```

---

## 11. 版本與相容性 ✅（ADR-009 D3）

### 11.1 部門專案引用方式

```xml
<PackageReference Include="McpGateway.Core" Version="[1.2,2.0)" />
```

### 11.2 Core 版本語意

| 變更類型 | 版號 | 範例 |
|----------|------|------|
| MAJOR | 破壞性 | 移除公開 API、新增會使既有工具無法啟動的驗證規則 |
| MINOR | 向後相容新增 | 新增 helper、新增可選設定 |
| PATCH | 修正 | bug fix、安全修補、不改介面 |

⚠️ **新增啟動驗證規則屬破壞性變更**：既有部門工具可能因此無法啟動。
若必須新增，先以 warning 發布一個 MINOR 版本，下個 MAJOR 才改為 fail-fast。

### 11.3 CI 閘門（ADR-009 D3 第 2 層）

共用 build template 檢查解析後的 Core 版本：

```
if (resolvedCoreVersion < MIN_SUPPORTED) → fail build
```

`MIN_SUPPORTED` 由平台團隊維護，安全修補發布時上調。
建議 SLA：Critical 7 天內全部門升級、High 30 天（待安全團隊確認，ADR-006 Δ3）。

---

## 12. 部署 ✅

### 12.1 容器與路由

```
prod:
  mcp.corp.local/report  ──ingress──▶  report-svc:8080/report
  mcp.corp.local/spc     ──ingress──▶  spc-svc:8080/spc

dev:
  localhost:5000/report   （只跑正在改的那個專案）
```

### 12.2 每部門需要的部署資源

| 資源 | 說明 |
|------|------|
| Deployment | 部門 gateway container |
| Service | ClusterIP |
| Ingress rule | 一條路徑規則 |
| Secret | `mcpgw-{dept}-secrets`（NTLM 帳密，建議各部門獨立，ADR-006 Δ6） |
| ConfigMap | 非敏感設定 |

**共用資源**：Redis（Token Cache）、JWKS 服務、API-KEY 服務、日誌 sink。

⚠️ Redis `maxclients` 需涵蓋「部門數 × 連線池大小 × 副本數」（ADR-006 Δ5）。

---

## 13. 測試要求

> 完整測試策略待 ADR-007 撰寫。本節為 Core 的最低要求。

| 層級 | 範圍 | 工具 |
|------|------|------|
| 單元測試 | 啟動驗證各檢查項、PII 遮蔽、欄位投影、Token Cache TTL 計算 | xUnit |
| 契約測試 | **Core 改動不得破壞既有部門專案** —— 以測試用部門專案驗證 | xUnit |
| 整合測試 | 認證管線 + 下游呼叫（WireMock 模擬 Ocelot 與認證服務） | WireMock.Net |
| E2E | 完整 MCP 呼叫鏈，至少 1 個工具 | MCP client |
| 效能 | 端到端 p95（含與不含 ingress 兩組） | k6 |

**契約測試為 Core 的核心防線**：N 個部門依賴 Core，破壞性變更若未在 CI 攔下，
會同時打壞所有部門。測試專案中應維護一個最小部門專案作為契約基準。

---

## 14. 部門開發者快速上手

> 目標：從零到部署一個 gateway ≤ 1 人天（ADR-002 成功標準）。

1. `dotnet new web -n McpGateway.{Dept}`
2. 加入 `<PackageReference Include="McpGateway.Core" Version="[1.2,2.0)" />`
3. 貼上 3 行 `Program.cs`（§3.1）
4. 填 `appsettings.json` 的 `Department` 與 `Ocelot:BaseUrl`（§4）
5. 寫工具：`ToolBase<TInput, TOutput>` + `[McpTool("{dept}_...")]` + `[Description]`（§3.4）
6. `dotnet run` → 啟動驗證會告訴你漏了什麼
7. 部署：container + ingress 路徑規則一行

**部門開發者不需要理解**：MCP 協定、認證管線、Token Cache、稽核日誌、傳輸層。

---

## 15. 未決事項

| # | 項目 | 阻斷 | 對象 |
|---|------|------|------|
| 1 | SDK 實際的動態 Tool 註冊 API（§3 全部 🔬 段落） | 🔴 全部實作 | Sprint 0 spike |
| 2 | `ModelContextProtocol.AspNetCore` 是否支援路徑前綴 | 🔴 ADR-009 D4 前提 | Sprint 0 spike |
| 3 | preview 版本能否用於生產，GA 時程 | 🟡 上線 | Sprint 0 spike |
| 4 | 內部 NuGet feed | 🔴 package 發布 | DevOps |
| 5 | Ingress 路徑分流能力 | 🔴 Phase 2 部署 | DevOps |
| 6 | Redis `maxclients` | 🟡 Sprint 1 | DevOps |
| 7 | NTLM 帳號各部門獨立 vs 共用 | 🟡 Sprint 3 | 安全團隊 |
| 8 | `MIN_SUPPORTED` 維護者與升級 SLA | 🟢 | 平台 + 安全 |
| 9 | 部門代號核發規則 | 🟢 | 架構團隊 |

---

*最後更新：2026-08-02*
*相關文件：[ADR-009](../architecture/adr/ADR-009-department-gateway-split.md)、[開發計畫](development-plan.md)*
