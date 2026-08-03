# McpGateway 開發計畫

**版本**：v0.1
**日期**：2026-08-02
**依據**：[Core 規格](mcp-gateway-core-spec.md)、ADR-001 ~ ADR-006、ADR-009
**狀態**：待 Sprint 0 完成後修正估時

---

## 0. 起點盤點（2026-08-02 查核結果）

開始排程前，先誠實記錄目前實際擁有的東西。

| 項目 | 宣稱狀態 | 實際狀態 |
|------|----------|----------|
| PoC 程式碼 | 「PoC 實作完成」 | 🔴 **無法編譯**（詳下） |
| 效能數據 | p95 42–52ms | 🔴 **預估值**，`PoC-REPORT.md:375` 自陳 |
| 正確率數據 | 55–65% vs 90–95% | 🔴 **預估值**，非實測 |
| SDK 驗證 | 「Streamable HTTP 支援完整（經 PoC 驗證）」 | 🔴 **無依據**，`ModelContextProtocol.AspNetCore` 未曾引用 |
| 建置環境 | — | 🔴 本機僅 .NET SDK **3.1.402**，無法建置 `net9.0` |
| ADR 決策 | 6 份 + ADR-009 | ✅ **有效**，此為真正的資產 |
| 設計文件 | design-doc + glossary | ✅ **有效**（架構圖待更新為多部門） |

### PoC 程式碼的具體編譯錯誤

| 位置 | 錯誤 |
|------|------|
| `McpServerHost.cs:68` + `ToolRegistry.cs:49` | 同命名空間兩個簽章不同的 `ITool` → CS0101 |
| `McpGatewayService.cs:17` | 建構子收 `ILogger<McpServer>`，指派給 `ILogger<McpGatewayService>` → CS0029 |
| `Program.cs:13` | `AddSingleton<IMcpServer, McpServer>()`，SDK 的 `McpServer` 未實作本地 `IMcpServer` |
| `IMcpServer.cs:7,12` | 引用未定義的 `IServer`、`ProtocolMessage` |

### 既有程式碼的處置

| 路徑 | 處置 | 理由 |
|------|------|------|
| `src/McpGateway/Infrastructure/` | ❌ **捨棄** | 無法編譯，且對應的 SDK API 未經驗證 |
| `src/McpGateway/Tools/Manual/` | ✅ **保留為參考** | 語意封裝的**描述文字**有價值，是 ADR-003 的實例 |
| `src/McpGateway/Tools/Mechanical/` | ❌ **捨棄** | PoC 對照組，已完成任務（PoC 結論選 Manual） |
| `src/McpGateway/Tools/OpenApi*` | ❌ **捨棄** | 機械式轉換路線已否決 |
| `src/MockOcelotApi/` | ✅ **保留** | 整合測試需要 |
| `tests/k6/` | ✅ **保留** | 效能測試腳本，Sprint 0 後真正跑起來 |

**這不是「重寫」，是「首次實作」** —— 現有 `Infrastructure/` 從未成功建置過。

---

## 1. 里程碑總覽

```
Sprint 0   環境與 SDK spike        ← 🔴 決策關卡，可能推翻 ADR-001/002
   │
Sprint 1   Core 骨架 + JWT 認證
   │
Sprint 2   API-KEY + 下游 + 稽核
   │
Sprint 3   NTLM + 可觀測性 + 測試框架
   │
Sprint 4   首個部門專案 + 部署驗證  ← 🔴 效能數據首次真實量測
   │
Sprint 5   第二個部門（驗證可複製性）  ← 觸發式，非排程
```

**估時前提**：1 名專職 C# 工程師（Core）+ 部門開發者兼職（工具）。
若人力不同，需重估。

---

## 2. Sprint 0：環境與 SDK Spike 🔴

**目標**：讓「能不能做」這件事有答案。**這是決策關卡，不是暖身。**

**估時**：5 人天
**阻斷**：全部後續工作

