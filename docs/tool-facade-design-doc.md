AI Agent Tool Facade

需求及設計文件

文件版本：v1.0（草案）

日期：2026-07-27

所屬領域：CIM IT — AI 自動化 / Agent 基礎設施

狀態：待審閱


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


| 編號 | 需求描述 |
| --- | --- |
| FR-1 | 須可透過設定檔（JSON）指定下游 API 的 Base URL，不同環境（dev/staging/prod）可切換，無需重新編譯。 |
| FR-2 | 須可透過設定檔指定每個 Tool 的名稱（name）與描述（description）。 |
| FR-3 | 須可透過設定檔指定每個參數的描述文字；描述路徑以「樹狀字串」（如 filter.dateRange.start）對應到參數結構中的巢狀欄位。 |
| FR-4 | 欄位轉換（輸入參數映射、輸出欄位篩選／更名／投影）須以程式（C#）實作，不透過設定檔驅動。 |
| FR-5 | 每個 Tool 對應下游一個或多個 API 呼叫；聚合、條件邏輯、重試策略皆以程式實作。 |
| FR-6 | 服務啟動時，須驗證設定檔中參數描述路徑與程式中實際參數結構的一致性（詳見 5.3）。 |
| FR-7 | 服務對外以 MCP 協定（Streamable HTTP）提供服務，供內部 Semantic Kernel／Pydantic AI Agent 或其他 MCP 用戶端呼叫。 |


## 3.2 非功能性需求（Non-Functional Requirements）


| 編號 | 需求描述 |
| --- | --- |
| NFR-1 | 服務須可獨立部署於公司封閉內網環境，不依賴公有雲（Azure）服務。 |
| NFR-2 | 服務須與 Ocelot Gateway 解耦，以獨立 process／container 部署，不以 Ocelot middleware 形式存在。 |
| NFR-3 | 設定檔（name／description／參數描述）須納入版本控管（Git），變更須經過 Code Review。 |
| NFR-4 | 服務須具備可觀測性（logging、Tool 呼叫紀錄），以利除錯與稽核。 |
| NFR-5 | 下游 API 認證資訊（如 API Key／OAuth Token）集中於 Tool Facade 管理，不暴露給 AI 用戶端。 |


# 4. 架構設計


## 4.1 整體架構


```json
[Semantic Kernel Agent]  [Pydantic AI Agent]  [其他 MCP Client]
            │                  │                    │
            └──────────────────┴───────MCP───────────┘
                               │ (Streamable HTTP)
                               ▼
                    ┌─────────────────────┐
                    │   Tool Facade        │  ← 獨立服務／獨立部署
                    │  (.NET MCP SDK)       │
                    │  - 動態註冊 Tool       │
                    │  - 欄位轉換/投影(C#)   │
                    │  - 讀取 tools-config   │
                    └──────────┬───────────┘
                               │ HTTP/JSON（一般 API consumer）
                               ▼
                    ┌─────────────────────┐
                    │   Ocelot Gateway      │
                    └──────────┬───────────┘
                     ┌─────────┴─────────┐
                     ▼                   ▼
         [Java Spring Boot 服務]   [C#/.NET 服務]
```


## 4.2 元件設計


### 4.2.1 MCP Server 主體

採用官方 ModelContextProtocol（.NET SDK）搭配 ModelContextProtocol.AspNetCore，於 ASP.NET Core Minimal API 專案中以 Streamable HTTP 對外提供服務。


### 4.2.2 Tool 註冊方式：動態註冊（非純 Attribute）

由於 [McpServerTool] 等 Attribute 的 Name／Description 為編譯期常數，無法直接綁定執行期讀入的設定值，因此採用 SDK 提供的 programmatic（動態）註冊 API，流程如下：

以 C# 類別／方法定義 Tool 的輸入輸出結構（強型別 record／class）與核心邏輯（呼叫下游 API、欄位轉換）。

服務啟動時讀取 tools-config.json，取得每個 Tool 的 name、description、baseUrl 與參數描述。

以 SDK 提供的 Tool 建構 API，將設定內容套用至該 Tool 產生的 JSON Schema（含 description 欄位覆寫），再註冊進 MCP Server。


### 4.2.3 下游呼叫

透過具名 HttpClient（可設定 Base URL、Timeout、重試策略）呼叫 Ocelot Gateway，再由 Ocelot 依既有路由規則轉發至 Java／C# 服務。Tool Facade 對 Ocelot 而言是一個一般的 API consumer，不需修改 Ocelot 既有程式碼。


## 4.3 設定檔（tools-config.json）Schema 草案


```json
{
  "baseUrl": "https://internal-ocelot.corp.local",
  "tools": [
    {
      "id": "get_order_status",
      "name": "get_order_status",
      "description": "查詢訂單目前狀態，適合客服人員確認訂單進度時使用",
      "descriptions": {
        "order_id": "欲查詢的訂單編號，格式如 SO-2026-000123",
        "filter.dateRange.start": "查詢區間起始日期（YYYY-MM-DD）",
        "filter.dateRange.end": "查詢區間結束日期（YYYY-MM-DD）"
      }
    }
  ]
}
```

欄位說明：

