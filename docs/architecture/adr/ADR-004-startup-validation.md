# ADR-004: 啟動時參數描述驗證機制

## 狀態
**✅ 已簡化**（因 ADR-003 選擇選項 B，無需複雜驗證）

## 背景

設計文件 5.3 節提到啟動時驗證：
- flatten JSON Schema 取得參數路徑清單
- 與設定檔 descriptions 做 diff
- "參數存在但無描述" → fail-fast，阻擋啟動
- "描述存在但無參數" → warning

在 grilling 中識別出嚴重問題：

### 識別的風險

1. **啟動失敗 = 部署阻塞**
   - 描述文字更新變成"破壞性變更"
   - DevOps pipeline 被意外阻斷
   - 緊急修復需繞過檢查
   
2. **JSON Schema Flatten 的歧義性**
   - `oneOf`/`anyOf`/`allOf` 如何處理？
   - `array` of `object` 的路徑格式？
   - `additionalProperties` 怎麼攤平？
   - **結果**: 可能產生不一致的路徑表示，驗證邏輯複雜且脆弱

3. **錯誤恢復困難**
   - 生產環境因新增參數但未更新描述而啟動失敗
   - 緊急措施：手動修改設定檔？這違反 GitOps 原則
   - 需要 hotfix 部署，繞過 CI/CD

## 決策

**✅ 採用「輕量驗證」模式（因 ADR-003 選擇選項 B）**

### 理由

**ADR-003 選擇選項 B（程式碼內聯描述）** 後，驗證機制大幅簡化：

- ✅ **描述一定存在**：在 C# Attribute 中，編譯時期即綁定
- ✅ **無設定檔脫鉤**：沒有 JSON 設定檔需要同步
- ✅ **無啟動驗證負擔**：不需要複雜的 JSON Schema flatten + diff
- ✅ **開發者體驗好**：編譯錯誤馬上知道，IDE 支援好

### 簡化後的驗證邏輯（僅剩基本檢查）

```csharp
public class ToolRegistrationService
{
    public void RegisterTool(MethodInfo method)
    {
        // 1. 檢查是否有 McpServerTool Attribute
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        if (toolAttr == null)
        {
            throw new InvalidOperationException($"方法 {method.Name} 缺少 [McpServerTool]");
        }
        
        // 2. 檢查所有參數是否有 Description Attribute
        foreach (var param in method.GetParameters())
        {
            if (param.GetCustomAttribute<DescriptionAttribute>() == null)
            {
                throw new InvalidOperationException(
                    $"參數 {param.Name} 缺少 DescriptionAttribute"
                );
            }
        }
        
        // 3. 註冊 Tool（無需複雜驗證）
        Register(toolAttr.Name, toolAttr.Description, parameters);
    }
}
```

**特點**：
- 編譯期或啟動時 fail-fast（清晰錯誤）
- 無需 JSON Schema flatten（避開複雜演算法）
- 無需設定檔 diff（無設定檔）

## 與 ADR-003 的關聯（關鍵）

### 情境 1：如果 ADR-003 保持選項 B（程式碼內聯）

**當前狀態**：✅

- 驗證機制 = 簡單 Attribute 檢查（50 行程式碼）
- 風險 = 低
- 複雜度 = 低

**建議**：
- 維持現狀（無需複雜驗證）
- 啟動時檢查 Attribute 存在即可
- 不需要 Metric 或 Warning

### 情境 2：如果 ADR-003 未來升級到選項 C（混合方案）

**未來可能**：有需求時才實作

- 驗證機制 = 需要額外邏輯
- 風險 = 中
- 複雜度 = 中

**影響**：
- 設定檔加 `descriptionOverrides`（選填）
- 驗證邏輯需檢查：
  - 所有參數都有 Attribute 描述（基本）
  - 覆寫路徑存在於參數結構（避免孤兒路徑）

**實作**（僅在升級到 C 時才加）：
```csharp
if (config.DescriptionOverrides != null)
{
    foreach (var (paramPath, overrideDesc) in config.DescriptionOverrides)
    {
        // 檢查路徑是否存在
        if (!ParameterPathExists(paramPath, method))
        {
            _logger.LogWarning($"孤兒路徑: {paramPath} 不存在於參數結構");
        }
    }
}
```

**決定**：可在 B→C 遷移時再實作（非阻塞）

### 情境 3：如果 ADR-003 改用選項 A（設定檔驅動）

**不會發生**：已排除此選項

- 風險極高（雙重維護、啟動阻塞）
- 與 ADR-003 決策相反

## 當前建議（基於 ADR-003 選 B）

### 啟動時驗證邏輯（簡化版）

**1. 基本檢查（必須）**

```csharp
public void RegisterAllTools(IEnumerable<MethodInfo> methods)
{
    var errors = new List<string>();
    
    foreach (var method in methods)
    {
        try
        {
            // 檢查有 [McpServerTool]
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttr == null) continue; // 不是 Tool 方法
            
            // 檢查所有參數有 [Description]
            var missingDescriptions = method.GetParameters()
                .Where(p => p.GetCustomAttribute<DescriptionAttribute>() == null)
                .Select(p => p.Name)
                .ToList();
            
            if (missingDescriptions.Any())
            {
                errors.Add(
                    $"Tool {method.Name} 參數缺少描述: {string.Join(", ", missingDescriptions)}"
                );
            }
        }
        catch (Exception ex)
        {
            errors.Add($"註冊 {method.Name} 失敗: {ex.Message}");
        }
    }
    
    if (errors.Any())
    {
        throw new InvalidOperationException(
            $"啟動驗證失敗:\n{string.Join("\n", errors)}"
        );
    }
}
```

**2. 建議：不要 Warning（因為 B 方案描述一定存在）**

原因：
- Attribute 遺漏 = 編譯錯誤或啟動失敗
- 無 silent failure 風險
- 不需要 Metrics 追蹤

**3. 例外：如果升級到 C（混合方案）**

```csharp
// 僅在升級到 C 時才需要
if (config.DescriptionOverrides != null)
{
    WarnOnOrphanPaths(config.DescriptionOverrides, method);
}
```

## 決策總結

**當前狀態**：✅ 已簡化（因 ADR-003 選 B）

**實作建議**：
- 啟動時檢查 Attribute 是否存在（50 行程式碼）
- 不需要複雜的 JSON Schema flatten
- 不需要 Warning 或 Metrics
- 如有遺漏，啟動時拋出清晰錯誤

**未來保留**：若升級到 C（混合方案），再實作孤兒路徑 Warning

**風險**：無（簡單且安全）

**成本**：低（50 行程式碼）

---
*最後更新：2026-08-01*
*依賴：ADR-003 選項 B（已批准）*
*未來演化：若 ADR-003 升級到 C，再補強驗證*
