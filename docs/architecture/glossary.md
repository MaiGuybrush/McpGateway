# 🤖 AI Agent Tool Facade - 術術語表

## 核心概念

### **Tool Facade**
作為 AI Agent 與企業內部 API 之間的適配層。它使用 MCP 協定對外暴露工具，負責：
- 語意封裝（將技術 API 轉為 LLM 友好的工具）
- 欄位轉換與投影
- 參數收斂（簡化複雜參數）
- 認證管理

**同義詞**：Tool Gateway, AI Tool Adapter, MCP Server

> **【ADR-009 更新】不再是單一服務**。自 [ADR-009](adr/ADR-009-department-gateway-split.md) 起，
> Tool Facade 為一組服務的統稱：**一個共用 `McpGateway.Core` package + N 個部門 Gateway 服務**。
> 見下方「Core Package」與「部門 Gateway」詞條。

---

### **Core Package（`McpGateway.Core`）**
所有部門 Gateway 共用的 NuGet package，承擔全部橫向關切：

- MCP Host bootstrap（ASP.NET Core + Streamable HTTP）
- 認證代理（JWT / API-KEY / NTLM + Token Cache）
- Ocelot 具名 HttpClient
- 啟動驗證（含部門前綴檢查）
- 稽核日誌與 PII 遮蔽
- 欄位投影 helper、可觀測性

部門專案引用此 package 後，`Program.cs` 僅需 3 行。

**維護者**：平台團隊（bus factor ≥ 2）
**相關 ADR**：[ADR-009](adr/ADR-009-department-gateway-split.md) D2

---

### **部門 Gateway（Department Gateway）**
單一部門的 MCP 服務，如 `McpGateway.Report`、`McpGateway.Spc`。

- **內容**：僅該部門的 `ITool` 類別 + 3 行 `Program.cs` + `appsettings.json`
- **部署**：獨立 container，獨立發版，故障互不影響
- **對外路徑**：`mcp.corp.local/{dept}`（經 ingress 路徑分流）
- **工具命名**：一律 `{dept}_{intent}[_v{major}]`，由 Core 啟動時強制驗證

**相關 ADR**：[ADR-009](adr/ADR-009-department-gateway-split.md) D1/D4/D6、[ADR-004](adr/ADR-004-startup-validation.md) 檢查項 3

---

### **工具爆炸（Tool Explosion）**
單一 MCP Server 掛載過多工具，導致 LLM 選錯工具、填錯參數，且工具描述吃掉大量 context token 的現象。

| Tool 數量 | LLM 行為 |
|-----------|----------|
| 5–15 | 選擇準確、參數正確率高 |
| 30+ | 開始選錯語意相鄰的工具 |
| 50+ | context 被描述吃掉，準確率明顯下滑 |

**解法**：依部門拆分 Gateway，agent 只連接所需部門端點（[ADR-009](adr/ADR-009-department-gateway-split.md) D1/D5）。

**與語意封裝的區別**：語意封裝（ADR-003）讓「每一支工具」更好懂；部門拆分讓「agent 看到的工具集合」更小。兩者互補。

---

### **MCP (Model Context Protocol)**
由 Linux Foundation 透過 Agentic AI Foundation 管理的開放標準協定，用於 AI Agent 與工具之間的通訊。

**關鍵特色**：
- 支援多種傳輸方式（Streamable HTTP, stdio, WebSocket）
- JSON-RPC 2.0 為基礎
- 動態工具註冊
- 內建工具描述與參數驗證

**相關 ADR**：[ADR-001](adr/ADR-001-use-mcp-protocol.md)

---

### **Streamable HTTP**
MCP 協定的一種傳輸方式，使用 HTTP 進行伺服器推播事件（Server-Sent Events），允許雙向非同步通訊。

** vs stdio **：
- ** Streamable HTTP **：跨機器、多用戶、適合分散式部署
- ** stdio **：單機單用戶、簡單、效能更好（但僅限本地）

本專案選擇 Streamable HTTP 以支援內部分散式架構。

---

### ** Ocelot Gateway **
現有的 .NET API Gateway，作為企業內部 API 的統一入口。Tool Facade 作為普通 API Consumer 呼叫它，無需修改其程式碼。

** 關係 **：
```
AI Agent → MCP → Ingress → 部門 Gateway → HTTP → Ocelot Gateway → 下游服務
             （路徑分流）    （McpGateway.{Dept}）
```

⚠️ 【ADR-009】此鏈路較原設計多一跳（ingress）。延遲預算需重新驗證，
見 [ADR-001](adr/ADR-001-use-mcp-protocol.md)「延遲預算需重驗」。

---

## 設計模式

### ** 語意封裝 (Semantic Encapsulation) **
將技術 API 契約轉換為 LLM 友好的工具契約的過程。核心原則：

