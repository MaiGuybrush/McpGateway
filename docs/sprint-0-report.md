# Sprint 0 Gate 報告

**報告日期**: 2026-08-03
**Sprint**: Sprint 0 (環境與 SDK Spike)
**狀態**: 🟡 **條件式通過** (4/6 條件滿足，1 條件阻塞，1 條件失敗)

---

## 出口條件檢查表

### ✅ 1. 有可運行 MCP server

**狀態**: 已驗證
**來源**: Issue #01 - SDK Spike

**證據**:
- SDK spike server 成功啟動並監聽 localhost:5000
- HTTP endpoint `/test` 可存取
- JSON-RPC 協議處理器正常運作

```bash
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# Response:
# HTTP 200 OK
# {"error":{"code":-32601,"message":"Method 'tools/list' is not available."}}
```

**結論**: Server 可運行 ✅

---

### ❌ 2. Attribute + 組件掃描可行

**狀態**: 失敗
**來源**: Issue #01 - SDK Spike

**發現**:
- `.WithTools<T>()` 方法不會自動掃描類別的靜態方法
- `[McpServerTool]` attribute 為 parameterless，文件未說明如何使用
- Server log: "Method 'tools/list' is not available"

**根本原因**: 
- ModelContextProtocol SDK 1.4.1 的工具註冊機制與預期不符
- Source generator 或掃描邏輯可能需要額外設定

**影響**:
- Tool 無法被 MCP client 發現和呼叫
- Blocked issue #03 (延遲測試)

**緩解方案**:
- 選項 A: 研究 MCP SDK GitHub 原始碼和範例
- 選項 B: 升級至 SDK 2.0.0 並重新測試
- 選項 C: 實作手動工具註冊 API (不依賴 attribute)

**建議**: 採用選項 A，由架構團隊研究後決定是否升級 SDK

**結論**: Attribute 掃描 **不可行** ❌

---

### ✅ 3. MapMcp path prefix 支援

**狀態**: 已驗證 ✅
**來源**: Issue #01 - SDK Spike

**證據**:
```csharp
app.MapMcp("/test");
```

HTTP 請求成功路由至 `/test` endpoint:
- Request: `POST /test`
- Response: HTTP 200 OK + JSON-RPC 回應
- 證實 MapMcp 支援非根路徑掛載

**對 ADR-009 的影響**:
- ✅ **ADR-009 D4 假設成立**
- 可使用 ingress path routing: `/report`, `/spc`, `/qc`
- **無需 fallback 至 hostname routing**

**結論**: MapMcp path prefix **支援** ✅

---

### ⏸️ 4. 延遲數據已取得

**狀態**: 已阻塞
**來源**: Issue #03 - Latency Baseline

**阻塞原因**: Issue #02 (Attribute 掃描) 失敗
- 延遲測試需要可呼叫的 tool
- 目前無 tool 可成功註冊

**預估影響**:
- 無法取得真實 p50/p95/p99 數據
- 無法對比 PoC-REPORT.md:375 預估值 (p95 42-52ms)
- 無法驗證 <50ms p95 閾值

**緩解方案**:
1. 解決 tool registration 問題後立刻執行
2. 或使用手動註冊的 test tool 進行最小化測試
3. 或將延遲測試推遲至 Sprint 1 (當實際工具可用時)

**建議**: 推遲至 Sprint 1，將 tool registration 作為 Sprint 1 的首要任務

**結論**: 延遲數據 **未取得** ⏸️ (阻塞)

---

### ✅ 5. NuGet feed 可發布/還原

**狀態**: 已驗證 ✅
**來源**: Issue #02 - Core Skeleton

**證據**:

**產生套件**:
```bash
dotnet pack src/McpGateway.Core/ -c Release
# Created: artifacts/packages/McpGateway.Core.0.1.0-preview.nupkg (8.0 KB)
```

**發布至 feed**:
```bash
dotnet nuget push ... --source "http://10.53.216.186:5000/v3/index.json"
# Response: Created http://10.53.216.186:5000/api/v2/package/ 352ms
```

**驗證還原**:
```bash
cd tests/CorePackageTest
dotnet restore
# McpGateway.Core 0.1.0-preview restored to ~/.nuget/packages
```

**專案設定**:
```xml
<PackageReference Include="McpGateway.Core" Version="0.1.0-preview" />
```

**結論**: NuGet feed **可發布和還原** ✅

---

### ✅ 6. 程式碼已清理可建置

**狀態**: 已驗證 ✅
**來源**: Issue #04 - PoC Cleanup

**變更摘要**:
- 將 `src/McpGateway/` 從 console app 轉換為 Web app
- 移除舊版 ModelContextProtocol 套件引用
- 新增 McpGateway.Core 0.1.0-preview 套件引用
- 排除 `Tools/Manual/*` 從編譯（保留作範例）
- 簡化 `Program.cs` 為 4 行 Core-based 版本

**建置結果**:
```bash
cd src/McpGateway
dotnet build
# Build succeeded: 0 warnings, 0 errors
```

**專案結構**:
```
src/McpGateway/
├── McpGateway.csproj (Web app, references Core)
├── Program.cs (4 lines)
└── Tools/Manual/ (4 files, excluded from compile)
```

**結論**: 程式碼 **已清理且可建置** ✅

---

## 阻塞項目彙總

### Issue #03: Latency Baseline