| # | 任務 | 人天 | 相依 |
|---|------|------|------|
| 0.1 | 安裝 .NET 9 SDK，確認建置環境 | 0.5 | — |
| 0.2 | **SDK spike**：以官方 `ModelContextProtocol` + `.AspNetCore` 寫一支最小可運行 MCP server | 2 | 0.1 |
| 0.3 | 驗證 Attribute 標註 + 組件掃描的註冊路徑（design-doc 4.2.2 v1.1） | 含 0.2 | — |
| 0.4 | 驗證 `MapMcp` 是否支援路徑前綴（ADR-009 D4 前提） | 含 0.2 | — |
| 0.5 | 量測第一組**真實**延遲（單體、無 ingress） | 0.5 | 0.2 |
| 0.6 | 建立 `McpGateway.Core` 專案骨架 + 內部 NuGet feed 發布流程 | 1.5 | 0.1、DevOps |
| 0.7 | 清理既有無法編譯的程式碼（依 §0 處置表） | 0.5 | — |

### Sprint 0 出口條件（Gate）

必須全數為「是」才進 Sprint 1：

- [ ] 有一支**可運行**的 MCP server，能被 MCP client 連上並列出工具
- [ ] Attribute 標註 + 組件掃描的註冊路徑**確認可行**
- [ ] `MapMcp` 路徑前綴**確認可行**（否則 ADR-009 D4 需改設計）
- [ ] preview 版本無阻斷性 bug，或已知 GA 時程可接受
- [ ] 取得第一組真實延遲數據
- [ ] 內部 NuGet feed 可發布與還原

> **📉 風險降低（design-doc v1.1）**：原本 design-doc 4.2.2 主張需要「動態註冊 API」
> （執行期由設定檔決定 name/description），該 API 是否存在為高風險未知數。
> 但 ADR-003 選定**程式碼內聯描述**後，此需求消失 —— Attribute 常數即可，
> 而 Attribute 標註是 MCP SDK 最基本、最不可能缺席的功能。
> **Sprint 0 的最大技術風險因此從「動態註冊」轉移到「路徑前綴支援」。**

### ⚠️ Gate 失敗時的行動

| 失敗項 | 機率 | 影響 | 行動 |
|--------|------|------|------|
| Attribute 註冊不可行 | 低 | 中 | 極不可能（SDK 最基本功能）。若真發生，改 source generator |
| **路徑前綴不支援** | **中** | **高** | 退回「純 hostname 區分」（ADR-009 D4 選項 2），ingress 改 host routing。Agent 端需管 N 個 hostname，但架構其餘部分不變 |
| 真實延遲 >> 預估 | 中 | **高** | **停下重審 ADR-001**，評估快取（ADR-008）或直接呼叫方案。**stdio 退路已失效** |
| preview 不堪用 | 中 | **高** | 評估等待 GA、fork SDK、或改用其他語言 SDK（需重審 ADR-002） |

**不得在 Gate 未通過時推進到 Sprint 1。** 這正是 GRILLING-SUMMARY 當初要求做 PoC 的理由，
而該 PoC 未真正執行 —— 不應重蹈覆轍。

---

## 3. Sprint 1：Core 骨架 + JWT 認證

**估時**：8 人天
**相依**：Sprint 0 Gate 通過、Redis 環境（DevOps）

| # | 任務 | 人天 | 規格 |
|---|------|------|------|
| 1.1 | Host bootstrap：`AddMcpGateway` / `RunMcpGatewayAsync` | 2 | §3.2 |
| 1.2 | Tool 掃描與註冊：`AddToolsFromAssembly`、`ToolBase<TIn,TOut>`、`McpToolAttribute` | 2 | §3.3 |
| 1.3 | 啟動驗證 6 項檢查（含 ADR-009 D6 部門前綴） | 1 | §5.1 |
| 1.4 | 設定結構繫結與必填驗證 | 0.5 | §4 |
| 1.5 | JWT 驗證（JWKS Public Key）+ 快取 | 1.5 | §6 |
| 1.6 | Token Cache（Redis）+ TTL 策略 | 1 | §6.2 |

**出口條件**：
- [ ] 一支測試工具可經 JWT 認證後被呼叫
- [ ] 啟動驗證 6 項各有單元測試，錯誤訊息一次列出全部問題
- [ ] Core 可發布至內部 NuGet feed 並被測試專案還原

---

## 4. Sprint 2：API-KEY + 下游呼叫 + 稽核

**估時**：7.5 人天
**相依**：Sprint 1；API-KEY 服務 SLA 確認（Auth 團隊）

| # | 任務 | 人天 | 規格 |
|---|------|------|------|
| 2.1 | API-KEY 服務客戶端 + 快取 | 2 | §6 |
| 2.2 | 認證降級策略（5 種情境） | 2 | §6.4 |
| 2.3 | `IDownstreamClient` + 具名 HttpClient + 重試（僅冪等方法） | 1.5 | §7 |
| 2.4 | 稽核日誌 + PII 遮蔽（含 `Department`/`CoreVersion` 欄位） | 2 | §8 |