baseUrl：下游 Ocelot Gateway 位址，依環境（dev/staging/prod）切換不同設定檔或環境變數覆寫。

tools[].id：對應程式中該 Tool 的實作識別碼（非對外顯示）。

tools[].name / description：對外呈現給 LLM 的 Tool 名稱與說明文字。

tools[].descriptions：以「樹狀字串路徑」為 Key，對應巢狀參數結構中各欄位的說明文字。


# 5. 技術注意事項與風險


## 5.1 Attribute 為 Compile-time 常數的限制

[McpServerTool] 之 Name／Description 屬性於編譯時期即固定，無法直接綁定執行期讀入的設定值。因應對策：不採用純 Attribute 驅動，改採 4.2.2 所述之動態註冊方式，Attribute（如有使用）僅作為參數型別／結構的來源，實際對外呈現的 name/description 一律於啟動時由設定檔套用覆寫。


## 5.2 設定檔與程式參數結構的脫鉤風險

樹狀路徑字串（如 filter.dateRange.start）與 C# 參數模型（record／class）為兩份獨立維護的來源，並無編譯期檢查機制確保兩者一致，可能發生：

風險 A（高）：參數結構新增欄位，但設定檔未同步更新 → 該參數在 MCP Schema 中無描述文字，LLM 容易漏填或臆測填值。此為靜默失敗，不會有明確錯誤訊息，風險最高。

風險 B（低）：設定檔中存在孤兒路徑（已無對應參數）→ 累積設定檔技術債，但不影響實際運作。


## 5.3 啟動時驗證機制（緩解 5.2 風險）

服務啟動時執行以下檢查流程，將設定與程式脫鉤的風險降至接近編譯期檢查的水準：

攤平（flatten）每個 Tool 實際產生的 JSON Schema，取得該 Tool 所有參數的完整路徑清單。

將上述清單與設定檔中對應 Tool 的 descriptions 路徑清單進行差異比對（diff）。

若發現「參數存在但無描述」→ 判定為驗證失敗，服務不予啟動（fail-fast），並輸出明確錯誤訊息（含 Tool 名稱與缺漏的參數路徑）。

若發現「描述存在但無對應參數」（孤兒路徑）→ 僅記錄 warning log，不阻擋服務啟動。


## 5.4 Description 文字治理

Tool 與參數的 description 文字本質上屬於 Prompt 的一部分，直接影響 LLM 選擇與填寫 Tool 的正確率，其變更應與程式碼變更同等看待，具體要求：

設定檔須存放於 Git 版控（與服務程式碼同一或對應的 repository）。

設定檔變更須經過 Pull Request 與 Code Review 流程，不應成為無人管理、可隨意修改的環境設定檔。


## 5.5 Tool 語意拆解與封裝原則


| 問題類型 | 設計對策 |
| --- | --- |
| 多功能共用端點（action 參數） | 依語意拆成獨立 Tool（如 create_x／update_x／delete_x），即使底層仍呼叫同一支 API。 |
| 輸出欄位過多 | 於 Facade 層進行 DTO Projection，僅保留 Agent 真正需要的欄位；必要時分層設計（摘要 Tool + 明細 drill-down Tool）。 |
| 輸入參數過多 | 收斂為高頻使用情境的 Tool，各自僅保留 3–5 個常用參數，其餘固定值或給預設值。 |
| 需聚合多支 API／條件邏輯／安全性關鍵檢查 | 以程式（C#）手寫實作，不透過設定檔驅動；safety-critical 的確定性檢查須內嵌於程式流程中。 |


# 6. 部署架構

以獨立 ASP.NET Core（Minimal API）專案實作，獨立 container／process 部署於公司內網環境。

傳輸協定採用 Streamable HTTP（不使用 stdio），以支援跨機器、多用戶存取。

與 Ocelot Gateway 的關係：Tool Facade 為 Ocelot 的一般 API consumer，不修改 Ocelot 既有程式碼與路由規則。

與既有 Agent 框架的關係：同一份 Service 方法可視需要同時提供 [KernelFunction]（供內部 Semantic Kernel Agent 直接呼叫，無 MCP 傳輸開銷）與 MCP Tool（供外部／跨框架用戶端呼叫）兩種介面，邏輯僅維護一份。


# 7. 開放問題／待決事項

是否需要支援多環境（dev/staging/prod）設定檔的自動切換機制（如以環境變數指定載入的 config 檔案）。

是否需要建立 tools-config.json 的 Schema 驗證工具，於 CI 階段即檢查設定檔格式正確性，而非僅於服務啟動時驗證。

未來 Tool 數量成長後（例如超過數十支），是否需要建立管理介面，取代目前純手動編輯 JSON 的方式。

是否需要針對高頻呼叫的 Tool 增加快取機制，降低對下游 Ocelot／API 的重複請求量。


# 8. 參考資料

Microsoft ModelContextProtocol .NET SDK（NuGet: ModelContextProtocol, ModelContextProtocol.AspNetCore）

Model Context Protocol 官方規範（modelcontextprotocol.io，現由 Linux Foundation 旗下 Agentic AI Foundation 管理）

本文件之討論脈絡與決策理由，詳見《Tool Facade 架構討論與決策紀錄》
