AI Agent Tool Facade

需求及設計文件

文件版本：v1.1

日期：2026-07-27（v1.0）／2026-08-02（v1.1 修訂）

所屬領域：CIM IT — AI 自動化 / Agent 基礎設施

狀態：待審閱

---

## 修訂紀錄

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-07-27 | 初版草案（單一 Tool Facade 服務） |
| v1.1 | 2026-08-02 | 依 [ADR-009](architecture/adr/ADR-009-department-gateway-split.md) 改為 **Core package + N 部門服務 + ingress 路徑分流**；第 4、6 節重寫；第 7 節開放問題依已定案的 ADR 更新 |

> **v1.1 修訂範圍說明**：第 1–3 節（背景、範圍、需求）與第 5 節（技術注意事項）的**核心論點不變** ——
> API 契約 ≠ Tool 契約、需人工語意封裝層、設定檔與程式結構脫鉤風險。
> 變更集中於**部署拓撲**：單一服務 → 多部門服務 + 共用 package。


# 1. 背景與目的


## 1.1 現況

企業內部 API 由 Ocelot Gateway（.NET）作為統一入口，後端為 Java Spring Boot 與 C#/.NET 組成的多語言（Polyglot）微服務架構。團隊目前以 Semantic Kernel 與 Pydantic AI 開發多支 AI Agent，需要呼叫這些內部 API 作為 Agent 的工具（Tool）。


## 1.2 目的

建立一個獨立的「Tool Facade」服務，以 Model Context Protocol（MCP）對外提供工具，讓內部 AI Agent（及未來其他 MCP 相容的用戶端）能以語意清晰、欄位精簡、易於正確呼叫的方式使用既有 API，同時不更動 Ocelot Gateway 與下游服務既有程式碼。


## 1.3 問題陳述

若採用機械式「OpenAPI／Swagger 自動轉 MCP」的方式，會遇到以下兩個核心問題：

多功能共用端點：企業內部常見以單一 endpoint 搭配 action／type 參數處理多種操作。機械式轉換後，LLM 需自行判斷該填入的列舉值，選擇與填參錯誤率偏高。

查詢／輸出欄位過多：既有 API 常帶有大量 filter 參數與內部稽核／關聯欄位，直接暴露會造成 LLM 填參幻覺與 context 浪費。

因此，API 契約（給人看的 REST 規格）與 Tool 契約（給 LLM 用的呼叫規格）須視為兩種不同的設計標的，不能以自動轉換一步到位，須加入一層人工設計的語意封裝層。


# 2. 範圍


## 2.1 範圍內

Tool Facade 服務本身的功能需求、架構設計、部署方式

Tool 的欄位映射、語意拆解、參數收斂設計原則

設定檔（Base URL／Tool 名稱／描述／參數描述）的管理機制與驗證機制


## 2.2 範圍外

下游 Java／C# 服務本身的重構

Ocelot Gateway 既有路由規則的變更

Semantic Kernel／Pydantic AI 內部 Agent 邏輯設計


# 3. 需求


## 3.1 功能性需求（Functional Requirements）


