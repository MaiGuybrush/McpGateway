# ADR-001: 採用 MCP 協定作為 AI Agent 工具呼叫標準

## 狀態
**⚠️ 已提議 —— 數據驗證尚未真正完成**（2026-08-02 重新評估）

> **狀態澄清（2026-08-02）**：本 ADR 的「後續行動」清單原假設 PoC 會提供驗證數據。
> 經查核，**PoC 產出的是預估值而非實測值**，本 ADR 的核心技術假設仍未驗證。
>
> **查核發現**：
> 1. `PoC-REPORT.md:375` 自陳「數據為預估值，實際值以正式開發測量為準」，
>    全部延遲、正確率數字均標註「預估」。
> 2. PoC 程式碼**無法編譯**：`McpServerHost.cs:68` 與 `ToolRegistry.cs:49`
>    在同一命名空間定義兩個簽章不同的 `ITool`（CS0101）；
>    `McpGatewayService.cs:17` 建構子型別不符（CS0029）；
>    `Program.cs:13` 註冊的 `McpServer` 未實作本地 `IMcpServer`。
> 3. 故「Streamable HTTP 效能」「SDK 成熟度」**均無實測依據**。
>
> **結論**：MCP 協定的**選擇理由**（標準化、跨框架相容）仍然成立且不依賴實測，
> 但「效能可接受」「SDK 成熟度足夠」兩項假設**尚未驗證**，
> 需於開發 Sprint 0 以可運行的 spike 補齊後，本 ADR 方可改為已批准。
>
> 同時因 [ADR-009](ADR-009-department-gateway-split.md) 拓撲變更，本 ADR 的
> **stdio 退路**已失效、**延遲預算**需重驗，詳見下方標註。

## 背景

我們正在建立一個 AI Agent 基礎設施，讓內部 Agent（Semantic Kernel、Pydantic AI）能夠呼叫企業內部 API。目前面臨幾個選擇：

1. **直接呼叫**：讓 Agent 直接呼叫 Ocelot Gateway（REST API）
2. **自定義協定**：建立團隊內部的工具呼叫協定
3. **MCP 協定**：採用 Model Context Protocol 標準

## 決策

**選擇 3：採用 MCP 協定（Streamable HTTP 傳輸）**

### 理由

- **標準化**：MCP 已成為 Linux Foundation 下的開放標準，獲得多家 AI 廠商支持（OpenAI、Claude、Cody 等）
- **跨框架相容**：同一個 Tool 可同時被 Semantic Kernel、Pydantic AI、LangChain 等框架使用
- **未來擴展性**：未來可無縫整合新 AI 框架，無需重寫工具層
- **生態成熟**：MCP .NET SDK 提供完整實作，降低開發成本

### 替代方案考量

- **直接呼叫（方案 1）**：
  - *優點*：最簡單，無需額外層
  - *缺點*：不同框架各自實作工具呼叫邏輯，程式碼重複；API 變更需修改多處；無統一治理
  
- **自定義協定（方案 2）**：
  - *優點*：完全控制，可針對內部需求最佳化
  - *缺點*：需完整實作協定棧；框架整合成本高；未來維護負擔重

### 風險與緩解

| 風險 | 機率 | 影響 | 緩解措施 | 狀態（2026-08-02 重新評估） |
|-----|------|------|---------|------|
| MCP 協定規範變更 | 中 | 中 | 使用官方 SDK，及時追蹤規範更新；保持 SDK 版本最新 | 持續監控 |
| .NET SDK 成熟度不足 | 低 | 高 | 先建立 PoC 驗證核心功能；參與開源社群提交 bug fix | 🔴 **未解除** —— PoC 未產生可運行程式碼，套件仍為 `0.1.0-preview.*` |
| Streamable HTTP 效能不佳 | 中 | 中 | ~~保留 stdio 作為未來選項~~；在 PoC 階段做效能基準測試 | 🔴 **未驗證** + ⚠️ **退路已失效，見下** |

#### 🔴 SDK 成熟度風險重新評估（2026-08-02）

原評估「機率低」的依據為「.NET SDK 為官方維護、功能最完整」，但：

- `src/McpGateway/McpGateway.csproj` 引用版本為 **`ModelContextProtocol 0.1.0-preview.*`** —— preview 版本，API 可能變動
- PoC 程式碼中的 SDK 用法（`McpServer.ForTransport`、`server.RegisterToolAsync`、
  `CallToolResponse.Result`）**未經編譯驗證**，可能與實際 SDK API 不符
