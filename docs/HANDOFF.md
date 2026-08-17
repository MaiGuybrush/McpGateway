# MCP Gateway 欄位標準化與結構化資料治理 — 交接與總結文件 (Handoff Summary)

## 1. 任務背景與總結 (Overview)

本階段工作解決了跨系統 MCP Gateway（Report、MES、EAP、SPC、QC 等）在提供 AI Agent 工具時，欄位命名分歧（例如 `ProductCode` vs `prod_id` vs `productId`）、缺乏結構化資料回傳（舊版採 Markdown 純文字）以及缺乏統一編譯/啟動期檢查機制的核心問題。

本文件記錄了本次架構演進的交付成果、設計決策（包含 ADR-010）、重要討論共識與後續維護指引。

---

## 2. 已完成之架構與成果 (Completed Deliverables)

所有規劃之 8 項垂直切片任務（Tickets）已全數實作完成並通過自動化測試與方案建置：

### (1) 規範與參考文件
* **正式架構決策紀錄 (ADR)**：[`docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md`](file:///D:/Projects/.NET/McpGateway.Core/docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md)
* **規格需求書 (PRD)**：`McpGateway.Report/docs/SPEC-MCP-TOOL-FIELD-STANDARDIZATION.md`
* **決策歷程記錄**：`McpGateway.Report/docs/DECISION-LOG.md`
* **開發者指引手冊**：`McpGateway.Report/docs/DEVELOPER-GUIDE-FIELD-STANDARDIZATION.md`
* **自定義詞庫 Schema**：`McpGateway.Report/docs/mcp-glossary.schema.json`

### (2) 核心程式碼與元件 (Core & Analyzers)
* **編譯期分析器 [`McpGateway.Analyzers`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Analyzers/McpGateway.Analyzers.csproj)**：
  * `MCP0010` (Warning)：檢測已知歷史別名（如 `ProductCode`），提示轉換為標準詞（如 `productId`）。
  * `MCP0011` (Warning)：檢測 DTO 公開屬性遺漏 `[Description]`。
  * `MCP0012` (Info)：命名駝峰格式規範。
  * `CodeFixProvider`：支援 IDE 燈泡一鍵修復與註解插入。
* **詞庫治理機制 (Domain Glossary)**：
  * `CoreGlossary.cs`：內建 Tier 1 製造業通用核心詞庫與同義詞對照表。
  * `GlossaryLoader.cs`：支援載入與合併專案本地 `mcp-glossary.json` (Tier 2)。
* **啟動期驗證器 [`ToolStartupValidator.cs`](file:///D:/Projects/.NET/McpGateway.Core/src/McpGateway.Core/Validation/ToolStartupValidator.cs)**：
  * 遞迴掃描所有 Tool 的 Output DTO 物件圖譜，若缺少 `[Description]` 則記錄 Warning Log。
* **示範重構 (`McpGateway.Report/Tools/Report/QueryWipTool.cs`)**：
  * 標註 `[McpServerTool(UseStructuredContent = true)]`。
  * 輸出改為不可變 Record DTO（`WipReportResponse`, `WipItemDto`, `WipSummaryDto`），全數採用 Tier 1 詞彙與 `[property: Description]`。

---

## 3. 關鍵架構概念與討論共識 (Key Insights & Consensus)

### Q1：什麼是「Tier 1」？它是否只作用在 DTO 第一層？
* **非也**。「Tier」代表**「組織治理層級（Governance Tier）」**而非「JSON 物件深淺層次（Nesting Level）」。
  * **Tier 1**：中央核心標準字典（全廠區跨系統通用，由 Core 套件統一發布）。
  * **Tier 2**：專案自定義字典（各部門於專案 `mcp-glossary.json` 自行擴充）。
* 不論 Tier 1 還是 Tier 2，分析器與啟動驗證器皆會**遞迴深入檢查所有層級的 DTO（包含清單項目與巢狀物件）**。

### Q2：初期無法窮舉所有 Alias，如何解決開發者主動維護困難的問題？
* **三層互補治理架構**：
  1. **撰寫階段（AI 生成即標準）**：透過 Copilot / AI 助手生成 DTO 時，Prompt 直接注入 Tier 1 規範，一開始就產出標準名稱。
  2. **編譯階段（Roslyn 快速把關）**：抓取 20% 高頻知名別名，並透過 `MCP0011` 強制要求開發者寫中文說明（`[Description]`）。開發者寫中文說明比記住英文標準名容易得多。
  3. **PR / CI 階段（AI 語意分析與字典自動生長）**：在 CI 引入輕量 LLM 分析「未知欄位名 + 中文說明」，自動推薦標準化映射或自動提 PR 擴充別名庫（Self-growing Glossary）。
  4. **Runtime 強韌性**：只要每個欄位都有中文 `[Description]`，LLM 作為 MCP Client 在 Runtime 就能精準推理理解，即使遇到特殊歷史欄位也不會造成工具鏈接崩潰。

---

## 4. 下一步建議與推薦 Skills (Suggested Next Steps & Skills)

1. **推廣至其他 Gateway 專案**：
   * 參考 `McpGateway.Report.csproj`，在 `McpGateway.Spc`、`McpGateway.Qc` 等專案引用 `McpGateway.Analyzers`。
   * 建議使用技能：`/implement`
2. **CI/CD AI 語意審查腳本開發（AI Glossary Miner）**：
   * 在 CI Pipeline 中新增一個 CLI 工具，於 PR 階段自動比對新欄位與 Description，並自動沈澱 Alias。
   * 建議使用技能：`/implement` 或 `/plan`
3. **代碼審查與架構評估**：
   * 建議使用技能：`/code-review`