| 編號 | 需求描述 | 狀態 |
| --- | --- | --- |
| FR-1 | 須可透過設定檔（JSON）指定下游 API 的 Base URL，不同環境（dev/staging/prod）可切換，無需重新編譯。 | ✅ 有效 |
| FR-2 | ~~須可透過設定檔指定每個 Tool 的名稱（name）與描述（description）。~~ | ❌ **已由 [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md) 推翻** —— 改採程式碼內聯（`[McpTool]` + `[Description]`） |
| FR-3 | ~~須可透過設定檔指定每個參數的描述文字；描述路徑以「樹狀字串」對應巢狀欄位。~~ | ❌ **已由 ADR-003 推翻** —— 樹狀路徑與 C# 結構脫鉤風險過高，改採 `[Description]` 標註 |
| FR-4 | 欄位轉換（輸入參數映射、輸出欄位篩選／更名／投影）須以程式（C#）實作，不透過設定檔驅動。 | ✅ 有效 |
| FR-5 | 每個 Tool 對應下游一個或多個 API 呼叫；聚合、條件邏輯、重試策略皆以程式實作。 | ✅ 有效 |
| FR-6 | 服務啟動時須執行驗證，不通過則不予啟動（fail-fast）。 | 🔄 **範圍已變更**（ADR-004）—— 因 ADR-003 選程式碼內聯，不再需要 JSON Schema flatten 與設定檔 diff；改為 Attribute 存在性檢查 + 部門前綴檢查 |
| FR-7 | 服務對外以 MCP 協定（Streamable HTTP）提供服務，供內部 Semantic Kernel／Pydantic AI Agent 或其他 MCP 用戶端呼叫。 | ✅ 有效 |
| **FR-8** | **工具須依部門分組，各部門以獨立 MCP 端點對外提供服務，使單一 Agent 僅載入所需部門的工具。** | 🆕 [ADR-009](architecture/adr/ADR-009-department-gateway-split.md) D1 |
| **FR-9** | **各部門端點須可獨立部署與發版，任一部門發版不得中斷其他部門的 Agent 連線。** | 🆕 ADR-009 D1 |
| **FR-10** | **工具名稱須帶部門前綴（`{dept}_`），並於啟動時強制驗證，防止跨部門同名衝突。** | 🆕 ADR-009 D6 |
| **FR-11** | **Agent 端須僅需認識單一 hostname，換部門僅改路徑。** | 🆕 ADR-009 D4 |

> **FR-2／FR-3 的推翻理由**：這兩項原是設計文件的核心主張（「無需重新編譯即可調整提示詞」）。
> Grilling 過程識別出**雙重維護**與**啟動阻塞**風險（描述文字更新變成破壞性變更），
> 決策改為程式碼內聯。詳見 [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md)，
> 並保留未來演進至混合方案（選項 C）的彈性。


## 3.2 非功能性需求（Non-Functional Requirements）


| 編號 | 需求描述 | 狀態 |
| --- | --- | --- |
| NFR-1 | 服務須可獨立部署於公司封閉內網環境，不依賴公有雲（Azure）服務。 | ✅ 有效 |
| NFR-2 | 服務須與 Ocelot Gateway 解耦，以獨立 process／container 部署，不以 Ocelot middleware 形式存在。 | ✅ 有效（現為 **N 個** container） |
| NFR-3 | ~~設定檔（name／description／參數描述）須納入版本控管（Git），變更須經過 Code Review。~~ | 🔄 **自動達成**（ADR-003）—— 描述文字已內聯於程式碼，本就在 Git 且走 Code Review |
| NFR-4 | 服務須具備可觀測性（logging、Tool 呼叫紀錄），以利除錯與稽核。 | ✅ 有效（須含**部門維度**，ADR-006 Δ4） |
| NFR-5 | 下游 API 認證資訊集中管理，不暴露給 AI 用戶端。 | 🔄 **實作方式已變更**（ADR-006）—— 改採認證代理（Delegating），JWT/API-KEY 由 Client 提供、Gateway 不儲存；僅 NTLM 系統帳號由 K8s Secret 管理 |
| **NFR-6** | **橫向關切（認證、稽核、傳輸、驗證）須僅實作一份，部門專案不得也無法自行實作或略過。** | 🆕 [ADR-009](architecture/adr/ADR-009-department-gateway-split.md) D2 |
| **NFR-7** | **共用元件（Core package）的安全修補須有可量測、可強制的採納機制，不依賴各部門自律。** | 🆕 ADR-009 D3 |
| **NFR-8** | **新部門從零到部署一個 Gateway 應 ≤ 1 人天。** | 🆕 ADR-002 成功標準／ADR-009 D2 驗收指標 |


# 4. 架構設計


## 4.1 整體架構


> **v1.1 變更**：原設計為單一 Tool Facade 服務。因兩個驅動力 ——
> **① LLM 準確率**（工具數量成長至 30+ 後選錯率明顯上升）與
> **② 部署獨立**（單一服務下，任一部門發版會中斷所有 Agent 連線）——
> 改為多部門服務架構。詳見 [ADR-009](architecture/adr/ADR-009-department-gateway-split.md)。

