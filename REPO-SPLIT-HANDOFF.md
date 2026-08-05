# McpGateway Repository 拆分執行手冊

**日期**：2026-08-05  
**決策來源**：Grilling session — 評估 repo 是否需拆分  
**狀態**：待執行

---

## 背景

### 現況

```
D:\Projects\.NET\McpGateway\              ← git repo (master)
├── src/
│   ├── McpGateway.Core/                  ← 平台共用 package（無 .git）
│   ├── McpGateway.Report/                ← 第一個 department（有獨立 .git）
│   ├── McpGateway/                       ← 舊 PoC（不能 compile）
│   └── MockOcelotApi/                    ← 測試 mock
├── tests/
│   ├── CorePackageTest/
│   ├── McpGateway.Core.IntegrationTests/
│   ├── McpGateway.Report.IntegrationTests/
│   ├── McpGateway.Report.E2ETests/
│   └── McpGateway.Tests/                 ← 測舊 PoC
├── .scratch/sprint-0-sdk-spike/          ← SDK 驗證 spike（已完成）
├── docs/                                 ← ADR + specs + glossary
└── McpGateway.sln
```

### 問題

- `McpGateway.Report` 已有獨立 .git（nested repo），但仍在外層 .sln 裡
- 舊 PoC、spike 等無用 artifacts 未清理
- Repo 定位不清：是 monorepo 還是 Core repo？

---

## 決策摘要

| 議題 | 決策 | 理由 |
|------|------|------|
| Core 與 department 分離？ | ✅ 各自獨立 repo | ADR-009：部署獨立、發版自主 |
| Department repo 策略 | 每個 department 一個 repo | 完整 ownership |
| 現有 repo 定位 | 轉型成 `McpGateway.Core` | 實質內容就是 Core + 文件 |
| Development plan 去處 | 留在 Core repo | Core 是平台團隊主場 |
| 舊 PoC (`src/McpGateway/`) | 全刪 | 不能 compile，無參考價值 |
| MockOcelotApi | 留在 Core repo | Core 測試需要 |
| Sprint 0 spike | 刪除 | 已完成任務 |
| .sln 處理 | 重建（不手動編輯） | 乾淨簡單 |

---

## 執行步驟

### 步驟 2：移動 Report 到上層

**目標**：Report 完全獨立到 `D:\Projects\.NET\McpGateway.Report\`

```bash
# 2.1 移動 Report 本體
mv src/McpGateway.Report ../McpGateway.Report

# 2.2 在 Report repo 加入測試
cd ../McpGateway.Report
mkdir -p tests
cp -r ../McpGateway/tests/McpGateway.Report.IntegrationTests tests/
cp -r ../McpGateway/tests/McpGateway.Report.E2ETests tests/
git add tests/
git commit -m "chore: add integration and E2E tests from main repo"

# 2.3 回外層 repo，移除 Report 測試
cd ../McpGateway
git rm -r tests/McpGateway.Report.IntegrationTests
git rm -r tests/McpGateway.Report.E2ETests
git commit -m "chore: remove Report tests (moved to Report repo)"
```

**注意**：
- Report 測試的 git history 會斷掉（copy 不是 move）—— 已同意接受
- 外層 repo 不需 `git rm src/McpGateway.Report`（它本來就沒追蹤，因為有 .git）

---

### 步驟 3：清理外層 repo

**目標**：刪除舊 PoC、spike、Report 殘留

```bash
# 確認在外層 repo
cd D:/Projects/.NET/McpGateway

# 3.1 刪除舊 PoC
git rm -r src/McpGateway/

# 3.2 刪除舊 PoC 測試
git rm -r tests/McpGateway.Tests/

# 3.3 刪除 spike
git rm -r .scratch/sprint-0-sdk-spike/

# 3.4 Commit
git commit -m "chore: remove PoC and spike artifacts"
```

**刪除理由**：
- `src/McpGateway/`：不能 compile，development plan 已明確標示捨棄
- `tests/McpGateway.Tests/`：測舊 PoC，無用
- `.scratch/sprint-0-sdk-spike/`：spike 已完成，結論已記錄在 development plan

---

### 步驟 4：重建 .sln

**目標**：新 `.sln` 只包含 Core 相關 projects

```bash
# 4.1 刪除舊 .sln
rm McpGateway.sln

# 4.2 建立新 .sln
dotnet new sln -n McpGateway.Core

# 4.3 加入 Core 本體
dotnet sln McpGateway.Core.sln add src/McpGateway.Core/McpGateway.Core.csproj

# 4.4 加入 MockOcelotApi（測試基礎設施）
dotnet sln McpGateway.Core.sln add src/MockOcelotApi/MockOcelotApi.csproj

# 4.5 加入測試專案
dotnet sln McpGateway.Core.sln add tests/CorePackageTest/CorePackageTest.csproj
dotnet sln McpGateway.Core.sln add tests/McpGateway.Core.IntegrationTests/McpGateway.Core.IntegrationTests.csproj