1. ** 一個工具，一個意圖 **：避免多功能 endpoint（action 參數）
2. ** 欄位精簡 **：只保留 LLM 需要的參數，隱藏內部欄位
3. ** 描述清晰 **：使用自然語言描述，讓 LLM 理解工具用途

** 範例 **：
```cpp
// 原始 API
POST /api/orders/action
{ "action": "query", "orderId": "123" }  // action 是列舉，LLM 容易填錯

// 工具封裝
// Tool: get_order_status
// 描述：查詢訂單目前狀態
// 參數：orderId (string, description: "訂單編號，格式如 SO-2026-000123")
```

** 相關 ADR **：[ADR-003](adr/ADR-003-config-driven-descriptions.md)

---

### ** 欄位投影 (Field Projection) **
在 Tool Facade 層對下游 API 的輸出進行過濾與轉換，只返回 Agent 需要的欄位。

** 目的 **：
- 減少 Token 消耗
- 降低 LLM 困惑
- 隱藏敏感欄位

** 技術**：C# DTO映射、AutoMapper、手動選擇性複製

**相關程式碼**：延伸方法、LINQ Select

---

### **啟動時驗證（Startup Validation）**
服務啟動時執行的檢查，確保設定檔與程式碼一致性。

**目前設計**：
- 攤平 JSON Schema，獲取所有參數路徑
- 與設定檔中的 descriptions 做差異比對
- 參數無描述 → 根據 ADR-004 決定，可能是 Warning 或 Fail-fast

**相關ADR**：[ADR-004](adr/ADR-004-startup-validation.md)

**風險**：JSON Schema flatten 在邊緣案例（oneOf, array of objects）可能產生歧義

---

## 設定檔相關

### **Tree Path（樹狀路徑）**
設定檔中參數描述的鍵（Key），使用點號（.）分隔巢狀結構。

**範例**：
```json
{
  "descriptions": {
    "order_id": "訂單編號",
    "filter.dateRange.start": "開始日期",  // 嵌套路徑
    "filter.dateRange.end": "結束日期"
  }
}
```

**對應**：
```csharp
public record QueryParams(
    string OrderId,  // order_id
    FilterParams Filter  // filter.dateRange.start
);

public record FilterParams(
    DateRangeParams DateRange  // filter.dateRange
);

public record DateRangeParams(
    DateTime Start,  // filter.dateRange.start
    DateTime End     // filter.dateRange.end
);
```

**爭議**：路徑格式無標準，可能產生歧義。替代方案是 JSON Pointer（如 `/filter/dateRange/start`）。

**相關 ADR**：[ADR-003](adr/ADR-003-config-driven-descriptions.md), [ADR-004](adr/ADR-004-startup-validation.md)

---

### **JsonPointer（RFC 6901）**
JSON 資源的標準路徑表示法，用於識別內部欄位。

**格式**：字符串，以 `/` 開頭，每個層級分隔

**範例**：
```
# 對應 data.orderId
/orderId

# 對應 data.filter.dateRange.start
/filter/dateRange/start

# 對應 data.array[0].id
/array/0/id
```

**優點**：
- 標準化（RFC 標準）
- 現有函式庫支援
- 支援陣列索引

**建議**：考慮用 JsonPointer 取代自定義 tree path

---

## 安全與認證

### **Secret Management（秘密管理）**
儲存與管理 API 金鑰、密碼等敏感資訊的機制。

**選項**：
- 環境變數（MVP）
- Azure Key Vault（如可用）
- HashiCorp Vault（推薦長期）

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

---

### **Agent Identity（代理身份）**
識別是哪個 AI Agent 呼叫 Tool 的唯一標識符。

**MVP 方案**：API Key

**長期方案**：OAuth 2.0 + JWT Token

**關鍵問題**：
- Agent 不是人，沒有"角色"概念
- 需要 ABAC（屬性基礎授權），而非傳統 RBAC

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

### **Auth Provider（認證提供者）**
MCP Gateway 提供呼叫端點與下游存取的身份認證機制，支援以下選項：

- **`JWT`** (預設)：MCP Client 攜帶 Bearer Token，Gateway 透過 JWKS Endpoint 驗證公鑰並提取身份 Claim。
- **`API-KEY`**：服務對服務認證，由 Central API-KEY Validation Service 驗證身分。
- **`NTLM`**：Windows 整合認證，透過系統服務帳號（由環境變數 `NTLM_SERVICE_ACCOUNT` / `NTLM_SERVICE_PASSWORD` 注入）向下游舊型系統認證。
- **`None`** / **`Disabled`**：停用認證（`"Enabled": false`），僅適用於測試與單機開發環境。

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

---

---

