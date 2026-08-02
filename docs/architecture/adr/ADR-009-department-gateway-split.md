# ADR-009: 部門別 Gateway 拆分（Core Package + 多部門服務）

## 狀態

**已提議**（待架構團隊批准）

**提案日期**：2026-08-01
**提案方式**：`skill://grill-me` grilling session
**審核人**：TODO
**批准人**：TODO

> **編號說明**：ADR-007（測試策略）與 ADR-008（快取策略）已於 `GRILLING-SUMMARY.md` 預留但尚未撰寫，本 ADR 為避免衝號而取 009。

---

## 背景

### 觸發問題

原始設計（`docs/tool-facade-design-doc.md` 4.1）為**單一 Tool Facade 服務**，所有 Tool 註冊在同一個 MCP Server：

```
[SK Agent] [Pydantic AI Agent] ──MCP──> [Tool Facade（單一）] ──HTTP──> [Ocelot]
```

現有程式碼亦如此：`src/McpGateway/Infrastructure/ToolRegistry.cs:17` 是一個扁平的 `List<ITool>`，`ToolFactory.cs:32` 硬編碼 new 出所有工具，無任何分組概念。

隨部門與 Tool 數量成長，此結構暴露兩個問題。

### 驅動力 1：LLM 準確率（工具爆炸）

單一 MCP Server 掛載全公司工具時，任何 agent 連上就會拿到**全部**工具清單：

| Tool 數量 | LLM 行為 |
|-----------|----------|
| 5–15 | 選擇準確、參數正確率高 |
| 30+ | 開始選錯相鄰語意的工具 |
| 50+ | context 被工具描述吃掉大量 token，準確率明顯下滑 |

一支只做報表的客服 agent，不該在 context 裡背著 SPC 製程管制的工具描述。

> 此問題與 ADR-003（語意封裝提升單一 Tool 準確率）**互補而非重疊**：ADR-003 讓「每一支工具」更好懂，本 ADR 讓「agent 看到的工具集合」更小。

### 驅動力 2：團隊 / 部署獨立

單一服務代表：

- report 團隊改一支工具 → 整個 process 重啟 → **SPC agent 的連線一起斷**
- 所有部門共用一條發版排程，發版節奏被綁死
- 任一部門的工具出錯，可能拖垮整個 Facade

### 既有 ADR 涵蓋狀況

盤點全部 6 份 ADR + design-doc + GRILLING-SUMMARY，**均未涵蓋**本議題：

| 文件 | 最接近的內容 | 是否涵蓋 |
|------|--------------|----------|
| ADR-001 | 選定 Streamable HTTP 傳輸 | ❌ 只談傳輸協定，未談路由/分組 |
| ADR-005 | Tool > 50 時建 Tool Catalog | ❌ 是「清單管理」，非「部署拆分」 |
| ADR-006 | 認證代理架構 | ❌ 假設單一服務 |
| design-doc 6 | 「獨立 container 部署」 | ❌ 單數形態，無多實例規劃 |

故需新開本 ADR。

---

## 決策

**採用：Core Package + 各部門獨立專案／獨立部署，透過 ingress 路徑分流對外呈現單一 hostname。**

### 整體架構

```
                    Agent 端只認識一個 hostname
                              │
   ┌──────────────┐  ┌────────┴────────┐  ┌──────────────┐
   │ 客服 Agent    │  │  製程 Agent      │  │ 品保 Agent    │
   │ /report       │  │  /spc            │  │ /report,/qc  │
   └───────┬──────┘  └────────┬────────┘  └───────┬──────┘
           └──────────────────┼───────────────────┘
                              │ MCP (Streamable HTTP)
                              ▼
                  ┌───────────────────────┐
                  │   Ingress（路徑分流）   │  mcp.corp.local
                  └───────────┬───────────┘
              ┌───────────────┼───────────────┐
     /report  │        /spc   │         /qc   │
              ▼               ▼               ▼
   ┌────────────────┐ ┌──────────────┐ ┌──────────────┐
   │ McpGateway     │ │ McpGateway   │ │ McpGateway   │
   │   .Report      │ │   .Spc       │ │   .Qc        │
   │  ┌──────────┐  │ │ ┌──────────┐ │ │ ┌──────────┐ │
   │  │  .Core   │  │ │ │  .Core   │ │ │ │  .Core   │ │  ← 同一 package
   │  └──────────┘  │ │ └──────────┘ │ │ └──────────┘ │
   └───────┬────────┘ └──────┬───────┘ └──────┬───────┘
           └─────────────────┼────────────────┘
                             │ HTTP/JSON
                             ▼
                  ┌───────────────────────┐
                  │    Ocelot Gateway      │
                  └───────────────────────┘
```