```
  Agent 端只需認識一個 hostname，換部門僅改路徑

 ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
 │ Semantic Kernel  │ │  Pydantic AI     │ │  其他 MCP Client  │
 │ Agent            │ │  Agent           │ │                  │
 │ 連 /report       │ │  連 /spc         │ │  連 /report,/qc  │
 └────────┬─────────┘ └────────┬─────────┘ └────────┬─────────┘
          └────────────────────┼────────────────────┘
                               │ MCP（Streamable HTTP）
                               ▼
                 ┌─────────────────────────────┐
                 │  Ingress（路徑分流）          │  mcp.corp.local
                 └──────────────┬──────────────┘
         /report                │ /spc              /qc
      ┌─────────────────────────┼─────────────────────────┐
      ▼                         ▼                         ▼
┌───────────────┐      ┌───────────────┐      ┌───────────────┐
│ McpGateway    │      │ McpGateway    │      │ McpGateway    │
│   .Report     │      │   .Spc        │      │   .Qc         │
│               │      │               │      │               │
│ ITool 類別    │      │ ITool 類別    │      │ ITool 類別    │  ← 部門專案
│ Program.cs×3行│      │ Program.cs×3行│      │ Program.cs×3行│    （僅工具邏輯）
│ ┌───────────┐ │      │ ┌───────────┐ │      │ ┌───────────┐ │
│ │McpGateway │ │      │ │McpGateway │ │      │ │McpGateway │ │  ← 共用 package
│ │  .Core    │ │      │ │  .Core    │ │      │ │  .Core    │ │    （橫向關切）
│ │ 認證/稽核 │ │      │ │ 認證/稽核 │ │      │ │ 認證/稽核 │ │
│ │ 傳輸/驗證 │ │      │ │ 傳輸/驗證 │ │      │ │ 傳輸/驗證 │ │
│ └───────────┘ │      │ └───────────┘ │      │ └───────────┘ │
└───────┬───────┘      └───────┬───────┘      └───────┬───────┘
        │  獨立 container      │  獨立發版           │  故障隔離
        └──────────────────────┼──────────────────────┘
                               │ HTTP/JSON（一般 API consumer）
                               ▼
                    ┌─────────────────────┐
                    │   Ocelot Gateway     │  ← 不修改既有程式碼與路由
                    └──────────┬───────────┘
                     ┌─────────┴─────────┐
                     ▼                   ▼
         [Java Spring Boot 服務]   [C#/.NET 服務]

  共用基礎設施：Redis（Token Cache）／JWKS 服務／API-KEY 服務／日誌 sink
```

### 4.1.1 兩個切分軸

| 切分 | 內容 | 目的 |
|------|------|------|
| **垂直（部門）** | `McpGateway.Report` / `.Spc` / `.Qc` | LLM 準確率、部署獨立、故障隔離 |
| **水平（框架 vs 內容）** | `McpGateway.Core` vs 部門專案 | 橫向關切僅一份實作，部門專案極薄 |

**部門專案的完整內容**：`ITool` 類別 + 3 行 `Program.cs` + `appsettings.json`。
認證、稽核、傳輸、驗證全在 Core，部門開發者**無需理解、也無法略過**（NFR-6）。


## 4.2 元件設計


### 4.2.1 MCP Server 主體（位於 `McpGateway.Core`）

採用官方 ModelContextProtocol（.NET SDK）搭配 ModelContextProtocol.AspNetCore，於 ASP.NET Core Minimal API 專案中以 Streamable HTTP 對外提供服務。

