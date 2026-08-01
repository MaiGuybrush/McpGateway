# ADR-003: Tool 參數描述策略 —— 程式碼內聯為主，設定檔覆寫為輔

## 狀態
**已批准**（採用選項 B → C 演進路徑）

## 背景

在 grilling 過程中發現核心矛盾：

- **主張**：參數描述應放在 `tools-config.json`，以便"無需重新編譯即可調整提示詞"
- **識別的問題**：
  - 描述文字本質上是程式邏輯（影響 LLM 行為），外置造成雙重維護負擔
  - 設定檔與 C# 參數結構脫鉤，無編譯期檢查
  - 需啟動時驗證，失敗時服務無法啟動（阻塞部署）
  - 遺漏描述變成"破壞性變更"，違反直覺

## 決策

**採用選項 B（程式碼內聯描述）作為 Phase 1 實作方案**

**未來保留演進至選項 C（混合方案）的彈性**

### 決策理由

#### 為何 Phase 1 選擇 B？

1. **極簡啟動（MVP Friendly）**
   - 無設定檔負擔，專注核心邏輯
   - 開發者體驗一致（程式碼即文件）
   - 快速迭代，無需擔心設定檔同步

2. **編譯期安全**
   - Attribute 遺漏 = 編譯錯誤（或靜態分析警告）
   - 無運行期驚喜
   - IDE 重構支援（Rename、Move 自動更新描述）

3. **降低初期複雜度**
   - 無需啟動時驗證（描述一定存在）
   - 無需 JSON Schema flatten（避免 ADR-004 風險）
   - 無需設定檔治理流程（Code Review 就夠）

4. **團隊技能考量**
   - C# Attribute 是標準做法
   - 學習曲線低
   - 符合 .NET 生態系慣例

#### 為何保留 C 的彈性？

1. **未來可能需求**
   - A/B 測試描述文字（需要動態調整）
   - 頻繁微調（> 每週 1 次）
   - Prompt 工程師需要直接修改權限
   - Tool 數量 > 20，設定檔管理更方便

2. **B→C 遷移成本低**
   - 工作量：約 3 人天（24 小時）
   - 風險：低（向下相容，無破壞性變更）
   - 時機：可逐步導入（不用 Big Bang）

3. **風險可控**
   - 如果 B 夠用，可永久不導入 C
   - 保留選擇權，而非現在決定一切
   - 實際體驗後再決策

### 三個選項的比較

| 面向 | 選項 A（純設定檔） | 選項 B（純程式碼） | 選項 C（混合） | B→C 演進 |
|------|-------------------|-------------------|--------------|----------|
| **啟動速度** | 中（需同步設定檔） | ✅ **快**（無設定檔） | 慢（最複雜） | B 快，C 可選 |
| **維護成本** | 高（雙重維護） | ✅ **低**（單一點） | 中（兩處） | B 低，逐步加 |
| **編譯檢查** | ❌ 無 | ✅ **有** | 有 | 同 B |
| **部署風險** | 高（遺漏=阻塞） | ✅ **低** | 中 | B 低 |
| **動態修改** | ✅ 可 | ❌ 不可 | ✅ 可 | C 時才需要 |
| **初期複雜度** | 中 | ✅ **低** | 高 | B→C 漸進 |

**結論**：B 最適合 Phase 1，C 作為未來選項

## 實作細節（選項 B）

### 描述方式選擇

**方案：使用 System.ComponentModel.DescriptionAttribute**

```csharp
using System.ComponentModel;

[McpServerTool("get_order_status")]
public async Task<OrderStatus> GetOrderStatus(
    [Description("欲查詢的訂單編號，格式如 SO-2026-000123"))]
    string orderId,
    
    [Description("查詢區間開始日期（YYYY-MM-DD）")]
    DateTime startDate,
    
    [Description("查詢區間結束日期（YYYY-MM-DD）")]
    DateTime endDate
) { /* ... */ }
```

**優點**：
- 標準函式庫（無額外依賴）
- IDE 支援（自動完成、導航）
- 明確意圖（專門用於描述）

**替代方案：XML Documentation** （不推薦）
```csharp
/// <summary>
/// 查詢訂單狀態
/// </summary>
/// <param name="orderId">欲查詢的訂單編號，格式如 SO-2026-000123</param>
[McpServerTool]
public async Task<OrderStatus> GetOrderStatus(...) { }
```

缺點：
- 需 Source Generator 或 T4 模板提取
- 複雜度高，MVP 不必要

### 啟動邏輯（反射讀取 Attribute）

```csharp
public class ToolRegistrationService
{
    public void RegisterTool(ToolConfig config, MethodInfo method)
    {
        // 1. 讀取 Attribute 描述
        var paramDescriptions = method.GetParameters()
            .ToDictionary(
                p => p.Name,
                p => p.GetCustomAttribute<DescriptionAttribute>()?.Description 
                     ?? throw new InvalidOperationException($"參數 {p.Name} 缺少 DescriptionAttribute")
            );
        
        // 2. 生成 Tool（與現有邏輯相同）
        var tool = CreateTool(
            config.Name, 
            config.Description, 
            paramDescriptions
        );
        
        // 3. 註冊
        Register(tool);
    }
}
```

**重要**：
- 描述遺漏 = `InvalidOperationException`（啟動時拋出，清晰錯誤）
- 等同編譯期檢查（啟動時 fail-fast）

## 未來演進至選項 C（混合方案）

### 觸發條件（任一）

- [ ] **A/B 測試需求**：需要同時測試兩種描述，快速切換
- [ ] **頻繁調整痛點**：描述優化頻率 > 每週 1 次
- [ ] **Tool 數量 > 20**：設定檔批量管理比程式碼修改方便
- [ ] **Prompt 工程師參與**：非工程師需要直接修改描述