### 決策要點（八項）

| # | 議題 | 決策 |
|---|------|------|
| D1 | 拓撲 | Core package + 各部門獨立專案／container |
| D2 | 職責邊界 | 極薄部門專案：Core 吃下全部橫向關切 |
| D3 | 版本漂移 | 浮動 patch 範圍 + CI 閘門 + 執行期遙測 |
| D4 | URL 形狀 | 保留 `/{dept}` 前綴，ingress 路徑分流 |
| D5 | 發現機制 | Agent 設定檔寫死，不建 registry |
| D6 | 命名衝突 | Core 強制 `{dept}_` 前綴，啟動時 fail-fast |
| D7 | 實作時機 | MVP 即採用，首發一個部門 |
| D8 | 聚合層 | **不做** meta-server（會抵消部署獨立） |

---

## D1：拓撲 — Core Package + 各部門獨立專案

### 選項對比

| 面向 | A. 單一 app 多路徑 | B. 單一 image 多部署 | **C. Core package + N 專案（採用）** |
|------|-------------------|---------------------|-----------------------------------|
| LLM 準確率 | ✅ 解決 | ✅ 解決 | ✅ 解決 |
| 部署獨立 | ❌ 共用 process | ⚠️ 部分（共用 build） | ✅ 完全獨立 |
| 部門發版自主 | ❌ | ⚠️ 需協調 build | ✅ |
| 故障隔離 | ❌ 全掛 | ✅ | ✅ |
| 認證實作份數 | 1 | 1 | 1（在 package） |
| 版本漂移風險 | 無 | 低 | ⚠️ **中 → 見 D3** |
| 新部門上手成本 | 低 | 低 | 中（見 D2 壓低） |

### 採用理由

- 選項 A 只中一半驅動力，**部署獨立完全落空**（report 發版必斷 SPC 連線）
- 選項 B 的「共用 image」代表 build 仍需協調，且 image 內含所有部門程式碼，故障隔離不徹底
- 選項 C 的主要缺點（認證重複實作 N 份、版本漂移）分別由 **D2 極薄邊界** 與 **D3 三層防護** 化解

### 未採用：聚合 gateway（meta-server）

曾評估在各部門 gateway 之上再疊一層 MCP proxy，讓 agent 只連一個端點、由該層依身分過濾工具。**否決**，理由：

1. 該層成為新的單一故障點 — 它掛，全部 agent 掛
2. 它需知道所有部門的 schema → 部門發版又要動到它 → **把剛拆掉的耦合裝回來**
3. 它唯一的優點（agent 只設一個 URL）已由 D4 ingress 路徑分流達成，且不引入額外 runtime

---

## D2：職責邊界 — 極薄部門專案

### 劃分

| 歸屬 | 內容 |
|------|------|
| **McpGateway.Core**（package） | Host bootstrap（ASP.NET Core + Streamable HTTP）、ADR-006 認證代理（JWT/API-KEY/NTLM + Token Cache）、Ocelot 具名 HttpClient（timeout/retry）、ADR-004 啟動驗證、稽核日誌與 PII 遮蔽、欄位投影 helper、統一錯誤處理、健康檢查、metrics |
| **McpGateway.{Dept}**（部門專案） | **僅** `ITool` 實作類別（含 ADR-003 的 `DescriptionAttribute` 內聯描述）+ 3 行 `Program.cs` + `appsettings.json` |

### 部門專案完整樣貌

```csharp
// McpGateway.Report/Program.cs —— 全部內容
var b = WebApplication.CreateBuilder(args);
b.AddMcpGateway();                     // auth + http + audit + validation + transport
b.AddToolsFromAssembly<Program>();     // 掃描本組件內的 ITool
b.Build().RunMcpGateway();
```