# 4.6 Commit
git add McpGateway.Core.sln
git commit -m "chore: rebuild solution for Core repo"
```

**不包含**：
- `tests/k6/`（JavaScript script，不需加進 .NET solution）

---

### 步驟 5：Repo 改名

**目標**：目錄名從 `McpGateway` → `McpGateway.Core`

```bash
# 5.1 回到上層目錄
cd D:/Projects/.NET

# 5.2 改名
mv McpGateway McpGateway.Core

# 5.3 進入新目錄
cd McpGateway.Core
```

**注意**：
- 沒有 git remote，不需更新 remote URL
- 改名後所有未 push 的 commits 仍在

---

### 步驟 6：更新文件

**目標**：文件反映 repo 已拆分

```bash
# 6.1 更新 README.md
# 手動編輯：
#   - 標題改成「McpGateway.Core」
#   - 說明這是 Core package repo
#   - 移除 Report 相關段落
#   - 更新架構圖（不再有 nested Report）

# 6.2 檢查其他文件
# 需檢查：
#   - docs/specs/development-plan.md（可能提到 repo 結構）
#   - docs/specs/mcp-gateway-core-spec.md（可能有路徑範例）
#   - 任何提到「McpGateway repository」的地方

# 6.3 Commit
git add README.md docs/
git commit -m "docs: update for Core repo split"
```

**重點更新**：
- README.md 開頭說明：這是 `McpGateway.Core` —— 平台共用 NuGet package
- 移除 `McpGateway.Report` 相關的「使用範例」（Report 現在有自己的 repo）
- 更新「專案結構」段落（不再列 Report）

---

### 步驟 7：驗證

**目標**：確認兩個 repo 都能獨立運作

```bash
# 7.1 驗證 Core repo
cd D:/Projects/.NET/McpGateway.Core
dotnet build McpGateway.Core.sln
dotnet test McpGateway.Core.sln

# 7.2 驗證 Report repo
cd D:/Projects/.NET/McpGateway.Report
dotnet build  # 假設 Report 有 .sln 或 .csproj
dotnet test   # 含新加入的測試

# 7.3 檢查檔案結構
cd D:/Projects/.NET
tree -L 2 McpGateway.Core
tree -L 2 McpGateway.Report
```

**驗證清單**：
- [ ] Core repo build 成功
- [ ] Core tests 通過
- [ ] Report repo build 成功
- [ ] Report tests（integration + E2E）通過
- [ ] Core repo 不再有 Report 殘留（`.sln` / `tests/`）
- [ ] Report repo 有完整測試（integration + E2E）

---

## 最終結果

### 檔案系統結構

```
D:\Projects\.NET\
├── McpGateway.Core\                      ← 外層 repo（已改名）
│   ├── src/
│   │   ├── McpGateway.Core/              ← NuGet package
│   │   └── MockOcelotApi/                ← 測試 mock
│   ├── tests/
│   │   ├── CorePackageTest/
│   │   ├── McpGateway.Core.IntegrationTests/
│   │   └── k6/
│   ├── docs/
│   │   ├── architecture/adr/             ← ADRs
│   │   └── specs/                        ← Core spec + dev plan
│   ├── McpGateway.Core.sln
│   └── .git
│
└── McpGateway.Report\                    ← Report 獨立 repo
    ├── (Report 的專案結構)
    ├── tests/
    │   ├── McpGateway.Report.IntegrationTests/
    │   └── McpGateway.Report.E2ETests/
    └── .git
```

### Repository 職責

| Repo | 職責 | Owner | 發版節奏 |
|------|------|-------|---------|
| `McpGateway.Core` | 平台共用 NuGet package | 平台團隊 | 按需（CVE / 新功能） |
| `McpGateway.Report` | Report department gateway | Report 團隊 | 獨立（不影響其他 dept） |
| `McpGateway.Spc`（未來） | SPC department gateway | SPC 團隊 | 獨立 |

---

## 風險與注意事項

### ⚠️ Git history 斷裂

- Report 的測試 history 會斷掉（從 Core repo copy 過去時）
- **已同意接受**：Report 測試剛建立，history 不多

### ⚠️ 文件更新遺漏

- 執行步驟 6 時需仔細檢查所有文件
- 特別注意：路徑範例、架構圖、「如何開始」段落

### ⚠️ 未來新 department

未來新增 department（`.Spc`、`.Qc`）時：
- 直接在 `D:\Projects\.NET\` 建獨立 repo
- 不要再 nested 到任何 repo 裡
- 參考 `McpGateway.Report` 的結構

---

## Checklist（執行前確認）

- [ ] 已閱讀完整手冊
- [ ] 確認當前在 `D:\Projects\.NET\McpGateway`
- [ ] 確認沒有 uncommitted changes（`git status` 乾淨）
- [ ] 確認 `src/McpGateway.Report/.git` 存在
- [ ] 備份當前狀態（optional，可 `git bundle` 或 zip 整個目錄）

## Checklist（執行後驗證）

- [ ] Core repo build/test 通過
- [ ] Report repo build/test 通過
- [ ] Core repo 不再有 Report 痕跡
- [ ] Report repo 有完整測試
- [ ] README.md 已更新
- [ ] 文件路徑引用已更新

---

**執行者**：另起 session 執行  
**預估時間**：30–45 分鐘（含驗證）
