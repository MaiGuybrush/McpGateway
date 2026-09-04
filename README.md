# McpGateway.Core

[![Status](https://img.shields.io/badge/status-Active%20Development-green)](#)
[![Spec](https://img.shields.io/badge/Spec-Core%20Spec-blue)](./docs/specs/mcp-gateway-core-spec.md)
[![Plan](https://img.shields.io/badge/Plan-Development%20Plan-blue)](./docs/specs/development-plan.md)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

`McpGateway.Core` is the foundational .NET 9.0 library for building enterprise Model Context Protocol (MCP) gateways. It transforms internal REST APIs into LLM-friendly MCP tools with built-in authentication, audit logging, PII redaction, and metrics observability.

---

## 🎯 Purpose & Architecture

`McpGateway.Core` is designed according to **[ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md)** (per-department gateway topology).

```
 Agent (/report)   Agent (/spc)   Agent (/qc)
        └───────────────┼───────────────┘
                        │ MCP (Streamable HTTP)
                        ▼
             Ingress — mcp.corp.local
        ┌───────────────┼───────────────┐
   /report            /spc            /qc
        ▼               ▼               ▼
  McpGateway.Report  McpGateway.Spc  McpGateway.Qc    ← Independent Department Repos
   └─ .Core          └─ .Core        └─ .Core         ← Shared Core Library
        └───────────────┼───────────────┘
                        ▼
                 Ocelot Gateway
              ┌─────────┴─────────┐
        Java Spring Boot      C#/.NET services
```

### Why a Shared Core Library?
1. **Consistency**: Authentication (JWT, API Key, NTLM), token caching, PII redaction, and audit logging are implemented **once** in Core.
2. **Autonomous Deployment**: Departments depend on `McpGateway.Core` as a NuGet package and maintain their own repos & deployment lifecycles.
3. **Low Onboarding Overhead**: Department gateways require minimal boilerplate in `Program.cs`.

---

## 🏗️ Repository Structure

```
McpGateway.Core/
├── src/
│   ├── McpGateway.Core/                 # Core NuGet package source (.NET 9.0)
│   │   ├── Auth/                        # Auth degradation, JWT/API-Key/NTLM proxies
│   │   ├── Cache/                       # Memory & Redis cache abstractions
│   │   ├── Configuration/               # Option validators & department contracts
│   │   ├── Downstream/                  # Ocelot HTTP client & resilience
│   │   ├── Hosting/                     # AddMcpGateway / RunMcpGatewayAsync
│   │   ├── Observability/               # Prometheus metrics & health checks
│   │   ├── Tools/                       # ToolBase & tool registry infrastructure
│   │   └── Validation/                  # Startup validation
│   └── MockOcelotApi/                   # Mock downstream API server for testing
├── tests/
│   ├── CorePackageTest/                 # Package contract tests
│   ├── McpGateway.Core.IntegrationTests/ # Integration tests with WireMock / Mock API
│   └── k6/                              # Performance and load testing scripts
├── docs/                                # ADRs, specs, and architectural documents
└── McpGateway.Core.sln                  # Main Visual Studio Solution
```

---

## 🚀 建立新部門 Gateway (Department Scaffolding)

依據 **[ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md)** 獨立儲存庫原則與 **[SPEC-SCAFFOLD-001](./docs/specs/spec-department-scaffold.md)** 標準化規範，本專案提供自動化腳手架腳本 [`templates/scaffold.ps1`](./templates/scaffold.ps1)，協助各製造與業務部門（如 EAP、SPC、EDC、報表等）一鍵產出符合企業架構規範、開箱即用的 `McpGateway.<Department>` C# .NET 9 專案骨架。

### 📌 快速產生範例

開啟 PowerShell 執行腳本（建議將 `-OutDir` 設為與 `McpGateway.Core` 同層之目錄）：

```powershell
# 1. (建議) 先使用 -DryRun 預覽預計產生的檔案清單與設定映射
pwsh .\templates\scaffold.ps1 `
    -Department eap `
    -Port 5200 `
    -ToolName eap_query_lot `
    -AuthProvider API-KEY `
    -UseConsul $true `
    -Solution `
    -OutDir .. `
    -DryRun

# 2. 正式執行專案產生
pwsh .\templates\scaffold.ps1 `
    -Department eap `
    -Port 5200 `
    -ToolName eap_query_lot `
    -AuthProvider API-KEY `
    -UseConsul $true `
    -Solution `
    -OutDir ..
```

### 💡 做法與參數建議 (Best Practices)

| 參數 | 必填 | 預設值 | 做法建議與規範說明 |
|---|---|---|---|
| `-Department` | **是** | - | **部門代碼**：長度需介於 2~20 字元，不可使用保留字（如 `core`, `test`, `gateway`, `common`, `shared` 等）。產出專案命名採用 PascalCase（如 `eap` $\rightarrow$ `McpGateway.Eap`），設定檔中則維持小寫。 |
| `-Port` | **是** | - | **服務通訊埠**：範圍 1024~65535。執行前請先查閱 [`templates/ports.md`](./templates/ports.md) 確認並登記部門專屬 Port，避免多部門本地開發或佈署時發生通訊埠衝突。 |
| `-ToolName` | **是** | - | **主要 MCP 工具名稱**：必須為小寫 `snake_case` 且**強制以 `<department>_` 為前綴**（例如 `eap_query_lot`）。符合 ADR-009 D6 與 Core `ToolStartupValidator` 啟動強制檢核，避免多 Gateway 掛載時同名工具碰撞。 |
| `-AuthProvider` | 否 | `API-KEY` | **認證模式**：支援 `API-KEY`、`JWT`、`NTLM`、`None`。企業內部整合建議採用預設 `API-KEY`（內建 UAC 驗證、降級容錯與記憶體快取）。 |
| `-UseConsul` | 否 | `$true` | **服務發現整合**：預設啟用 Consul 集中廠別端點解析。若部門不需依賴 Consul，設定為 `$false` 即可自動切換至本地 `FallbackShops` 設定。 |
| `-Solution` | 否 | `$false` | **方案檔**：若指定 `-Solution`，會自動在目標資料夾建立 `McpGateway.<Department>.sln` 並掛載專案檔，方便 Visual Studio 直接開啟。 |
| `-OutDir` | 否 | `.\` | **輸出路徑**：建議指向獨立工作目錄（例如 `..` 或獨立部門 Git Repo），確保符合獨立 Repository 演進之架構原則。 |
| `-DryRun` | 否 | `$false` | **預覽模式**：僅列出設定參數與預計產生的檔案清單，不寫入磁碟，適合初次使用確認。 |
| `-Force` | 否 | `$false` | **覆寫防護**：若目標目錄已存在且非空，預設會主動報錯保護；僅在確認覆寫時指定 `-Force`。 |

### 🛠️ 專案產生後續步驟

專案產生完成後，即可依照標準流程進行驗證與業務邏輯實作：

```powershell
# 1. 切換至新建立的部門專案目錄
cd ..\McpGateway.Eap

# 2. 還原與驗證 NuGet 套件 (專案內建 nuget.config 已設定 BaGet 公司私有庫)
dotnet restore

# 3. 驗證專案編譯與 Roslyn 分析器 (自動套用 McpGateway.Analyzers 規則)
dotnet build

# 4. 啟動服務並驗證內建工具 (HelloTool 與 eap_query_lot)
dotnet run
```

啟動後服務將監聽於指定通訊埠（如 `http://localhost:5200/mcp`），後續開發業務 Tool 與 DTO 時，強烈建議搭配 [`mcp-tool-dto-builder`](./docs/ai-tools/skills/mcp-tool-dto-builder/SKILL.md) 遵循 [ADR-010](./docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 詞彙標準化規範。

---

## 🛠️ Usage in Department Gateways

A department gateway reference `McpGateway.Core` and registers its MCP tools:

### `Program.cs` Example
```csharp
using ModelContextProtocol.AspNetCore;
using McpGateway.Core.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. Add McpGateway Core services (Auth, Audit, Metrics)
builder.Services.AddMcpGateway();

// 2. Add MCP Server and register department tools
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<QueryWipTool>();

var app = builder.Build();

// 3. Map MCP endpoints and start gateway
app.MapMcp();
await app.RunMcpGatewayAsync();
```

---

## 🤖 MCP Tool 開發指南 (AI Tool Skill 推薦)

在開發新的 MCP Tool 或將既有 Tool 回傳結構重構為結構化契約時，強烈建議使用專屬的 AI Agent Skill：

👉 **[`mcp-tool-dto-builder`](./docs/ai-tools/skills/mcp-tool-dto-builder/SKILL.md)**（目錄路徑：`docs/ai-tools/skills/mcp-tool-dto-builder`）

### 核心功能與優勢：
- **動態詞庫探測 (Dynamic Vocabulary Probe)**：自動解析中央 `Tier1Vocabulary.cs` 核心製造業字典（本地工作區或遠端 Gitea Tag）。
- **欄位標準化與別名校正 (Canonicalization)**：自動識別歷史別名（如 `prod_id` $\rightarrow$ `ProductId`、`wip_qty` $\rightarrow$ `QuantityInProcess`）並修正為符合 [ADR-010](./docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) 的標準詞彙。
- **強型別不可變契約 (Structured Content)**：一鍵產出 Positional Record DTO、補齊繁體中文 `[Description]` 特性，並自動生成 `[McpServerToolType]` 工具方法骨架。
- **中央詞庫回饋提報 (PR Automation)**：自動進行衝突防呆檢核，並可透過 `tea` CLI / Git 向中央儲存庫發起詞庫擴充 PR。

---

## 📝 Key Decision Summary (ADRs)

| ADR | Decision | Status |
|-----|----------|--------|
| [ADR-001](./docs/architecture/adr/ADR-001-use-mcp-protocol.md) | MCP protocol, Streamable HTTP | ✅ Approved |
| [ADR-002](./docs/architecture/adr/ADR-002-dotnet-mcp-sdk-choice.md) | Official .NET MCP SDK | ✅ Approved |
| [ADR-003](./docs/architecture/adr/ADR-003-config-driven-descriptions.md) | Code-inline tool descriptions | ✅ Approved |
| [ADR-004](./docs/architecture/adr/ADR-004-startup-validation.md) | Startup validation & department prefix check | ✅ Approved |
| [ADR-005](./docs/architecture/adr/ADR-005-tool-versioning.md) | Tool-level versioning (deferred to MVP+1) | ✅ Approved |
| [ADR-006](./docs/architecture/adr/ADR-006-security-model.md) | Authentication proxy pattern | ✅ Approved |
| [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) | Per-department gateway repository split | ✅ Approved |
| [ADR-010](./docs/architecture/adr/ADR-010-mcp-tool-field-standardization-and-structured-dto.md) | MCP Tool 欄位命名標準化、結構化 DTO 回傳與編譯期分析器治理 | ✅ Approved |

---

## 🧪 Testing & Verification

```bash
# Build the Core solution
dotnet build McpGateway.Core.sln

# Run Core integration and package tests
dotnet test McpGateway.Core.sln

# Run Mock Ocelot API for local testing
dotnet run --project src/MockOcelotApi/MockOcelotApi.csproj
```

---

## 📄 License

MIT License — see [LICENSE](LICENSE) file for details.