**v1.1 變更**：此段程式碼位於 `McpGateway.Core` package，**僅實作一份**，各部門服務引用之。
部門專案的 `Program.cs` 僅需三行：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMcpGateway();                    // 傳輸 + 認證 + 稽核 + 驗證
builder.AddToolsFromAssembly<Program>();    // 掃描本組件內的工具
await builder.Build().RunMcpGatewayAsync();
```

⚠️ **待驗證**：`ModelContextProtocol.AspNetCore` 是否支援將 MCP 端點掛載於**路徑前綴**
（`/report`、`/spc`）。此為 4.1 架構圖的前提，須於開發 Sprint 0 以可運行 spike 確認。
若不支援，需退回「純 hostname 區分」方案（ADR-009 D4 選項 2）。


### 4.2.2 Tool 註冊方式：Attribute 標註 + 組件掃描

> **🔄 v1.1 重大簡化**：v1.0 主張採「動態註冊」，理由是
> 「`[McpServerTool]` 的 Name／Description 為編譯期常數，無法綁定執行期讀入的設定值」。
> 此理由**已隨 [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md) 消失** ——
> ADR-003 決定描述文字採**程式碼內聯**（推翻 FR-2／FR-3），
> 既然描述本來就是編譯期常數，Attribute 的限制便不再是問題。
>
> **結果**：實作大幅簡化，不需要 programmatic Tool 建構 API，
> 也不需要「將設定內容套用至 JSON Schema」的覆寫邏輯。

**修訂後的流程**：

1. 部門開發者以 C# 類別定義 Tool：強型別 `record` 輸入輸出 + 核心邏輯（呼叫下游、欄位投影）
2. 以 `[McpTool("{dept}_{intent}")]` 標註工具名，以 `[Description]` 標註工具與各參數說明
3. Core 於啟動時掃描部門組件，找出所有標註的工具
4. Core 執行啟動驗證（Attribute 完整性 + **部門前綴** + 名稱唯一性），不通過即 fail-fast
5. 驗證通過後註冊進 MCP Server，SDK 自動由強型別 record 產生 JSON Schema

**保留的演進空間**：若未來升級至 ADR-003 選項 C（設定檔覆寫描述），
屆時才需要在註冊前插入覆寫合併邏輯。合併邏輯實作於 Core，各部門不需重複實作。


### 4.2.3 下游呼叫（位於 `McpGateway.Core`）

透過具名 HttpClient（可設定 Base URL、Timeout、重試策略）呼叫 Ocelot Gateway，再由 Ocelot 依既有路由規則轉發至 Java／C# 服務。部門 Gateway 對 Ocelot 而言是一個一般的 API consumer，不需修改 Ocelot 既有程式碼。

**v1.1 補充**：

- HttpClient 由 Core 提供（`IDownstreamClient`），部門工具**不得**自行建立 HttpClient
- Core 自動注入認證後的身分標頭（`X-User-Id`、`X-User-Department`、`X-User-Role`、
  `X-Gateway-Department`），原始 Token **不轉發**至下游（[ADR-006](architecture/adr/ADR-006-security-model.md) Delegating Pattern）
- 重試僅套用於冪等方法（GET/PUT/DELETE），POST 預設不重試以避免重複建單


### 4.2.4 共用元件的版本治理（v1.1 新增）

`McpGateway.Core` 為 N 個部門服務的共同依賴，其漏洞或缺陷會同時影響全部部門。
採三層防護確保修補能實際進入生產（[ADR-009](architecture/adr/ADR-009-department-gateway-split.md) D3）：

| 層 | 機制 | 作用 |
|----|------|------|
| 1 | 浮動版本範圍 `[1.2,2.0)` | 部門下次 rebuild 自動取得 patch／minor |
| 2 | CI 閘門檢查 `MIN_SUPPORTED` | 版本過舊直接 fail build，不靠部門自律 |
| 3 | 執行期 metric `mcpgw_core_version{dept}` | 平台團隊可見哪個部門落後，可主動催辦 |


## 4.3 設定檔 Schema

> **🔄 v1.1 全面改寫**：v1.0 的 `tools-config.json` 以設定檔承載 Tool 的
> `name`／`description`／參數描述（樹狀路徑）。此設計已由
> [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md) 推翻（見 FR-2／FR-3 狀態），
> 理由為雙重維護負擔與啟動阻塞風險。
>
> 描述文字現以 `[Description]` 內聯於程式碼；設定檔僅保留**環境相依**與**基礎設施**設定。
> 樹狀路徑字串（`filter.dateRange.start`）機制**整個取消** —— 5.2 節所述的脫鉤風險亦隨之消失。

### 4.3.1 部門專案 `appsettings.json`

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

### 4.3.2 欄位說明

| 欄位 | 說明 |
|------|------|
| `Department` | **必填**。部門代號，工具名前綴驗證依賴此值（FR-10）。缺少即啟動失敗 |
| `RoutePrefix` | MCP 端點掛載路徑，應等於 `/{Department}`。不一致時記 warning |
| `Ocelot.BaseUrl` | 下游 Ocelot Gateway 位址（原 FR-1，唯一保留的 v1.0 設定項） |
| `Auth.*` | 認證服務端點與逾時（ADR-006） |
| `TokenCache.*` | Redis 連線與 TTL 策略。**N 個部門共用同一座 Redis**，鍵不加部門前綴 |
| `Audit.PiiFields` | 需遮蔽的欄位名清單 |

### 4.3.3 設定管理原則

| 原則 | 說明 |
|------|------|
| 秘密不進設定檔 | NTLM 帳密由 K8s Secret 注入環境變數，非 `appsettings.json`（ADR-006） |
| 環境差異靠環境變數覆寫 | `McpGateway__Ocelot__BaseUrl=...`，不維護 dev/staging/prod 三份 json |
| 描述文字不在此 | 全部內聯於程式碼（ADR-003），本檔案不含任何 LLM 可見文字 |

> **與 5.4「Description 文字治理」的關係**：描述文字仍屬 Prompt 的一部分、
> 仍須經 Code Review —— 但因已內聯於 `.cs` 檔，此要求**自動達成**，
> 不需要額外的設定檔治理流程（NFR-3 狀態說明）。


# 5. 技術注意事項與風險


## 5.1 ~~Attribute 為 Compile-time 常數的限制~~ ✅ 已消失

> **v1.1 狀態：此限制不再構成問題。**
>
> v1.0 認為「`[McpServerTool]` 的 Name／Description 為編譯期常數，無法綁定執行期設定值」是個障礙。
> 但 [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md) 決定描述文字**本來就用程式碼內聯**，
> 執行期綁定的需求消失，Attribute 的編譯期常數性質反而成為優點（編譯期檢查、IDE 重構支援）。
>
> **對策更新**：直接採用 Attribute 標註 + 組件掃描（見 4.2.2），不需要動態註冊機制。


## 5.2 ~~設定檔與程式參數結構的脫鉤風險~~ ✅ 已消失

> **v1.1 狀態：風險來源已移除。**
>
> 此風險的前提是「樹狀路徑字串與 C# 參數模型為兩份獨立維護的來源」。
> ADR-003 取消樹狀路徑機制後，描述文字直接標註於參數上，**只剩一份來源**：
>
> ```csharp
> public sealed record GetReportStatusInput(
>     [property: Description("報表工單編號，格式如 RPT-2026-000123")]
>     string JobId);   // ← 參數與描述在同一行，不可能脫鉤
> ```
>
> - **原風險 A（高，靜默失敗）** → 消失。漏標 `[Description]` 會在啟動驗證時 fail-fast，非靜默
> - **原風險 B（低，孤兒路徑）** → 消失。無設定檔即無孤兒路徑
>
> ⚠️ 若未來升級至 ADR-003 選項 C（設定檔覆寫），風險 B 會以較輕的形式回歸，
> 屆時再實作孤兒路徑 warning（[ADR-004](architecture/adr/ADR-004-startup-validation.md) 檢查項 4）。


## 5.3 啟動時驗證機制 🔄 已簡化並擴充

> **v1.1 狀態：範圍變更。**
> 因 5.2 風險消失，原本的「JSON Schema flatten + 設定檔 diff」演算法**不需要了** ——
> 該演算法在 `oneOf`／`array of object`／`additionalProperties` 等邊緣案例本就脆弱
> （[ADR-004](architecture/adr/ADR-004-startup-validation.md) 識別）。
>
> 改為輕量檢查，並因 [ADR-009](architecture/adr/ADR-009-department-gateway-split.md) 新增部門前綴檢查。

服務啟動時執行下列檢查，一次蒐集全部錯誤後統一拋出（方便一次修完），任一失敗即不予啟動：

| # | 檢查項 | 來源 |
|---|--------|------|
| 1 | 類別有 `[McpTool]` 標註 | ADR-003／004 |
| 2 | 輸入 record 所有屬性有 `[Description]` | ADR-003／004 |
| 3 | **工具名以 `{Department}_` 開頭** | **ADR-009 D6（FR-10）** |
| 4 | 工具名在本服務內唯一 | Core 規格 |
| 5 | `Version` 若有值須符合 SemVer | ADR-005 |
| 6 | 孤兒描述覆寫路徑（warning） | ADR-003 選項 C，未實作 |

**檢查 3 的必要性**：Agent 可同時連接多個部門端點。若兩部門各有一支 `get_status`，
LLM 會呼叫到錯誤部門的工具 —— 此為**靜默錯誤**，不拋例外、不留錯誤日誌，
只回傳錯部門的資料，是本專案最難排查的失敗模式之一。故於服務端強制而非依賴 MCP Client 行為。


## 5.4 Description 文字治理 🔄 已自動達成

Tool 與參數的 description 文字本質上屬於 Prompt 的一部分，直接影響 LLM 選擇與填寫 Tool 的正確率，其變更應與程式碼變更同等看待。

> **v1.1 狀態：要求不變，達成方式改變。**
>
> v1.0 要求「設定檔須存放於 Git、變更須經 PR 與 Code Review」。
> ADR-003 將描述內聯至 `.cs` 檔後，此要求**自動滿足** ——
> 描述文字就是程式碼，本來就在 Git、本來就走 Code Review、本來就有 blame 紀錄。
>
> **仍需注意**：改描述雖不需要改設定檔，但**仍需重新建置與發版**。
> 若某部門的描述調整頻率 > 每週一次，即為升級至 ADR-003 選項 C 的觸發條件之一。


## 5.5 Tool 語意拆解與封裝原則 ✅ 完全有效

> **v1.1 狀態：不變。此節是本設計的核心價值主張，未受任何 ADR 影響。**

| 問題類型 | 設計對策 |
| --- | --- |
| 多功能共用端點（action 參數） | 依語意拆成獨立 Tool（如 create_x／update_x／delete_x），即使底層仍呼叫同一支 API。 |
| 輸出欄位過多 | 於 Facade 層進行 DTO Projection，僅保留 Agent 真正需要的欄位；必要時分層設計（摘要 Tool + 明細 drill-down Tool）。 |
| 輸入參數過多 | 收斂為高頻使用情境的 Tool，各自僅保留 3–5 個常用參數，其餘固定值或給預設值。 |
| 需聚合多支 API／條件邏輯／安全性關鍵檢查 | 以程式（C#）手寫實作，不透過設定檔驅動；safety-critical 的確定性檢查須內嵌於程式流程中。 |

### 5.5.1 與部門拆分的關係（v1.1 新增）

本節處理的是「**單一工具**是否好懂」；[ADR-009](architecture/adr/ADR-009-department-gateway-split.md) 的部門拆分處理的是「**工具集合**是否夠小」。兩者互補，缺一不可：

```
工具數少但每支難懂  → LLM 選對工具，填錯參數
工具數多但每支好懂  → LLM 根本選錯工具，填參再對也沒用
兩者都做           → 目標狀態
```

**每部門工具數建議**：5–15 支。超過 30 支時，該部門應考慮再拆分子領域端點。
（此建議值待 Sprint 4 以真實 LLM 測試驗證。）


# 6. 部署架構

> **🔄 v1.1 全面改寫**：v1.0 為單一服務單一部署。現為 Core package + N 個部門服務。

## 6.1 部署形態

以 `McpGateway.Core` NuGet package 承載全部橫向關切，各部門以獨立 ASP.NET Core（Minimal API）專案引用之，**各自獨立 container／process** 部署於公司內網環境。

傳輸協定採用 Streamable HTTP（不使用 stdio），以支援跨機器、多用戶存取。

⚠️ **stdio 已非可行退路**：v1.0 曾將 stdio 列為「若 Streamable HTTP 效能不佳」的備案
（[ADR-001](architecture/adr/ADR-001-use-mcp-protocol.md) 風險表）。多部門 + ingress 拓撲下此退路失效 ——
stdio 無法跨機器、無法路徑分流、無法獨立擴縮。效能問題須改以快取、連線池調校或水平擴充解決。

## 6.2 路由與端點

```
prod（ingress 路徑分流）:
  mcp.corp.local/report  ──▶  report-svc:8080/report
  mcp.corp.local/spc     ──▶  spc-svc:8080/spc
  mcp.corp.local/qc      ──▶  qc-svc:8080/qc