### B→C 遷移步驟（約 3 人天）

 **工作 1：設定檔結構修改**（4 小時）
```json
{
  "tools": [{
    "id": "get_order_status",
    "descriptionOverrides": {
      "orderId": "更精準的覆寫描述"
    }
  }]
}
```

**工作 2：啟動邏輯重構**（8 小時）
```csharp
public void RegisterTool(ToolConfig config, MethodInfo method)
{
    // 1. 讀取 Attribute（與 B 相同）
    var paramDescriptions = GetAttributeDescriptions(method);
    
    // 2. 套用 JSON 覆寫（新增）
    if (config.DescriptionOverrides != null)
    {
        foreach (var (paramName, overrideDesc) in config.DescriptionOverrides)
        {
            paramDescriptions[paramName] = overrideDesc;
        }
    }
    
    // 3. 生成 Tool（同 B）
    RegisterTool(config.Name, config.Description, paramDescriptions);
}
```

**工作 3：測試覆蓋**（8 小時）
```csharp
[Fact]
public void B_to_C_Migration_ShouldWork()
{
    // Arrange：既有 B 的 Tool
    var config = new ToolConfig 
    { 
        Id = "get_order_status",
        DescriptionOverrides = new Dictionary<string, string> 
        { 
            ["orderId"] = "覆寫描述" 
        } 
    };
    
    // Act
    var tool = RegisterTool(config, methodWithAttribute);
    
    // Assert：覆寫生效
    Assert.Equal("覆寫描述", tool.Parameters["orderId"].Description);
}
```

**工作 4：文件更新**（4 小時）
- 修訂本 ADR（加入 C 的細節）
- 更新開發者文件
- 團隊分享

### 遷移風險（低）

- **程式碼變更**：僅啟動邏輯（50 行）
- **既有 Tool 影響**：無（Attribute 保留）
- **設定檔相容性**：完全向下相容（`descriptionOverrides` 可選）
- **回滾難度**：容易（移除 JSON 覆寫即回 B）

### 遷移後的運作模式

```
描述來源（優先順序）：
1. JSON 覆寫（如果存在）
2. Attribute（預設）
3. 無描述 → 啟動失敗（fail-fast）
```

**特色**：
- 99% 情況使用 Attribute（與 B 相同）
- 只有需要調優的 Tool 才加 JSON 覆寫
- 無覆寫 = 完全等同 B 行為
- 逐步導入，不用 Big Bang

## 常見問題

### Q1: 為何不直接做 C？

** 理由 **：
- C 比 B 複雜（合併邏輯、設定檔、測試）
- 如果 B 就夠用，C 的複雜度是浪費
- MVP 應極簡，驗證核心價值

**時機**：如果一開始就知道需要 A/B 測試或頻繁調整，可直接做 C

### Q2: 如果一直不升級到 C，會有技術債嗎？

** 技術債評估 **：
- ✅ **無負面影響**：B 本身是完整方案
- ⚠️ **機會成本**：如果後來需要 C，需回頭重構啟動邏輯（3 人天）
- ✅ **可控**：3 人天成本可接受，且不是緊急

**結論**：不是技術債，是「可選的未來投資」

### Q3: 如何決定是否升級到 C？

**量化指標**（建議）：
- 描述調整頻率 > 每週 1 次
- 參與調整人員 > 3 人（工程師 + Prompt 工程師）
- A/B 測試需求明確

**質化指標**（主觀）：
- 團隊抱怨「改描述要發布很麻煩」
- Prompt 工程師想要自主調整權限
- 發現描述優化能顯著提升 LLM 正確率

## 與其他 ADRs 的關聯

- **ADR-004**：驗證機制（B 無需驗證，C 需測試覆寫邏輯）
- **ADR-006**：設定檔結構（C 需 `descriptionOverrides`）
- **ADR-005**：版本控制（描述變更是否算破壞性變更？）

## 決策記錄

**審核**：架構團隊 grilling session 2026-07-31  
**批准**：架構團隊負責人（待簽名）  
**實施**：MVP Phase 1 採用選項 B（程式碼內聯）  
**未來演進**：保留選項 C（混合方案）彈性

---
*最後更新：2026-07-31*  
*相關文件*：
- B→C 演進分析（`b-to-c-migration-analysis.md`）
- Tool Facade 設計文件（`docs/tool-facade-design-doc.md`）
- Grilling 審查報告（`docs/architecture/GRILLING-SUMMARY.md`）

## 後續行動

### Phase 1（B）實作清單
- [ ] 建立 `DescriptionAttribute` 使用範例（1-2 個 Tool）
- [ ] 實作啟動邏輯（反射讀取 Attribute）
- [ ] 建立單元測試（驗證 Attribute 遺漏行為）
- [ ] 更新開發者文件
- [ ] 團隊分享（Attribute 使用方式）

### 未來升級到 C 的準備
- [ ] 在設定檔結構保留 `descriptionOverrides` 欄位（可選，先不實作）
- [ ] 在啟動邏輯留擴充點（註解標記）
- [ ] 建立 B→C 遷移待辦清單（供未來使用）

### 監控指標（決定是否升級到 C）
- [ ] 追蹤描述調整頻率（每週/每月）
- [ ] 記錄調整所花時間（發布成本）
- [ ] 追蹤 Tool 數量（到 20 個時評估）
- [ ] 蒐集團隊回饋（是否覺得 B 不方便？）

---
*本 ADR 經 grilling 嚴格審查，確保決策品質*
*最後更新：2026-07-31*
*相關文件：B→C 演進分析（`b-to-c-migration-analysis.md`）*
