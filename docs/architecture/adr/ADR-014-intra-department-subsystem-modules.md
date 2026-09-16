# ADR-014: 部門內部多系統模組化與端點分流架構 (Modular Monorepo + Subsystem Tool Isolation)

## 狀態
**✅ 已提議 / 達成共識 (Proposed / Consensus Reached)**（2026-09-16）

---

## 背景 (Context)

在 [ADR-009](ADR-009-department-gateway-split.md) 中，我們確立了跨部門（如 Report、SPC、EAP）以「Core Package + 各部門獨立 Repo / 服務 + Ingress 分流」的微服務架構。

然而，在**單一部門內部（Intra-Department）**（例如製造資訊部 MFG、企業應用處 IT），業務往往涵蓋 3~5 個彼此關聯但職責獨立的子系統（例如 MES 製造執行、WMS 倉儲管理、EAP 機台連線、EDC 工程資料收集）。這些子系統通常由部門內**不同的專責工程師或小組**進行維護。

### 現況痛點

1. **程式碼與維護權限混雜 (Lack of Ownership & Git Conflicts)**：
   若整個部門僅使用單一專案檔（`McpGateway.<Dept>.csproj`）與單一進入點，所有子系統的工程師將在同一目錄下修改程式碼。這會造成：
   - Git PR 頻繁衝突。
   - 無法利用 `CODEOWNERS` 機制將特定子系統的審核權限精確隔離給對應負責人。
   - 缺乏物理編譯邊界，子系統間容易發生不當的型別耦合。
2. **LLM 工具膨脹與跨系統干擾 (Tool Explosion & Hallucination)**：
   每個子系統通常需要提供 2~5 支 MCP 工具。若部門內所有系統工具均註冊至單一 MCP 端點（如 `/mfg/mcp`），工具總數將膨脹至 15~25 支以上。這會導致：
   - 消耗大量 Token 描述無關工具。
   - 專門處理倉儲查詢的 Agent 容易被相鄰語意的製造工具干擾，降低 LLM 呼叫準確率。
3. **組態與下游服務干擾 (Configuration Pollution)**：
   各子系統呼叫不同的後端 API（Downstream BaseUrl、Timeout 等），若平鋪於單一組態區段，命名容易碰撞，Consul 動態更新時亦容易相互影響。

---

## 決策 (Decision)

我們決定在部門 Gateway 專案架構中導入**方案級模組化（Modular Monorepo）**，並在 `McpGateway.Core` 中實作**無狀態請求層級之端點分流與工具隔離機制**。

```
                    Agent 端請求（依系統精確連線）
                               │
                ┌──────────────┴──────────────┐
                ▼                             ▼
       /mfg/mes/mcp                  /mfg/wms/mcp
     (MES 專屬 Agent)              (WMS 專屬 Agent)
                │                             │
                └──────────────┬──────────────┘
                               │
                               ▼
    ┌─────────────────────────────────────────────────────────┐
    │  McpGateway.Mfg.Host (薄宿主 Web Application)           │
    │  - 統一 Ingress 路由: /{dept}/{system}/mcp               │
    │  - API-KEY 認證與 Audit 中介軟體                        │
    │  - ConfigureSessionOptions 動態白名單過濾               │
    └──────────────┬───────────────────────────┬──────────────┘
                   │                           │
                   │ (ProjectReference)        │ (ProjectReference)
                   ▼                           ▼
    ┌───────────────────────────┐ ┌───────────────────────────┐
    │   McpGateway.Mfg.Mes      │ │   McpGateway.Mfg.Wms      │
    │   (Class Library 專案)     │ │   (Class Library 專案)     │
    │   - MesTools (2~5 支)     │ │   - WmsTools (2~5 支)     │
    │   - MesServices           │ │   - WmsServices           │
    │   - AddMesSubsystem()     │ │   - AddWmsSubsystem()     │
    └───────────────────────────┘ └───────────────────────────┘
```

---

### 1. 專案拓撲：Modular Monorepo（實體編譯期強隔離）

部門專案採用方案級多專案結構，統一收納於 `src/` 目錄：