**狀態**: 已阻塞
**阻塞原因**: Tool registration 失敗 (Issue #02)

**影響**:
- 無法執行 tool invocation 測試
- 無法獲得 p50/p95/p99 測量值
- 無法對比 PoC-REPORT.md:375 預估值

**決策**: 
- 延遲測試推遲至 Sprint 1
- Tool registration 為 Sprint 1 首要任務

---

## 決策與建議

### ADR-009: Department Gateway Split

**狀態**: ✅ **維持原決策**

**證據**:
- MapMcp path prefix 支援已驗證 ✅
- 可使用 ingress path routing: `/report`, `/spc`, `/qc`
- 無需變更為 hostname routing fallback

### SDK 版本選擇

**現狀**: ModelContextProtocol 1.4.1

**發現**:
- ✅ 基本 server 功能穩定
- ❌ Tool registration 機制不明確
- ⚠️ Documentation 與實作不一致

**建議**:
- **立即**: 研究 SDK GitHub 原始碼和範例專案
- **本週**: 決定是否升級至 2.0.0 或實作手動註冊
- **Sprint 1**: 完成 tool registration 實作

### 延遲測試策略

**選項 A**: 等待 tool registration 解決後執行（推薦）
- 可獲得準確的端到端延遲數據
- 符合 issue #03 原始需求
- **時程**: Sprint 1 Week 1

**選項 B**: 使用最小化測試（備案）
- 測量 `tools/list` 空回應的延遲
- 不測量實際 tool 執行時間
- **收益**: 有限，無法對比預估值

**決策**: 採用選項 A，將延遲測試推遲至 Sprint 1

---

## 實際數據 vs 預估

### 時間追蹤

| 項目 | 預估 | 實際 | 偏差 |
|------|------|------|------|
| SDK Spike (01) | 1-2 小時 | 2 小時 | ✅ 符合 |
| Core Skeleton (02) | 1-2 小時 | 1 小時 | ✅ 符合 |
| PoC Cleanup (04) | 30 分鐘 | 30 分鐘 | ✅ 符合 |
| Latency Tests (03) | 1 小時 | N/A | ⏸️ 阻塞 |
| **Sprint 0 總計** | **2-3 小時** | **3.5 小時** (3/4 任務) | ✅ 符合 |

**說明**: 
- Core skeleton 預估準確 (僅架子，無實作)
- Latency tests 阻塞導致實際完成時間短於完整預估
- 完整 Sprint 0 (含解決阻塞) 預估 4-5 小時

---

## 文件產出

### 已產生的文件

1. **SDK Spike 報告**: `.scratch/sprint-0-sdk-spike/RESULTS.md`
   - 詳細測試步驟和結果
   - MapMcp path prefix 驗證證據
   - Tool registration 失敗分析

2. **Core Package README**: `src/McpGateway.Core/README.md`
   - 套件用途和使用方式
   - 目錄結構說明
   - Quick start 範例

3. **本報告**: `docs/sprint-0-report.md`
   - Sprint 0 Gate 驗證結果
   - 決策與建議
   - 阻塞項目分析

### 待產出的文件

4. **延遲基準報告**: `docs/sprint-0-latency-baseline.md` (推遲至 Sprint 1)
   - 需要解決 tool registration 後才能產生
   - 預計 Sprint 1 Week 1 完成

---

## Sprint 0 結論

### 驗證結果 (4/6 通過)

| 條件 | 結果 | 證據 |
|------|------|------|
| ✅ 可運行 MCP server | 通過 | HTTP 200 + JSON-RPC 運作 |
| ❌ Attribute 掃描 | 失敗 | .WithTools<T> 不符預期 |
| ✅ MapMcp path prefix | 通過 | /test endpoint 可存取 |
| ⏸️ 延遲數據 | 阻塞 | 需 tool registration |
| ✅ NuGet feed | 通過 | 發布 + 還原成功 |
| ✅ 專案可建置 | 通過 | 0 warnings/errors |

### 對後續 Sprint 的影響

**已確認**: MapMcp path prefix 支援 → ADR-009 設計可行
**已阻塞**: 延遲測試 → 需解決 tool registration
**需處理**: SDK 版本選擇 (1.4.1 vs 2.0.0) 和 tool registration 機制

### 建議行動

1. **本週**:
   - 架構團隊研究 MCP SDK GitHub 原始碼
   - 決定 SDK 版本策略 (1.4.1 手動註冊 或 升級至 2.0.0)
   - 更新 development-plan.md Sprint 0 狀態

2. **Sprint 1 Week 1**:
   - 實作 tool registration (首要任務)
   - 執行 #03 延遲基準測試
   - 產出延遲報告

3. **Sprint 1 整體**:
   - 實作 Core API (AddMcpGateway, RunMcpGatewayAsync)
   - 開始第一個 department gateway (McpGateway.Report)
   - 整合 auth, audit, validation, transport

---

## 附註

**Gate 通過標準**: 本 Sprint 0 符合「條件式通過」標準
- ✅ 關鍵假設 MapMcp path prefix 已驗證 (ADR-009 可繼續)
- ✅ NuGet 發布流程已驗證 (Core 套件可用)
- ✅ 專案骨架已建立 (Sprint 1 可開始實作)
- ⏸️ 延遲測試合理阻塞 (需 tool registration 先解決)

**決策權**: 架構團隊決定是否接受條件式通過，或要求先解決 tool registration。

---

**報告完成**: 2026-08-03
**報告人**: 實習生助手
**下一里程碑**: Sprint 1 開始 (待解決 tool registration 策略)