```csharp
// McpGateway.Report/Tools/GetReportStatusTool.cs
[McpTool("report_get_status")]
[Description("""
    查詢報表產製狀態，適合使用者詢問「我的報表跑好了沒」時使用。

    回傳：狀態（排隊中/產製中/完成/失敗）、預估完成時間、下載連結（完成時才有）。
    """)]
public class GetReportStatusTool : ToolBase
{
    [Description("報表工單編號，格式如 RPT-2026-000123")]
    public required string JobId { get; init; }

    public override async Task<ToolResult> ExecuteAsync(...) { ... }
}
```

```json
// McpGateway.Report/appsettings.json
{
  "McpGateway": {
    "Department": "report",
    "RoutePrefix": "/report",
    "Ocelot": { "BaseUrl": "https://internal-ocelot.corp.local", "TimeoutSeconds": 10 }
  }
}
```

### 採用理由

- 新部門上手成本壓到最低：抄 3 行 `Program.cs` + 寫工具，其餘不需要懂
- **認證不可能實作錯** — 部門專案根本沒有機會漏裝 ADR-006 的認證中介層
- 稽核日誌、PII 遮蔽等合規要求集中一處，安全審核只需審 Core

### 未採用選項

| 選項 | 否決理由 |
|------|----------|
| 積木式（à la carte） | 部門可漏裝 `AddMcpAuthProxy()` / `AddAuditLog()` → 安全與合規靠人工審查把關，不可接受 |
| 模板 repo（`dotnet new`） | scaffold 出去即 fork，6 個月後 N 份分歧的 `Program.cs`，Core 修 bug 流不回去 |

---

## D3：版本漂移防護

### 風險

ADR-006 認證代理位於 Core。若 Core 修補認證漏洞，修補要進入生產需 N 個部門各自 rebuild + 發版。若無機制，可能拖數月且**無人知情**。

此為本 ADR 引入的**最主要新風險**。

### 三層防護

**第 1 層：浮動版本範圍**（自動吃到修補）

```xml
<!-- McpGateway.Report/McpGateway.Report.csproj -->
<PackageReference Include="McpGateway.Core" Version="[1.2,2.0)" />
```

下次 rebuild 自動取得 patch 與 minor；major 需明確升版（破壞性變更不自動吃）。

**第 2 層：CI 閘門**（強制下限）

```
# 共用 build template
MIN_SUPPORTED = 1.2.4        # 由平台團隊維護，含安全修補下限

if (resolvedCoreVersion < MIN_SUPPORTED):
    fail("McpGateway.Core 版本過舊，未含安全修補 CVE-XXXX。請 rebuild。")
```

低於下限直接 fail build — 部門即使長期不發版，下次任何一次發版都必然升級。

**第 3 層：執行期遙測**（可見度）

```
mcpgw_core_version{dept="report"} 1.2.4
mcpgw_core_version{dept="spc"}    1.0.1   ← 儀表板標紅
mcpgw_core_version{dept="qc"}     1.2.4
```

Core 啟動時把自身版本吐進 metric。平台團隊一眼看完誰落後，可主動催辦而非被動等待。

### 效果

| 沒有防護 | 三層防護後 |
|----------|-----------|
| 採納率不可見 | 儀表板即時可見 |
| 靠部門自律 | CI 強制，不靠人 |
| 修補進生產時間未知 | 可量測、可設 SLA |

---

## D4：URL 形狀 — 保留部門前綴 + Ingress 路徑分流

### 決策

每個部門服務掛在自己的 `/{dept}` 路徑下，ingress 依路徑扇出：

```
prod:
  mcp.corp.local/report  ──▶  report-svc:3000/report
  mcp.corp.local/spc     ──▶  spc-svc:3000/spc
  mcp.corp.local/qc      ──▶  qc-svc:3000/qc

dev:
  localhost:3000/report        （只跑正在改的那個專案）
```

### 採用理由

雖然「N 個服務各有 host，路徑前綴技術上冗餘」，但保留前綴換得：

| 效益 | 說明 |
|------|------|
| Agent 端只認一個 hostname | 換部門只改路徑，不改 DNS/憑證 |
| TLS 憑證一份 | 而非 N 張 |
| 新增部門 agent 端無感 | ingress 加一行即可 |
| dev / prod 形狀一致 | `localhost:3000/report` ↔ `mcp.corp.local/report` |