- 尚未使用 `ModelContextProtocol.AspNetCore`（Streamable HTTP 所需套件）

**行動**：Sprint 0 須先做**可運行的 SDK spike**（見開發計畫），確認：
1. 實際 SDK 的動態 Tool 註冊 API 為何（design-doc 4.2.2 的核心假設）
2. `ModelContextProtocol.AspNetCore` 的 `MapMcp` 是否支援路徑前綴（ADR-009 D4 的前提）
3. preview 版本是否足以支撐生產，或需等待 GA

#### ⚠️ 【ADR-009】stdio 退路已失效

本 ADR 原將「若 Streamable HTTP 效能不佳，退回 stdio」列為緩解措施。
[ADR-009](ADR-009-department-gateway-split.md) 採用多部門服務 + ingress 路徑分流拓撲後，**此退路不再成立**：

| stdio 的限制 | 與 ADR-009 拓撲的衝突 |
|-------------|---------------------|
| 單機、單一用戶端、process 綁定 | ADR-009 要求跨機器、多 agent 同時連線 |
| 無 HTTP 路由概念 | ADR-009 D4 依賴 `/{dept}` 路徑分流 |
| 無法置於 ingress 之後 | ADR-009 D4 以 ingress 提供單一 hostname |
| 無法獨立擴縮 | ADR-009 D1 要求部門各自部署與擴縮 |

**修正後的緩解措施**：若 Streamable HTTP 效能不佳，可行方向為
（a）Core 層加入回應快取（見 ADR-008，未撰寫）、
（b）調整 ingress 與 HttpClient 連線池設定、
（c）部門服務水平擴充。
**不再以 stdio 作為退路**，任何規劃不應假設此選項存在。

### 後果

- **正面**：統一工具層，跨框架相容，未來擴展性強
- **負面**：需學習 MCP 協定；依賴外部 SDK 品質
- **負面（ADR-009 修訂）**：網路跳轉由「一跳」增為**兩跳** —— `Agent → ingress → 部門 gateway → Ocelot → 下游服務`

#### ⚠️ 【ADR-009】延遲預算需重驗

PoC 提出的延遲數字（[PoC-REPORT.md](../../../PoC-REPORT.md)，p95 約 42–52ms）為**預估值，非實測**（該報告第 375 行自陳），且假設**單體、無 ingress** 環境。

兩個疊加問題：

1. **基準本身未經量測** —— 沒有可運行的程式碼產生過這些數字
2. **拓撲多一跳** —— ADR-009 引入 ingress，實際鏈路為 `Agent → ingress → 部門 gateway → Ocelot → 下游`

另注意：預估值中 Manual OrderCreate 的 p95 已達 **52ms**，本身即高於原訂 <50ms 門檻，
README 卻聲稱「All results meet <50ms p95 threshold」—— 此宣稱與其自身表格不符。

**行動**：
- [ ] Sprint 0 SDK spike 完成後，取得**第一組真實延遲數據**（單體、無 ingress）
- [ ] ADR-009 Phase 2（首個部門部署驗證）量測**含 ingress** 的端到端 p95
- [ ] 依真實數據重新確認 <50ms p95 門檻是否合理，或訂定含 ingress 的新門檻

## 後續行動

- [ ] 🔴 **建立可運行的 SDK spike** —— 原「PoC」未產生可編譯的程式碼（見狀態欄查核發現）
- [ ] 🔴 **進行效能基準測試（真實量測）** —— 現有數字為預估值
- [ ] 評估 SDK 原始碼品質與社群活躍度（含 preview → GA 時程）
- [ ] 與現有 Agent 框架整合測試（Semantic Kernel + Pydantic AI 各一）
- [ ] **【ADR-009】**重新量測含 ingress 的端到端延遲
- [ ] **【ADR-009】**將 stdio 傳輸實作（`Program.cs` `RunConsoleAsync`）替換為 ASP.NET Core Streamable HTTP

## 相關 ADRs

- [ADR-002](ADR-002-dotnet-mcp-sdk-choice.md)：SDK 與語言選擇
- [ADR-009](ADR-009-department-gateway-split.md)：多部門拓撲。**確定 Streamable HTTP 的落地時機（MVP Phase 1）並將傳輸層收進 `McpGateway.Core`**；同時使 stdio 退路失效、延遲預算需重驗

## 參與決策者

- 提案人：架構師（待填寫）
- 審核人：待確定

---
*最後更新：2026-08-02*