### **ABAC（Attribute-Based Access Control）**
基於屬性（而非角色）的授權機制。

**屬性範例**：
- Agent 類型：`customer_service_bot`, `admin_bot`, `finance_bot`
- 環境：`development`, `production`
- 敏感度：低、中、高

**政策範例**：
```
允許 IF agent.type == "customer_service" AND tool.name == "get_order_status"
禁止 IF agent.environment == "production" AND tool.is_experimental == true
```

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

---

### **PII Redaction（個人識別資訊遮蔽）**
在稽核日誌中遮蔽敏感個人資訊（email, 姓名, 地址）的機制。

**範例**：
```cpp
// 原始
{ "email": "customer@example.com", "orderId": "SO-12345" }

// 遮蔽後
{ "email": "c***@example.com", "orderId": "SO-***" }
```

**重要性**：符合隱私法規（GDPR, 個資法），防止日誌洩漏敏感資訊

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

---

### **Audit Log（稽核日誌）**
記錄所有 Tool 呼叫的詳細資訊，用於：
- 安全稽核
- 故障排查
- 使用率分析

**必記欄位**：
- Timestamp
- Agent Identity
- Tool Name & Version
- Parameters（遮蔽後）
- Success/Failure
- Duration
- HTTP Status Code

**相關 ADR**：[ADR-006](adr/ADR-006-security-model.md)

---

## 版本控制

### **Tool Version（工具版本）**
工具的版本號，用於管理破壞性變更。

**格式**：Semantic Versioning（MAJOR.MINOR.PATCH）

**變更規則**：
- MAJOR：破壞性變更（刪參數、改類型）
- MINOR：向後相容（新增可選參數）
- PATCH：bug fix（不改介面）

**相關 ADR**：[ADR-005](adr/ADR-005-tool-versioning.md)

---

### **Deprecation（棄用）**
標記舊版工具為已棄用，鼓勵遷移到新版本。

**政策**：
- 棄用通知期：3 個月
- 強制移除：6 個月後
- 遷移文件：提供變更指南

**相關 ADR**：[ADR-005](adr/ADR-005-tool-versioning.md)

---

### **Tool Catalog（工具目錄）**
當 Tool 數量超過 50+ 時，需要的管理工具，包含：
- 所有 Tool 的資訊（owner, version, stats）
- 棄用狀態
- 使用統計

**格式**：YAML 或數據庫

**相關 ADR**：[ADR-005](adr/ADR-005-tool-versioning.md)

---

## 開發流程

### **PoC（Proof-of-Concept）**
驗證核心假設的小型試驗，需回答：
- MCP SDK 成熟度
- 效能影響（latency）
- 正確率提升（量化數據）

**必要**：在正式開發前完成

---

### **Grilling（嚴格審查）**
對設計或計劃進行無情的質疑，目的是：
- 暴露隱藏風險
- 驗證假設
- 找出缺陷

**產出**：High/Medium/Low 風險清單、決策問題、ADR 列表

**本文件即 grilling 產出**

---

### **ADR（Architecture Decision Record）**
架構決策記錄，紀錄重要決策的：
- 背景
- 選項與權衡
- 最終決策
- 後果與風險

**相關**：所有 `docs/architecture/adr/*.md` 檔案

---

## 相關文件

### **High Priority ADRs**
- [ADR-001: MCP 協定選擇](adr/ADR-001-use-mcp-protocol.md) ✅ 已批准（PoC 完成）
- [ADR-002: .NET SDK 語言選擇](adr/ADR-002-dotnet-mcp-sdk-choice.md) ✅ 技能棧風險已解除
- [ADR-003: 參數描述策略](adr/ADR-003-config-driven-descriptions.md) ✅ 選項 B（程式碼內聯）
- [ADR-004: 啟動驗證機制](adr/ADR-004-startup-validation.md) ✅ 含部門前綴檢查
- [ADR-005: 版本控制策略](adr/ADR-005-tool-versioning.md) ✅ 選項 B（Tool 層級版本號）
- [ADR-006: 安全模型](adr/ADR-006-security-model.md) ⚠️ 已批准，需 delta 確認（ADR-009）
- [ADR-009: 部門別 Gateway 拆分](adr/ADR-009-department-gateway-split.md) ⏳ 已提議，待批准

> ADR-007（測試策略）、ADR-008（快取策略）已預留編號但尚未撰寫。

### **Design Docs**
- `docs/tool-facade-design-doc.md` - 原始設計文件 ⚠️ 4.1 架構圖與第 6 節部署架構仍為單體形態，待更新
- `docs/specs/` - 實作規格文件

### **Skills Referenced**
- `skill://grill-with-docs` - grilling 技能
- `skill://domain-modeling` - 領域建模

---

*最後更新：2026-07-31*
*維護者：架構團隊*