### 未採用：純 hostname 區分

`report-svc.corp.local/mcp`、`spc-svc.corp.local/mcp` — 服務邊界較直觀，但 agent 端要管 N 個 hostname、N 筆 DNS、N 張憑證，且新增部門需改動所有相關 agent 設定。

---

## D5：發現機制 — Agent 設定檔寫死

### 決策

**不建 registry、不做動態發現。** 寫 agent 的人本來就知道這支 agent 要做什麼。

```yaml
# agent config（Pydantic AI / Semantic Kernel 概念相同）
mcp_servers:
  - url: https://mcp.corp.local/report
  - url: https://mcp.corp.local/spc      # 真的需要跨部門時才加
```

LLM 看到的工具集 = 這幾個 endpoint 的工具聯集，工具爆炸問題就此解決。

### 採用理由

- **零額外基礎設施** — 不需維運一個高可用的 registry 服務
- 失效模式良好：設定寫錯 → 啟動即錯，很早發現（非靜默失敗）
- Semantic Kernel 與 Pydantic AI 皆原生支援掛載多個 MCP server
- 部門數量在可預見期間內是「十位數以下」，動態發現屬 YAGNI

### 未採用：中央 registry

```
GET https://mcp.corp.local/.well-known/gateways
```

否決理由：多一個需 HA、需監控的服務；registry 資料與實際部署會不同步（新風險）；且目前**尚無**需要跨部門動態探索的 agent。

**未來重評觸發條件**：出現需在執行期依任務動態決定連哪些部門的 agent，或部門數 > 15。

---

## D6：命名衝突 — Core 強制部門前綴

### 風險

Agent 同時連 `/report` 與 `/spc`，兩邊都有 `get_status` → LLM 看到同名工具 → 呼叫到錯部門的工具。此為**靜默錯誤**，最難查。

### 決策

Core 於啟動驗證階段（沿用 ADR-004 機制）檢查工具命名：

```csharp
// McpGateway.Core —— 接續 ADR-004 的啟動驗證
foreach (var tool in tools)
{
    if (!tool.Name.StartsWith($"{cfg.Department}_"))
        throw new StartupValidationException(
            $"工具 '{tool.Name}' 未以 '{cfg.Department}_' 開頭。" +
            $"跨部門同名衝突會導致 LLM 呼叫錯誤部門的工具。");
}
```

命名結果：

```
report_get_status
spc_get_status
report_get_order_status_v2      ← 與 ADR-005 版本後綴相容
```

### 採用理由

- 衝突可能性歸零，且是**編譯後、上線前**就擋掉（fail-fast，不進生產）
- 不依賴 MCP client 的命名空間行為 — SK 與 Pydantic AI 前綴格式不一致，換 client 就可能失效，等於把正確性外包給不可控的第三方
- 實作成本極低（沿用既有驗證流程，約 10 行）

### 代價

工具名變長，每支工具描述多吃數個 token。相較靜默呼叫錯誤的除錯成本，可接受。

### 未採用

| 選項 | 否決理由 |
|------|----------|
| 靠 MCP client 命名空間 | 行為隨 client 實作而異；壞掉時是靜默錯誤 |
| 人工審查命名登記表 | 人工流程，部門一多即卡住；審核漏看就優先衝突 |

---

## D7：實作時機 — MVP 即採用

### 決策

**MVP 階段直接採用 Core package 結構，但只交付一個部門專案。**

```
src/
  McpGateway.Core/          ← package（auth / audit / host / validation / http）
  McpGateway.Report/        ← 首個部門，5–10 支工具
  MockOcelotApi/            ← 既有 mock
tests/
  McpGateway.Core.Tests/
  McpGateway.Report.Tests/
```

MVP 對外交付形態與單體無異（就是一個服務），但結構已就位。

### 採用理由

- ADR-006 認證（13.5 人天）排定 **2026-08-02 開工**。若寫進單體，日後抽離 Core 需重寫並**重跑安全審核**
- 結構成本低（約 2 人天：專案切分 + 內部 NuGet feed 發布流程），退場成本高（單體時期的捷徑會硬化成邊界）
- 第二個部門出現時零重構

