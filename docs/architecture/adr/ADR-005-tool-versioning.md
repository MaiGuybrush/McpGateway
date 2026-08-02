# ADR-005: Tool 版本控制策略

## 狀態
**✅ 已批准**（採用選項 B：Tool 層級版本號）

**批准人**：架構團隊（2026-08-01）

**實作時機**：MVP+1（當**單一部門** Tool 數量 > 10 或出現破壞性變更時）

**🔄 已修訂**（2026-08-02，因 ADR-009 釐清計數範圍）

> **變更摘要**：本 ADR 原以「Tool 數量」作為所有階段的觸發條件，在單一服務架構下無歧義。
> [ADR-009](ADR-009-department-gateway-split.md) 拆分為 N 個部門服務後，「Tool 數量」需明確區分
> **單一部門計數** 與 **全公司加總**。原決策（選項 B、Semantic Versioning、棄用政策）**不變**。
> 另加註本 ADR 已否決的「選項 D：Git 分支版本」與 ADR-009 的部門拆分**並非同一件事**。

## 背景

設計文件第 7 節提到："未來 Tool 數量成長後（數十支），是否需要管理介面"

但在 grilling 中識別：**版本控制是更基礎的問題**。當：

1. **Tool 介面需要改變**（參數增刪）
2. **下游 API 升級**（v1 → v2）
3. **破壞性變更**無法避免

現有設計沒有版本機制，這將導致：

- **Agent 相容性災難**：升級 Tool Facade 後，所有 Agent 可能同時失效
- **原子升級不可能**：無法逐步遷移 Agent 到新版本
- **無法回滾**：一旦部署，發現問題也無法回到舊版

## 決策

**採用選項 B：Tool 層級版本號**

### 決策理由

1. **長期可維護性**
   - 當 Tool 數量 > 20，無版本管理將導致「升級地獄」
   - 破壞性變更無法避免，需平滑遷移機制
   - Agent 逐步遷移，降低風險

2. **逐步導入策略**
   - MVP（<10 Tools）：無版本，約束向後相容
   - MVP+1（10-20 Tools）：導入版本號，不棄用舊版
   - 長期（>20 Tools）：棄用流程 + Tool Catalog

   > **【ADR-009 修訂】計數範圍**：上述 Tool 數量一律指**單一部門內**的工具數。
   > 版本控制要解決的是「該部門 agent 的遷移痛」，痛感隨該部門工具數成長，與其他部門無關。
   > 唯一例外是 Tool Catalog（見下方「工具生命週期管理」），它是跨部門盤點工具，以**全公司加總**計數。

3. **成本可控**
   - 實作簡單（加 version 欄位）
   - 遷移平滑（同時部署兩版本）
   - 可根據使用量決定棄用時機

4. **風險低**
   - 無版本時，約束向後相容
   - 有版本後，可容忍破壞性變更
   - 棄用期可控（建議 6 個月）

### 三個選項對比

| 面向 | 選項 A（無版本） | 選項 B（Tool 層級版本） | 選項 C（命名空間） | 選項 D（Git 分支） |
|------|----------------|---------------------|----------------|---------------|
| **實作複雜度** | 無 | 低（加欄位） | 中（命名空間） | 極高 |
| **當 Tool=50** | 崩潰 | 可控 | 可控 | 災難 |
| **破壞性變更** | 不可能 | 平滑遷移 | 平滑遷移 | 困難 |
| **程式碼重複** | 無 | 短期有 | 短期有 | 大量 |
| **推薦度** | ⚠️ 僅限 MVP | ✅ **推薦 ** | ⚠️ 複雜度高 | ❌ 不建議 |

## 選項 B 實作細節

**策略**：
```json
{
  "tools": [
    {
      "id": "report_get_order_status",       // 含部門前綴（ADR-009 D6）
      "version": "1.0",                       // 👈 新增版本欄位
      "name": "report_get_order_status_v1",   // 名稱含部門前綴 + 版本後綴
      "deprecated": false                     // 標記棄用
    }
  ]
}
```

> **【ADR-009 D6】完整命名格式**：`{department}_{intent}[_v{major}]`
>
> ```
> report_get_order_status        ← MVP（無版本）
> report_get_order_status_v2     ← MVP+1（導入版本後）
> ```
>
> 部門前綴由 `McpGateway.Core` 於啟動時強制驗證（見 [ADR-004](ADR-004-startup-validation.md) 檢查項 3），
> 不符則 fail-fast 不予啟動。版本後綴則為本 ADR 的可選機制，由各部門自行決定何時導入。
>
> 本 ADR 後續段落的工具名範例（如 `get_order_status_v1`）為求簡潔省略部門前綴，
> **實際實作一律須帶部門前綴**。