**出口條件**：
- [ ] API-KEY 與 JWT 兩條認證路徑皆可用
- [ ] 認證服務停機時降級行為符合 §6.4，有整合測試覆蓋
- [ ] 稽核日誌含全部必要欄位，PII 遮蔽經測試驗證（原始值不進 sink）

---

## 5. Sprint 3：NTLM + 可觀測性 + 測試框架

**估時**：6.5 人天
**相依**：Sprint 2；NTLM 帳號配置裁示（安全團隊，ADR-006 Δ6）

| # | 任務 | 人天 | 規格 |
|---|------|------|------|
| 3.1 | NTLM 系統帳號（K8s Secret 注入） | 1 | §6、§12.2 |
| 3.2 | Metrics（6 項，含 `mcpgw_core_version`） | 1 | §10.1 |
| 3.3 | Health checks（live / ready） | 0.5 | §10.2 |
| 3.4 | 錯誤處理與 CorrelationId 貫穿 | 1 | §9 |
| 3.5 | **契約測試框架**（最小部門專案作基準） | 2 | §13 |
| 3.6 | 整合測試（WireMock 模擬 Ocelot 與認證服務） | 1 | §13 |

**出口條件**：
- [ ] 契約測試可攔下 Core 的破壞性變更
- [ ] `mcpgw_core_version` 已上報，儀表板可見
- [ ] Core v1.0.0 發布至內部 NuGet feed

---

## 6. Sprint 4：首個部門專案 + 部署驗證

**估時**：Core 側 4 人天 + 部門側 5 人天
**相依**：Sprint 3；ingress 路徑分流能力確認（DevOps）

| # | 任務 | 人天 | 負責 |
|---|------|------|------|
| 4.1 | 建立 `McpGateway.{Dept}` 專案（驗證 3 行 `Program.cs`） | 0.5 | 部門 |
| 4.2 | 撰寫 5–10 支工具（語意封裝，參考既有 `Tools/Manual/`） | 5 | 部門 |
| 4.3 | Container 化 + K8s 部署資源 | 2 | Core |
| 4.4 | Ingress 路徑分流設定與驗證 | 1 | Core + DevOps |
| 4.5 | E2E 測試（SK + Pydantic AI 各接一次） | 1 | Core |
| 4.6 | **效能量測：含 ingress 的端到端 p95** | 含 4.5 | Core |

### 🔴 Sprint 4 的關鍵量測

這是專案第一次取得**真實的端到端數據**。需回答 PoC 原本該回答卻沒回答的問題：

- [ ] 含 ingress 的 p95 是多少？與 <50ms 門檻的關係？
- [ ] 語意封裝的工具，LLM 選擇與填參正確率實際是多少？
      → 用 `tests/McpGateway.Tests/TestPrompts.md` 的 20 個提示詞跑真實測試
- [ ] 新部門從零到部署花了多久？（ADR-002 成功標準：≤ 1 人天）

**若正確率或延遲與 PoC 預估落差過大，需回頭重審 ADR-001 與整體 ROI 論證。**

---

## 7. Sprint 5：第二個部門（觸發式）

**觸發條件**：第二個部門提出需求
**估時**：部門側 ≤ 1 人天（骨架）+ 工具開發

| # | 任務 | 目的 |
|---|------|------|
| 5.1 | 全程記錄新部門上手工時 | 驗證 ADR-009 D2 邊界是否劃對 |
| 5.2 | 共用 CI build template（含 `MIN_SUPPORTED` 閘門） | ADR-009 D3 第 2 層 |
| 5.3 | 跨部門 agent 同時連兩端點的行為驗證 | ADR-009 D5/D6 |

⚠️ **若新部門上手 > 3 人天，代表 ADR-009 D2 邊界劃錯，需回頭檢討 Core 職責範圍。**

---

## 8. 外部相依與阻斷點