dev:
  localhost:5000/report        （只跑正在開發的那個部門專案）
```

Agent 端僅需認識**一個 hostname**、**一份 TLS 憑證**，換部門只改路徑（FR-11）。
新增部門時，ingress 加一條規則即可，既有 Agent 設定不受影響。

## 6.3 每部門所需部署資源

| 資源 | 說明 |
|------|------|
| Deployment | 部門 gateway container |
| Service | ClusterIP |
| Ingress rule | 一條路徑規則 |
| Secret | `mcpgw-{dept}-secrets`（NTLM 帳密，建議各部門獨立，[ADR-006](architecture/adr/ADR-006-security-model.md) Δ6） |
| ConfigMap | 非敏感設定 |

**共用基礎設施**：Redis（Token Cache）、JWKS 服務、API-KEY 驗證服務、日誌 sink。

⚠️ Redis `maxclients` 需涵蓋「部門數 × 連線池大小 × 副本數」（ADR-006 Δ5）。
原容量規劃以單一服務估算，**需重估的是連線數而非儲存容量**。

## 6.4 與 Ocelot Gateway 的關係

不變：各部門 Gateway 對 Ocelot 而言均為一般的 API consumer，不修改 Ocelot 既有程式碼與路由規則。

## 6.5 與既有 Agent 框架的關係

同一份 Service 方法可視需要同時提供 `[KernelFunction]`（供內部 Semantic Kernel Agent 直接呼叫，無 MCP 傳輸開銷）與 MCP Tool（供外部／跨框架用戶端呼叫）兩種介面，邏輯僅維護一份。

⚠️ **v1.1 注意**：GRILLING-SUMMARY 建議 MVP 階段
「只提供 MCP 介面（無雙重 KernelFunction）」以降低初期複雜度。
雙介面應待 MCP 路徑穩定後再評估，且需注意 `[KernelFunction]` 路徑**繞過 Core 的認證與稽核管線** ——
若採用，需另行確保該路徑的安全與稽核等價性。


# 7. 開放問題／待決事項

## 7.1 v1.0 開放問題的處置狀態

| v1.0 問題 | 狀態 | 說明 |
| --- | --- | --- |
| 是否需要支援多環境設定檔的自動切換機制 | ✅ **已決** | 不做多份設定檔。改以**環境變數覆寫**（`McpGateway__Ocelot__BaseUrl=...`），見 4.3.3 |
| 是否需要建立 tools-config.json 的 CI Schema 驗證工具 | ✅ **已消失** | ADR-003 取消該設定檔。描述文字內聯於程式碼，由編譯器與啟動驗證把關 |
| Tool 數量成長後是否需要管理介面 | 🔄 **已規劃** | [ADR-005](architecture/adr/ADR-005-tool-versioning.md) Phase 3：**全公司加總** > 50 支時建立 Tool Catalog（資料由各部門 gateway 自動上報，非人工維護 YAML） |
| 是否需要針對高頻 Tool 增加快取 | ⏳ **待撰寫 ADR-008** | 已預留編號，優先級 Low。多部門拓撲下需注意快取鍵的部門命名空間 |

## 7.2 v1.1 新增待決事項

| # | 問題 | 阻斷 | 對象 |
| --- | --- | --- | --- |
| 1 | **`ModelContextProtocol.AspNetCore` 是否支援路徑前綴掛載**（4.1 架構圖的前提） | 🔴 全部 | Sprint 0 spike |
| 2 | **SDK preview 版本能否用於生產，GA 時程為何** | 🔴 上線 | Sprint 0 spike |
| 3 | 內部 NuGet feed 是否存在（Core package 發布所需） | 🔴 Sprint 0 | DevOps |
| 4 | 內網 ingress 是否支援路徑分流 | 🔴 Sprint 4 | DevOps |
| 5 | 各部門 NTLM 系統帳號獨立或共用 | 🟡 Sprint 3 | 安全團隊 |
| 6 | `McpGateway.Core` 由哪個團隊擁有與維護（需 bus factor ≥ 2） | 🟡 | 架構團隊 |
| 7 | 部門代號由誰核發、如何避免搶用 | 🟢 | 架構團隊 |
| 8 | 每部門工具數上限建議值（LLM 準確率天花板落在幾支） | 🟢 | Sprint 4 實測 |

## 7.3 ⚠️ 尚未驗證的核心假設

> 本設計文件的效能與正確率主張，**至今無實測數據支撐**。

| 假設 | 現況 |
| --- | --- |
| Streamable HTTP 效能可接受（p95 < 50ms） | 🔴 未驗證。`PoC-REPORT.md:375` 自陳數據為**預估值**；PoC 程式碼無法編譯 |
| 語意封裝可將正確率由 ~60% 提升至 ~92% | 🔴 未驗證。同上，為預估值 |
| .NET MCP SDK 成熟度足夠 | 🔴 未驗證。`ModelContextProtocol.AspNetCore`（Streamable HTTP 必需套件）**從未被引用** |

**行動**：開發 Sprint 0 須先以**可運行的 spike** 補齊上述驗證，Gate 未通過不得推進。
詳見 [開發計畫](specs/development-plan.md) §2。


# 8. 參考資料

Microsoft ModelContextProtocol .NET SDK（NuGet: ModelContextProtocol, ModelContextProtocol.AspNetCore）

Model Context Protocol 官方規範（modelcontextprotocol.io，現由 Linux Foundation 旗下 Agentic AI Foundation 管理）

本文件之討論脈絡與決策理由，詳見《Tool Facade 架構討論與決策紀錄》

## 8.1 本專案文件

| 文件 | 內容 |
| --- | --- |
| [ADR-001](architecture/adr/ADR-001-use-mcp-protocol.md) | MCP 協定選擇；stdio 退路失效；延遲預算待重驗 |
| [ADR-002](architecture/adr/ADR-002-dotnet-mcp-sdk-choice.md) | .NET SDK 選擇；維護範圍論述 |
| [ADR-003](architecture/adr/ADR-003-config-driven-descriptions.md) | **推翻 FR-2／FR-3** —— 描述改為程式碼內聯 |
| [ADR-004](architecture/adr/ADR-004-startup-validation.md) | 啟動驗證簡化 + 部門前綴檢查（本文 5.3） |
| [ADR-005](architecture/adr/ADR-005-tool-versioning.md) | Tool 版本控制；Tool Catalog 觸發條件 |
| [ADR-006](architecture/adr/ADR-006-security-model.md) | 認證代理架構；多服務拓撲的 Δ1–Δ6 |
| [ADR-009](architecture/adr/ADR-009-department-gateway-split.md) | **本文 v1.1 的變更來源** —— 部門別 Gateway 拆分 |
| [Core 規格](specs/mcp-gateway-core-spec.md) | 實作層級的完整契約 |
| [開發計畫](specs/development-plan.md) | Sprint 拆解、相依阻斷、Gate 條件 |
| [GRILLING-SUMMARY](architecture/GRILLING-SUMMARY.md) | 架構審查與風險清單 |
| [glossary](architecture/glossary.md) | 術語表 |