### 成本對比

| 方案 | 現在付 | 日後付 | 附帶風險 |
|------|--------|--------|----------|
| **MVP 即拆（採用）** | ~2 人天 | 0 | 無 |
| MVP 單體，MVP+1 再拆 | 0 | ~5–8 人天 | 需重走安全審核；單體捷徑硬化 |
| 錄案但無限期延後 | 0 | 可能拆不動 | 單體長大後拆分成本不可控 |

---

## 對現有程式碼的影響

| 現況 | 需變更 |
|------|--------|
| `src/McpGateway/Program.cs:20` — `RunConsoleAsync()`（stdio） | → ASP.NET Core `WebApplication` + Streamable HTTP（**ADR-001 已決定，本 ADR 只是確定落地時機**） |
| `Infrastructure/IMcpServer.cs` — 手寫 stub 介面 | → 改接官方 `ModelContextProtocol.AspNetCore` |
| `Infrastructure/ToolRegistry.cs:17` — 扁平 `List<ITool>` | → 移入 Core，加上部門前綴驗證 |
| `Infrastructure/ToolFactory.cs:32` — 硬編碼 `new` 工具 | → 改為組件掃描（`AddToolsFromAssembly`） |
| `Tools/Mechanical/`、`Tools/Manual/` | → PoC 比較用產物，MVP 保留 Manual 風格，遷入首個部門專案 |

> PoC 的 Mechanical vs Manual 對照已完成其任務（PoC-REPORT.md 結論：Manual 勝出），MVP 不再維護 Mechanical 分支。

---

## 後果

### 正面

- ✅ Agent 只看到相關部門工具 → LLM 準確率不隨全公司工具總數下滑
- ✅ 部門各自發版，互不中斷連線
- ✅ 故障隔離：SPC gateway 掛不影響 report agent
- ✅ 認證/稽核/PII 遮蔽單一實作，安全審核面積最小
- ✅ **安全加值**：各部門可持有各自的 NTLM 系統帳號（ADR-006），下游權限爆炸半徑縮小
- ✅ Agent 端只需認識一個 hostname 與一份憑證

### 負面

- ⚠️ N 個 container → N 份監控目標、N 條 pipeline（部分由共用 build template 攤平）
- ⚠️ 引入版本漂移風險（由 D3 三層防護化解，但需持續維運 `MIN_SUPPORTED`）
- ⚠️ 需要 ingress 具備路徑分流能力（企業內網現況待確認 → 見待決事項）
- ⚠️ 工具名變長（部門前綴），略增 token 消耗
- ⚠️ 需要內部 NuGet feed（現況待確認 → 見待決事項）
- ⚠️ 跨部門 agent 需維持多條 MCP 連線（SK / Pydantic AI 皆支援，但連線數與逾時處理需驗證）

---

## 實作規劃

### Phase 1：Core 抽出（MVP，與 ADR-006 併行）

- [ ] 建立 `McpGateway.Core` 專案，stdio → ASP.NET Core Streamable HTTP
- [ ] 實作 `AddMcpGateway()` / `AddToolsFromAssembly<T>()` / `RunMcpGateway()`
- [ ] ADR-006 認證代理直接寫入 Core（**不先寫進單體**）
- [ ] ADR-004 啟動驗證移入 Core，加入 D6 部門前綴檢查
- [ ] 稽核日誌 + PII 遮蔽移入 Core
- [ ] 內部 NuGet feed 發布流程
- [ ] Core 版本 metric（D3 第 3 層）

**估時**：ADR-006 原定 13.5 人天 + 約 2 人天結構成本

### Phase 2：首個部門專案（MVP）

- [ ] 建立 `McpGateway.{首個部門}`，引用 Core
- [ ] 遷入 5–10 支高價值工具（Manual 風格，ADR-003 內聯描述）
- [ ] 部署驗證：ingress 路徑分流 `mcp.corp.local/{dept}`
- [ ] Agent 端接線驗證（SK + Pydantic AI 各一）

### Phase 3：第二個部門（驗證可複製性）

**觸發條件**：第二個部門提出需求

