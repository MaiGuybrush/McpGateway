# Tool Registration 修正報告

**日期**: 2026-08-03  
**原因**: Sprint 0 初版 spike 漏了必要 attribute 標註

---

## 問題

初版 README.md 記錄「Tool Registration ❌」，宣稱：
> `.WithTools<T>()` 方法無法正確掃描並註冊工具的靜態方法

**此結論錯誤。**

---

## 根本原因

SDK 1.4.1 **已完整支援** attribute-based tool discovery，但需**同時標註兩個 attribute**：

```csharp
[McpServerToolType]  // ← 類別層級：標記此類別包含工具
public class Tools
{
    [McpServerTool]  // ← 方法層級：標記此方法為工具
    public static string Echo(string message) => $"Echo: {message}";

    [McpServerTool]
    public static int Add(int a, int b) => a + b;
}
```

初版程式碼**漏了這兩個標註**，導致工具未被註冊。

---

## 修正後測試結果

### ✅ tools/list（2 個工具成功註冊）

```bash
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

**回應**:
```json
{
  "result": {
    "tools": [
      {
        "name": "echo",
        "description": "",
        "inputSchema": {
          "type": "object",
          "properties": {"message": {"type": "string"}},
          "required": ["message"]
        }
      },
      {
        "name": "add",
        "description": "",
        "inputSchema": {
          "type": "object",
          "properties": {"a": {"type": "integer"}, "b": {"type": "integer"}},
          "required": ["a", "b"]
        }
      }
    ]
  },
  "id": 1,
  "jsonrpc": "2.0"
}
```

### ✅ tools/call echo

```bash
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"message":"Hello MCP"}}}'
```

**回應**:
```json
{
  "result": {
    "content": [{"type": "text", "text": "Echo: Hello MCP"}]
  },
  "id": 2,
  "jsonrpc": "2.0"
}
```

### ✅ tools/call add

```bash
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"add","arguments":{"a":10,"b":32}}}'
```

**回應**:
```json
{
  "result": {
    "content": [{"type": "text", "text": "42"}]
  },
  "id": 3,
  "jsonrpc": "2.0"
}
```

---

## Sprint 0 Gate 影響

| Gate 條件 | 原狀態 | 修正後 |
|----------|--------|--------|
| Tool registration | ❌ 失敗 | ✅ **通過** |
| tools/list | ❌ | ✅ **通過** |
| tools/call | ❌ | ✅ **通過** |

**結論**: SDK 1.4.1 完全可用於 Sprint 1+，無需升級至 2.0（但 2.0 仍建議，見下）。

---

## SDK 升級建議（基於 agent 研究）

### 選項 A: 升級至 2.0.0 ✅ **仍建議**

- **無 breaking changes**，tool registration API 完全相容
- 新功能：tool metadata (Title, Destructive, ReadOnly...)
- Protocol 驗證更嚴格（更符合規範）
- **阻礙**: 內部 NuGet feed 僅到 1.4.1

**行動**:
1. 推送 2.0.0 至內部 feed（DevOps）
2. 或暫時允許 Core 專案用 nuget.org source

### 選項 B: 繼續用 1.4.1 ✅ **可接受**

- ✅ 已驗證可用
- ✅ attribute-based registration 完整支援
- ⚠️ 錯過新功能（非阻斷）

---

## 修正清單

- [x] Program.cs 加入 `[McpServerToolType]` + `[McpServerTool]`
- [x] 驗證 tools/list 成功
- [x] 驗證 tools/call 成功
- [x] 撰寫此修正報告
- [ ] 更新 RESULTS.md（標註初版結論錯誤）
- [ ] 更新 Sprint 0 gate report

---

**修正人**: Claude Sonnet 4.5  
**驗證日期**: 2026-08-03 13:45
