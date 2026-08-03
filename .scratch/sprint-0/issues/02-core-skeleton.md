# 02 — Core 專案骨架 + NuGet 發布流程

**What to build:** 建立 McpGateway.Core class library 專案骨架，含目錄結構、package metadata、依賴項，並驗證可發布至內部 NuGet feed 且可被新專案 restore。**僅骨架，不實作任何功能**。

**Blocked by:** None — 可立即開始

**Status:** ready-for-agent

- [ ] 建立 `src/McpGateway.Core/` class library (net9.0)
- [ ] 建立目錄結構：Hosting/, Tools/, Auth/, Downstream/, Validation/, Audit/, Observability/, Projection/
- [ ] .csproj 設定：PackageId=McpGateway.Core, Version=0.1.0-preview, Authors, Description
- [ ] 加入依賴：ModelContextProtocol 1.4.1, ModelContextProtocol.AspNetCore 1.4.1
- [ ] 撰寫 README.md 說明 Core package 用途（參考 Core spec §1）
- [ ] `dotnet pack` 產出 .nupkg
- [ ] Push 至內部 NuGet feed (http://10.53.216.186:5000/v3/index.json)
- [ ] 建立測試專案驗證可 restore McpGateway.Core 0.1.0-preview