```text
McpGateway.<Department>/
├── src/
│   ├── McpGateway.<Department>.Host/                  # 薄宿主 Web 進入點 (Thin Host)
│   │   ├── Program.cs                                 # 聚合各系統模組之 Composite Root
│   │   ├── appsettings.json                           # 宿主層級與各系統組態
│   │   └── McpGateway.<Department>.Host.csproj        # 專案參考各子系統專案
│   │
│   ├── McpGateway.<Department>.<SystemA>/             # 子系統 A Class Library (net9.0)
│   │   ├── Configuration/
│   │   │   └── <SystemA>Options.cs
│   │   ├── Services/
│   │   │   └── <SystemA>Service.cs
│   │   ├── Tools/
│   │   │   └── <SystemA>Tool.cs                       # 實作 MCP Tools (三段式命名)
│   │   ├── <SystemA>ModuleExtensions.cs               # 提供 Add<SystemA>Subsystem 擴充方法
│   │   └── McpGateway.<Department>.<SystemA>.csproj
│   │
│   └── McpGateway.<Department>.<SystemB>/             # 子系統 B Class Library
│       └── ...
├── tests/
│   ├── McpGateway.<Department>.<SystemA>.Tests/       # 各系統專屬測試專案
│   └── McpGateway.<Department>.<SystemB>.Tests/
└── McpGateway.<Department>.sln
```

- **職責劃分**：子系統維護者僅需在自己專屬的 `src/McpGateway.<Dept>.<System>/` 目錄下工作，享有獨立的命名空間、組態與單元測試。
- **權限控制**：在 Git Repository 中可直接配置 `.github/CODEOWNERS`，讓 `src/McpGateway.<Dept>.<System>/**` 的變更僅需該子系統專責人員核准即可合入。

---

### 2. 端點分流與工具隔離機制 (Subsystem Tool Isolation)

#### 路由規範
對外暴露標準三段式端點：
$$\text{/\{department\}/\{system\}/mcp}$$
例如：
- `/mfg/mes/mcp`
- `/mfg/wms/mcp`

#### 協議層工具隔離機制
鑑於 `ModelContextProtocol.AspNetCore`（1.4.1）底層未原生支援 Named Server，若直接呼叫多次 `app.MapMcp(path)` 會共用同一全域工具集合。

我們利用 `McpGateway.Core` 的 **Stateless 傳輸模式（`httpOptions.Stateless = true`）** 達成 100% 的協議層隔離：
1. **白名單登錄中心**：引進 `IMcpSubsystemRegistry`，於啟動期記錄各子系統代碼所屬的 Tool 類型集合。
2. **連線選項動態回呼**：在每次連入 HTTP 請求時，SDK 觸發 `ConfigureSessionOptions`。
3. **路徑正則解析與過濾**：
   - 自 `HttpContext.Request.Path` 解析 `{system}` 名稱。
   - 自 `IMcpSubsystemRegistry` 取得該系統合法之工具白名單。
   - 將當前請求的 `mcpOptions.ToolCollection` 動態替換為該系統專屬工具集合。
4. **安全防護效果**：
   - 當 Agent 請求 `tools/list` 時，**只能看見該子系統所屬的工具**。
   - 當 Agent 發送 `tools/call` 時，若嘗試調用其他系統的工具，SDK 直接回傳 `Tool not found`，徹底杜絕跨系統誤呼叫與 LLM 幻覺。

---

### 3. DI 註冊契約與 Core Fluent API

各子系統採用 ASP.NET Core 標準慣用擴充方法模式（Extension Method），保證零反射與編譯期型別安全。

#### 子系統端模組註冊 (`MesModuleExtensions.cs`)
```csharp
namespace McpGateway.Mfg.Mes;

public static class MesModuleExtensions
{
    public static IServiceCollection AddMesSubsystem(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 1. 綁定 MES 專屬組態
        services.Configure<MesOptions>(configuration.GetSection("McpGateway:Systems:mes"));

        // 2. 註冊 MES 業務服務與 Downstream API
        services.AddHttpClient<IMesService, MesService>();

        // 3. 透過 Core Fluent API 向 MCP 與白名單註冊工具
        services.AddMcpSubsystem("mes", subsystem =>
        {
            subsystem.WithTools<MesLotQueryTool>()
                     .WithTools<MesDefectReportTool>();
        });

        return services;
    }
}
```