**變更管理流程**：

1. **非破壞性變更**（加可選參數、改描述）：
   - 維持版本號不變（1.0 → 1.0）
   - 直接部署

2. **破壞性變更**（刪參數、改必填、改類型）：
   - 新版本號（1.0 → 2.0）
   - 舊版本標記 `deprecated: true`
   - 同時部署兩個版本
   - Agent 逐步遷移
   - 觀察舊版使用量，降至 0 後移除

**棄用策略**：

```csharp
// 標記棄用，但在維護期內仍可使用
[Obsolete("請遷移至 get_order_status_v2，v1 將於 2026-12-31 移除")]
[McpServerTool("get_order_status_v1")]
public async Task<...> GetOrderStatusV1(...) { }

// 新版本
[McpServerTool("get_order_status_v2")]
public async Task<...> GetOrderStatusV2(...) { }
```

**優點**：
- 平滑遷移
- 逐步推出
- 可回滾
- 清晰的生命週期

**缺點**：
- 需維護多個版本（短期）
- 設定檔變複雜
- 需追蹤棄用時間表

### 選項 C：命名空間版本（替代）

**策略**：版本在名稱中，但用命名空間組織

```csharp
namespace ToolFacade.Tools.V1
{
    public class OrderTools { /* ... */ }
}

namespace ToolFacade.Tools.V2
{
    public class OrderTools { /* ... */ }
}
```

**優點**：
- 編譯期隔離
- 清晰的程式碼組織

**缺點**：
- 程式碼重複（棄用期）
- 命名空間污染

### 選項 D：Git 分支版本（不建議）

**策略**：每個版本一個 Git 分支，獨立部署

**缺點**：
- 運維複雜度爆炸
- 資源浪費（多個服務實例）
- 版本碎片化
- ❌ 強烈不建議

> ### ⚠️ 【ADR-009 澄清】選項 D 與「部門拆分」不是同一件事
>
> [ADR-009](ADR-009-department-gateway-split.md) 採用了「多個服務實例」的拓撲，
> 表面上與本選項 D 被否決的理由（「資源浪費：多個服務實例」）看似矛盾。**兩者切分軸完全不同**：
>
> | | 選項 D（已否決） | ADR-009 部門拆分（已採用） |
> |---|---|---|
> | 切分軸 | **版本**（v1 / v2 / v3） | **部門**（report / spc / qc） |
> | 實例數量成長 | 隨版本數成長，**無上限且會碎片化** | 隨部門數成長，**有限且穩定** |
> | 實例生命週期 | 短暫（棄用後即刪） | 長期（部門長期存在） |
> | 程式碼來源 | 同一份程式碼的 N 個歷史分支 | N 份不同的工具程式碼 + 共用 Core |
> | 產生的價值 | 無（版本可在同一實例內共存） | 部署獨立、故障隔離、LLM 準確率 |
>
> **關鍵區別**：多版本可在**同一個部門服務內**共存（選項 B 的做法 —— 同時註冊
> `report_get_order_status_v1` 與 `_v2`），不需要為此開新實例。
> 但多部門**無法**在同一服務內達成部署獨立與故障隔離。
>
> 故：**部門拆分成立，版本拆分不成立**。本 ADR 選項 D 的否決結論不受 ADR-009 影響。

## 版本模式建議

### Semantic Versioning（語義化版本）

參考：https://semver.org/

```
版本格式：MAJOR.MINOR.PATCH

MAJOR (X.0.0)：
- 破壞性變更（參數刪除、類型改變、必填改變）

MINOR (0.Y.0)：
- 向後相容（新增可選參數、新增 response 欄位）

PATCH (0.0.Z)：
- Bug 修復（改描述文字、改實作但不改介面）
```

### 版本支援政策

建議：
- **維護窗口**：每個 MAJOR 版本支援 6 個月
- **遷移通知**：棄用時通知，提供 3 個月遷移期
- **強制移除**：6 個月後硬性移除

## 工具生命週期管理（未來）

### 長期願景

當 **全公司加總** Tool 數量 > 50 時，需要：

> **【ADR-009 修訂】此處採全公司加總計數**，與版本機制的 per-department 計數不同。
> Tool Catalog 的價值正在於**跨部門盤點** —— 找出重複造輪子、命名不一致、無人維護的工具。
> 若各部門各自維護目錄，就失去它存在的意義。故 Catalog 由**平台團隊**維護，資料由各部門服務上報。