| # | 相依項 | 對象 | 阻斷 | 需求時點 |
|---|--------|------|------|----------|
| 1 | 內部 NuGet feed | DevOps | 🔴 Sprint 0.6 | **立即** |
| 2 | .NET 9 SDK 開發環境 | 自行安裝 | 🔴 Sprint 0.1 | **立即** |
| 3 | Redis（Token Cache）+ `maxclients` 評估 | DevOps | 🔴 Sprint 1.6 | Sprint 1 前 |
| 4 | JWKS 端點與 SLA | Auth 團隊 | 🔴 Sprint 1.5 | Sprint 1 前 |
| 5 | API-KEY 驗證服務與 SLA | Auth 團隊 | 🔴 Sprint 2.1 | Sprint 2 前 |
| 6 | NTLM 帳號配置裁示（獨立 vs 共用） | 安全團隊 | 🟡 Sprint 3.1 | Sprint 3 前 |
| 7 | Ingress 路徑分流能力 | DevOps | 🔴 Sprint 4.4 | Sprint 4 前 |
| 8 | ADR-009 批准 | 架構團隊 | 🔴 全部 | **立即** |
| 9 | ADR-006 delta 確認 | 安全團隊 | 🟡 Sprint 3 | Sprint 3 前 |
| 10 | `MIN_SUPPORTED` 政策與升級 SLA | 平台 + 安全 | 🟢 Sprint 5 | Sprint 5 前 |

**今天（2026-08-02）就該送出的**：#1、#2、#8。

---

## 9. 估時彙總

| Sprint | 內容 | Core 人天 | 部門人天 |
|--------|------|-----------|----------|
| 0 | 環境 + SDK spike | 5 | — |
| 1 | Core 骨架 + JWT | 8 | — |
| 2 | API-KEY + 下游 + 稽核 | 7.5 | — |
| 3 | NTLM + 可觀測性 + 測試 | 6.5 | — |
| 4 | 首個部門 + 部署 | 4 | 5 |
| **合計** | | **31** | **5** |

**對照既有估算**：

| 來源 | 估算 | 差異說明 |
|------|------|----------|
| ADR-006 | 認證 13.5 人天 | ✅ 已含於 Sprint 1–3，數字一致 |
| ADR-009 | 結構成本 +2 人天 | ✅ 已含於 Sprint 0.6 |
| GRILLING-SUMMARY | 6–7 週到 MVP | ⚠️ 31 人天 ≈ 6.2 週（單人），**但未含 Sprint 0 的 Gate 失敗風險** |

⚠️ **估時信心度：中低**。Sprint 0 完成前，Sprint 1–4 的估時建立在「SDK 如預期運作」的假設上。
Sprint 0 結束後應重估。

---

## 10. 風險登記

| 風險 | 機率 | 影響 | 緩解 |
|------|------|------|------|
| SDK preview API 在開發期間變動 | 中 | 中 | 鎖定版本；Core 對 SDK 的依賴集中於少數檔案，便於適配 |
| Sprint 0 Gate 失敗 | 中 | **高** | 已定義四種失敗的替代路徑（§2） |
| 真實正確率遠低於預估 90–95% | 中 | **高** | Sprint 4 首次真實量測；若落差大則重審 ROI |
| 真實延遲超出 <50ms 門檻 | 中 | 中 | 快取（ADR-008）、連線池調校、水平擴充。**stdio 退路已失效** |
| Core 維護者離職（bus factor） | 低 | 高 | ADR-002 要求 ≥ 2 名可維護人員 + 完整規格與契約測試 |
| 外部相依（Auth/DevOps）延遲 | 中 | 中 | 認證服務可先以 Stub 開發（ADR-006 已載明不阻斷） |

---

## 11. 待辦：文件同步

- [x] `docs/tool-facade-design-doc.md` → **v1.1 完成**（2026-08-02）：架構圖改為多部門拓撲、
      FR-2/FR-3 標記已被 ADR-003 推翻、§4.3 設定檔改寫、§5.1/5.2 標記風險已消失、
      §6 部署架構重寫、§7 開放問題更新
- [x] `docs/architecture/glossary.md` → **完成**：Tool Facade 定義、新增 Core Package／部門 Gateway／
      工具爆炸詞條、修正全部失效連結
- [ ] `README.md` 專案結構區塊、PoC 數據需標註為預估值
- [ ] `PoC-REPORT.md` → 加註「程式碼未通過編譯，數據為預估」的查核結果
- [ ] `DELIVERABLES.md` → 依實際交付狀態修正

---

*最後更新：2026-08-02*
*相關文件：[Core 規格](mcp-gateway-core-spec.md)、[ADR-009](../architecture/adr/ADR-009-department-gateway-split.md)*