#### 宿主組裝 (`Host/Program.cs` - Thin Host)
宿主僅作為 Composite Root，程式碼維持極簡：
```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. 註冊 McpGateway 核心基礎設施
builder.Services.AddMcpGateway();

// 2. 組裝各子系統模組 (由 add-module 腳本自動注入)
builder.Services.AddMesSubsystem(builder.Configuration);
builder.Services.AddWmsSubsystem(builder.Configuration);

var app = builder.Build();

// 3. 掛載 Ingress 端點與啟動
app.MapMcpGateway();
await app.RunMcpGatewayAsync();
```

---

### 4. 組態與 Consul KV 命名標準

為確保各子系統設定獨立，避免相互覆蓋，制定分層命名空間規範：

* **`appsettings.json`**：
  ```json
  {
    "McpGateway": {
      "Department": "mfg",
      "RoutePrefix": "/mfg",
      "Systems": {
        "mes": {
          "Downstream": {
            "BaseUrl": "http://mes-api.corp.local",
            "TimeoutSeconds": 15
          }
        },
        "wms": {
          "Downstream": {
            "BaseUrl": "http://wms-api.corp.local",
            "TimeoutSeconds": 30
          }
        }
      }
    }
  }
  ```
* **Consul KV 鍵值路徑**：
  `McpGateway/{department}/systems/{system}/Downstream/BaseUrl`

---

### 5. 工具命名規範與 Roslyn 靜態檢核

* **命名規範**：工具方法標註之 MCP 名稱必須符合三段式格式：
  $$\text{\{department\}\_\{system\}\_\{action\}}$$
  * 範例：`mfg_mes_query_lot`、`mfg_wms_query_stock`。
* **Roslyn 編譯期診斷 (`MCP003`)**：
  `McpGateway.Analyzers` 擴充診斷規則：若子系統模組內之 MCP Tool 未包含對應之 `<system>_` 前綴，或未符合命名樣式，將於編譯期發出診斷警告或錯誤，防範工具名稱於全域字典發生衝突。

---

### 6. 增量式腳手架工具鏈 (Scaffolding Workflow)

提供專屬增量腳手架指令 `templates/add-module.ps1`，簡化新子系統團隊的接入流程：

```powershell
pwsh .\add-module.ps1 -Department mfg -System mes -ToolName mes_query_lot
```

執行流程自動包含以下步驟：
1. 驗證部門宿主專案（`src/McpGateway.<Dept>.Host`）是否存在。
2. 建立子系統 Class Library 專案目錄與標準程式骨架（`Configuration/`, `Services/`, `Tools/`, `ModuleExtensions.cs`）。
3. 執行 `dotnet sln add` 將子系統專案納入部門 Solution。
4. 執行 `dotnet add reference` 讓 Host 專案自動引用子系統。
5. 在 `Host/Program.cs` 錨點自動注入 `builder.Services.Add<System>Subsystem(...)` 註冊碼。

---

## 影響與後果 (Consequences)

### 正面影響 (Positive)
1. **高度團隊自治與清晰權責**：各子系統代碼物理分離，PR 與 CODEOWNERS 審查獨立，解決 Git 衝突痛點。
2. **LLM 準確率最大化**：依系統分流端點，每個端點僅呈現該子系統的 2~5 支工具，Token 消耗大幅降低，杜絕工具幻覺。
3. **相容官方 SDK 且零反射**：利用 Stateless 請求級別回呼動態過濾，完全不需 Fork 官方 SDK，同時維持強型別編譯安全。
4. **標準化自動接入**：透過 `add-module.ps1` 確保所有子系統目錄與程式碼風格百分之百合規。

### 負面影響 / 代價與緩解 (Trade-offs & Mitigation)
1. **專案檔數量增加**：
   * *代價*：部門方案從單一專案變成 1 個 Host + N 個 Class Library，專案檔數量增加。
   * *緩解*：透過 `add-module.ps1` 全自動建立與綁定，開發者不需手動配置 `.sln` 或 ProjectReference。
2. **跨系統公用邏輯抽取**：
   * *代價*：若部門內多個子系統需要共用資料模型或 Helper，直接跨專案引用可能破壞邊界。
   * *緩解*：若有部門內部共用需求，建議建立 `McpGateway.<Dept>.Shared` 專案專門存放共用 DTO；平台級能力則統一透過 `McpGateway.Core` 提供。