**1. Tool Catalog（工具目錄）**
```yaml
# tools-catalog.yaml（平台團隊維護，資料由各部門 gateway 上報）
apiVersion: v1
tools:
  - id: report_get_order_status      # 含部門前綴（ADR-009 D6）
    department: report               # 【ADR-009 新增】
    gateway_url: /report             # 【ADR-009 新增】
    core_version: "1.2.4"            # 【ADR-009 新增】追蹤 package 落後狀況
    owner: @order-team
    created: 2026-07-01
    deprecated: null | 2026-12-31
    deprecation_reason: "已遷移至 v2"
    usage_stats:
      daily_calls: 1250
      unique_agents: ["agent1", "agent2"]
```

**資料來源**：各部門 gateway 於啟動時或定期將自身工具清單上報至 Catalog，**不需人工維護 YAML**。這也順帶解決 ADR-009 D3 第 3 層遙測的可見度需求（Catalog 即可看出哪個部門 Core 版本落後）。

**2. Tool 管理介面（CLI + Web UI）**

```bash
# CLI 工具
tool-facade list-tools --department report        # 【ADR-009】部門過濾
tool-facade inspect-tool report_get_order_status_v2
tool-facade deprecate-tool report_get_order_status_v1 --reason "遷移至 v2"

# 【ADR-009 新增】跨部門盤點
tool-facade find-duplicates                       # 找出語意重複的跨部門工具
tool-facade list-stale-core                       # 找出 Core 版本落後的部門
```

**3. Agent 遷移輔助**

```bash
# 分析 Agent 使用狀況
tool-facade analyze-usage --agent my-agent
# 輸出：此 Agent 使用 report_get_order_status_v1，建議遷移至 v2
#      此 Agent 連接 2 個部門 gateway：/report, /spc
```

## 實作規劃

### Phase 1：MVP（< 10 Tools）— 當前階段

**策略**：無版本，但建立約束

**約束**：
- [ ] 所有變更必須向後相容（加可選參數、不改現有邏輯）
- [ ] 破壞性變更必須開新 Tool ID（而非版本號）
- [ ] 建立 Tool 變更日誌（Tool Changelog）

** 時程 **：2026-08-01 ~ 2026-09-01（1 個月）

### Phase 2：MVP+1（10-20 Tools）— 啟動條件

** 觸發條件 **（任一）：
- [ ] **單一部門** Tool 數量 > 10
- [ ] 出現破壞性變更需求（無法避免）

> **【ADR-009】per-department 觸發**：各部門獨立達標、獨立導入。
> `report` 部門有 12 支工具即可導入版本號，不需等 `spc` 部門也達標。
> Core 只需提供版本機制，是否啟用由各部門自行決定（`appsettings` 開關）。

** 實作內容 **：
- [ ] 在設定檔加入 `version` 欄位（可選，預設 "1.0"）
- [ ] 在設定檔加入 `deprecated` 標記（可選，預設 false）
- [ ] 啟動時同時註冊多版本（如果有）
- [ ] 在監控中追蹤版本使用量（prometheus metric）
- [ ] 建立棄用流程文件

** 時程 **：1 Sprint（2 週）

### Phase 3：長期（> 20 Tools）— 啟動條件

** 觸發條件 **（任一）：
- [ ] **單一部門** Tool 數量 > 20（棄用流程）
- [ ] **單一部門**版本數量 > 10（需要管理界面）
- [ ] **全公司加總** Tool 數量 > 50（Tool Catalog，見下）

**實作內容**：
- [ ] Tool Catalog（YAML 或 DB）
- [ ] 棄用通知機制（Email / Slack）
- [ ] Agent 遷移輔助工具（分析使用狀況）
- [ ] Web UI（瀏覽器管理介面）

**時程**：1 Milestone（1 個月）

## 相關 ADRs

- **ADR-003**：版本變更的頻率與範圍（描述變更是否算破壞性變更）
- **ADR-004**：啟動時驗證需考慮版本相容性（孤兒路徑）；部門前綴驗證（檢查項 3）與版本後綴相容
- **ADR-006**：安全模型（版本存取權限）；稽核日誌已含 `ToolVersion` 與 `Department` 欄位
- **[ADR-009](ADR-009-department-gateway-split.md)**：**計數範圍來源**。版本機制觸發條件採 per-department 計數；Tool Catalog 採全公司加總；工具命名格式 `{department}_{intent}[_v{major}]`；並澄清本 ADR 選項 D 與部門拆分之區別

## 決策記錄

**審核**：架構審查團隊 grilling session 2026-07-31  
**批准**：架構團隊負責人（2026-08-01）  
**實作**：Phase 2（MVP+1）啟動條件：Tool 數量 > 10  

---
*最後更新：2026-08-01*
*相關文件：* 
- 版本遷移計劃（`version-migration-plan.md`）
- Tool Catalog 設計（`tool-catalog-design.md`）
