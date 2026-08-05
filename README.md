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
