# 04 — 清理無法編譯程式碼

**What to build:** 清理 src/McpGateway/ 殘留的無法編譯程式碼，將 Program.cs 改為 3 行 Core-based 版本（引用 Core package），移除已刪除檔案的 .csproj 參照，確認專案可建置。

**Blocked by:** 02-core-skeleton（需 Core package 可用）

**Status:** ready-for-agent

- [ ] 檢查 src/McpGateway/McpGateway.csproj 移除已刪除檔案參照（Infrastructure/, Tools/Mechanical/ 等）
- [ ] 加入 PackageReference: McpGateway.Core 0.1.0-preview
- [ ] 改寫 Program.cs 為 Core-based 3 行版本（參考 Core spec §3.1）
- [ ] `dotnet build src/McpGateway/` 確認可建置無錯誤
- [ ] 保留 Tools/Manual/* (4 files) 作為範例（不改動）
- [ ] 保留 MockOcelotApi/, tests/ 測試專案（不改動）