- [ ] 全程記錄新部門上手工時（目標：< 1 人天完成骨架）
- [ ] 建立共用 CI build template（含 D3 第 2 層 `MIN_SUPPORTED` 閘門）
- [ ] 驗證跨部門 agent 同時連兩個 endpoint 的行為

**若新部門上手 > 3 人天，代表 D2 邊界劃錯，需重新檢討。**

---

## 待決事項

| # | 問題 | 需確認對象 | 阻斷開發？ |
|---|------|-----------|-----------|
| 1 | Core package 由哪個團隊擁有與維護？（平台團隊 or 首個部門代管） | 架構團隊 | ❌ 否，但 Phase 3 前須定 |
| 2 | 內部 NuGet feed 是否已存在？（Azure Artifacts / BaGet / 檔案共享） | DevOps | ⚠️ **是**，Phase 1 需要 |
| 3 | 內網 ingress 是否支援路徑分流？（K8s Ingress / nginx / 既有 F5） | DevOps | ⚠️ **是**，Phase 2 需要 |
| 4 | `MIN_SUPPORTED` 版本下限由誰維護、多久檢視一次？ | 平台 + 安全團隊 | ❌ 否 |
| 5 | 部門代號（`report`/`spc`）由誰核發？如何避免兩部門搶同一代號？ | 架構團隊 | ❌ 否 |
| 6 | 每部門工具數量上限建議值？（LLM 準確率天花板落在幾支？） | 需實測 | ❌ 否，但應納入 PoC 後續量測 |
| 7 | ADR-006 的 Redis Token Cache：N 個服務共用一座，或各自一座？ | DevOps + 安全 | ❌ 否（建議共用，JWT 驗證結果可跨部門重用） |
| 8 | 各部門是否各持一組 NTLM 系統帳號？（縮小爆炸半徑 vs 帳號管理成本） | 安全團隊 | ❌ 否 |
| 9 | 是否需要 Core 支援「dev 單 process 掛載多部門」？ | — | ❌ 否（現決策：dev 只跑正在改的專案） |

---

## 相關 ADRs

| ADR | 關聯 |
|-----|------|
| [ADR-001](ADR-001-use-mcp-protocol.md) | 本 ADR 確定 Streamable HTTP 的落地時機（MVP Phase 1），並將傳輸層收進 Core |
| [ADR-003](ADR-003-config-driven-descriptions.md) | 內聯 `DescriptionAttribute` 留在部門專案；Core 負責掃描與套用 |
| [ADR-004](ADR-004-startup-validation.md) | 啟動驗證移入 Core，**擴充**部門前綴檢查（D6） |
| [ADR-005](ADR-005-tool-versioning.md) | 版本後綴與部門前綴相容：`report_get_order_status_v2`。Tool Catalog（> 50 支）觸發條件改為**跨部門總和** |
| [ADR-006](ADR-006-security-model.md) | 認證代理實作於 Core，僅一份。**建議 ADR-006 實作直接落在 Core，不經單體階段** |
| ADR-007（未撰寫） | 測試策略需涵蓋「Core 契約測試」— Core 改動不得破壞既有部門專案 |
| ADR-008（未撰寫） | 快取策略需考量 N 個服務共用 Redis 的鍵命名空間 |

---

## 決策記錄

**提案來源**：`skill://grill-me` grilling session，2026-08-01
**Grilling 涵蓋分支**：驅動力 → 拓撲 → 職責邊界 → 版本漂移 → URL 形狀 → 發現機制 → 命名衝突 → 實作時機（8 項全數收斂）

**批准前必須**：
- [ ] 架構團隊審核本 ADR
- [ ] DevOps 確認待決事項 #2（NuGet feed）與 #3（ingress 路徑分流）
- [ ] 與 ADR-006 實作團隊確認：認證直接寫入 Core，不經單體階段

**是否阻斷開發**：
- ⚠️ **部分**。ADR-006 預定 2026-08-02 開工；本 ADR 若未在此前定案，認證可能被寫進單體，日後抽離需 5–8 人天並重走安全審核。
- **建議**：於 2026-08-02 前完成待決事項 #2、#3 確認並批准本 ADR。

---

*最後更新：2026-08-01*
*狀態：待架構團隊審核*
