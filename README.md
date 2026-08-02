# McpGateway

[![Status](https://img.shields.io/badge/status-Design%20Phase-orange)](https://github.com/MaiGuybrush/McpGateway)
[![Spec](https://img.shields.io/badge/Spec-Core%20Spec-blue)](./docs/specs/mcp-gateway-core-spec.md)
[![Plan](https://img.shields.io/badge/Plan-Development%20Plan-blue)](./docs/specs/development-plan.md)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

MCP (Model Context Protocol) Gateway — a .NET implementation that transforms REST APIs into MCP-compatible tools for LLM integration.

**Current phase**: design complete, implementation not yet started.

> ## ⚠️ Status Correction (2026-08-02)
>
> This README previously reported "PoC Completed" with accuracy and latency figures.
> A code audit found those figures were **never measured**:
>
> - **The PoC code does not compile.** `McpServerHost.cs:68` and `ToolRegistry.cs:49` declare two
>   different `ITool` interfaces in the same namespace (CS0101), plus two further type errors.
> - **All numbers are estimates.** `PoC-REPORT.md:375` states this directly:
>   *"數據為預估值，實際值以正式開發測量為準"* (figures are estimates; actual values to be
>   measured during formal development).
> - **`ModelContextProtocol.AspNetCore`** — the package required for Streamable HTTP — **was never referenced.**
>
> The **ADRs remain the real asset**: seven architecture decisions, grilled and documented.
> The code does not. See [development plan §0](./docs/specs/development-plan.md) for the full audit.

## 🎯 Mission

Transform complex REST APIs into LLM-friendly tools through semantic wrapping, so agents call the
right tool with the right arguments.

Two complementary mechanisms:

| Mechanism | Problem solved | Decision |
|-----------|----------------|----------|
| **Semantic wrapping** | Each individual tool is hard for an LLM to understand | [ADR-003](./docs/architecture/adr/ADR-003-config-driven-descriptions.md) |
| **Per-department split** | The *set* of tools an agent loads is too large | [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) |

## 📊 Projected Figures (⚠️ estimates, not measurements)

> Every number below is a **planning estimate** from `PoC-REPORT.md`.
> None were produced by running code. First real measurements come in Sprint 0 / Sprint 4.

| Metric | Mechanical | Manual | Basis |
|--------|------------|--------|-------|
| Accuracy | 55–65% | 90–95% | ⚠️ Estimated |
| Latency p95 | 42–48ms | 45–52ms | ⚠️ Estimated, single-service, **excludes ingress hop** |
| Dev time | <1hr/tool | 3.5hr/tool | ⚠️ Estimated |
| LLM understanding | Low | High | Qualitative |

⚠️ The estimated Manual OrderCreate p95 (**52ms**) already exceeds the stated <50ms threshold,
and the [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) topology adds an
ingress hop. The latency budget needs to be re-derived from real data — see
[ADR-001](./docs/architecture/adr/ADR-001-use-mcp-protocol.md).

**Direction**: proceed with manual semantic wrapping — but **validate the core assumptions first**
(Sprint 0 gate) rather than treating the estimates as findings.

## 🏗️ Architecture

`McpGateway.Core` (shared NuGet package) + N independently deployed department gateways,
fronted by a single hostname via ingress path routing.

```
 Agent (/report)   Agent (/spc)   Agent (/report,/qc)
        └───────────────┼───────────────┘
                        │ MCP (Streamable HTTP)
                        ▼
             Ingress — mcp.corp.local
        ┌───────────────┼───────────────┐
   /report            /spc            /qc
        ▼               ▼               ▼
  McpGateway.Report  .Spc            .Qc      ← tools only, 3-line Program.cs
   └─ .Core          └─ .Core        └─ .Core ← auth / audit / transport / validation
        └───────────────┼───────────────┘
                        ▼
                 Ocelot Gateway
              ┌─────────┴─────────┐
        Java Spring Boot      C#/.NET services
```

**Why split by department**: an agent only loads the tools it needs (LLM accuracy), and each
department ships on its own schedule without dropping other departments' connections.

**Why a shared package**: authentication, audit logging, PII redaction, transport and startup
validation are implemented **once** — a department project cannot forget to wire them up.

### Key decisions

| ADR | Decision | Status |
|-----|----------|--------|
| [ADR-001](./docs/architecture/adr/ADR-001-use-mcp-protocol.md) | MCP protocol, Streamable HTTP | ⚠️ Proposed — perf/SDK assumptions unverified |
| [ADR-002](./docs/architecture/adr/ADR-002-dotnet-mcp-sdk-choice.md) | .NET MCP SDK | ✅ Approved |
| [ADR-003](./docs/architecture/adr/ADR-003-config-driven-descriptions.md) | Code-inline descriptions (not config-driven) | ✅ Approved |
| [ADR-004](./docs/architecture/adr/ADR-004-startup-validation.md) | Simplified startup validation + department prefix check | ✅ Approved |
| [ADR-005](./docs/architecture/adr/ADR-005-tool-versioning.md) | Tool-level versioning, deferred to MVP+1 | ✅ Approved |
| [ADR-006](./docs/architecture/adr/ADR-006-security-model.md) | Authentication proxy (delegating pattern) | ⚠️ Approved — needs delta re-confirmation |
| [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) | Per-department gateway split | ⏳ Proposed |

ADR-007 (test strategy) and ADR-008 (caching) are reserved but not yet written.

## 🛠️ Implementation

### What a department project looks like

The whole `Program.cs` — everything else lives in `McpGateway.Core`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMcpGateway();                    // auth + audit + transport + validation
builder.AddToolsFromAssembly<Program>();    // scan this assembly for tools
await builder.Build().RunMcpGatewayAsync();
```

A tool — semantic wrapping is the point, so descriptions carry real weight:

```csharp
[McpTool("report_get_status")]              // department prefix enforced at startup
public sealed class GetReportStatusTool : ToolBase<GetReportStatusInput, ReportStatus>
{
    public override async Task<ReportStatus> ExecuteAsync(
        GetReportStatusInput input, CancellationToken ct)
    {
        var raw = await Downstream.GetAsync<ReportJobDto>(
            $"/api/report-jobs/{input.JobId}", ct: ct);

        // Field projection: return only what the agent needs
        return new ReportStatus(
            Status: raw.StatusCode switch { 0 => "queued", 1 => "running", 2 => "done", _ => "failed" },
            EstimatedCompletion: raw.EtaUtc,
            DownloadUrl: raw.StatusCode == 2 ? raw.BlobUrl : null);
    }
}

[Description("""
    Check whether a report has finished generating. Use when a user asks
    "is my report ready yet".

    Returns: status (queued/running/done/failed), estimated completion time,
    and a download URL (only when status is done).
    """)]
public sealed record GetReportStatusInput(
    [property: Description("Report job ID, format RPT-2026-000123")]
    string JobId);
```

Department developers never touch MCP protocol, authentication, token caching, audit logging,
or transport. Target onboarding cost: **≤ 1 person-day** from nothing to a deployed gateway.

### Target project structure

```
src/
├── McpGateway.Core/           # Shared NuGet package (platform team)
│   ├── Hosting/               #   AddMcpGateway / RunMcpGatewayAsync
│   ├── Tools/                 #   ITool / ToolBase / McpToolAttribute
│   ├── Auth/                  #   authentication proxy (ADR-006)
│   ├── Downstream/            #   Ocelot HttpClient
│   ├── Validation/            #   startup validation (ADR-004)
│   ├── Audit/                 #   audit log + PII redaction
│   └── Observability/         #   metrics / health checks
├── McpGateway.Report/         # First department — tools only
└── MockOcelotApi/             # Mock API for testing
tests/
├── McpGateway.Core.Tests/            # unit + contract tests
├── McpGateway.Core.IntegrationTests/ # WireMock-backed
└── k6/                               # performance tests
```

⚠️ **Current repo state differs.** `src/McpGateway/` holds the non-compiling PoC scaffolding.
See [development plan §0](./docs/specs/development-plan.md) for what is kept vs discarded —
the hand-written descriptions in `Tools/Manual/` are worth keeping as ADR-003 examples;
`Infrastructure/` is not.

## 📝 Documents

### Build from these
- [**Core spec**](./docs/specs/mcp-gateway-core-spec.md) — public API, config schema, auth pipeline, audit, department contract
- [**Development plan**](./docs/specs/development-plan.md) — sprints, gate conditions, blockers, code-audit findings
- [**Design document**](./docs/tool-facade-design-doc.md) (v1.1) — requirements and architecture
- [**Glossary**](./docs/architecture/glossary.md) — terms

### Background
- [**GRILLING-SUMMARY.md**](./docs/architecture/GRILLING-SUMMARY.md) — architecture review that produced the ADRs
- [**PoC-PLAN.md**](./PoC-PLAN.md) / [**PoC-REPORT.md**](./PoC-REPORT.md) — ⚠️ figures are estimates
- [**DELIVERABLES.md**](./DELIVERABLES.md) — inventory (needs correction against actual state)

### Test assets (kept, not yet exercised)
- **20 LLM prompts** for comprehension testing ([TestPrompts.md](./tests/McpGateway.Tests/TestPrompts.md)) — these become the Sprint 4 accuracy measurement
- **k6 load tests** ([k6/load-test.js](./tests/k6/load-test.js)) — become the Sprint 0/4 latency measurement

## 🚀 Getting Started

### Prerequisites
- **.NET 8.0 SDK** — ⚠️ this machine currently has only 3.1.402; install .NET 8 first
- Node.js (for k6 tests)
- Visual Studio 2022 or VS Code

### Current state

⚠️ **`src/McpGateway/` does not build.** Implementation starts with Sprint 0 (SDK spike).
The mock API and k6 scripts do work and are kept:

```bash
cd src/MockOcelotApi && dotnet run     # mock downstream API
k6 run tests/k6/load-test.js           # performance harness (needs a running gateway)
```

Start here instead:

1. [Development plan](./docs/specs/development-plan.md) — Sprint 0 gate, blockers, estimates
2. [Core spec](./docs/specs/mcp-gateway-core-spec.md) — the contract to build against
3. [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) — why the topology looks like this

## 📊 Performance

### Latency — ⚠️ estimates, never measured

| Tool Type | p50 | p95 | p99 |
|-----------|-----|-----|-----|
| Mechanical UserQuery | 25ms | 42ms | 48ms |
| Manual UserQuery | 28ms | 45ms | 52ms |
| Mechanical OrderCreate | 35ms | 48ms | 55ms |
| Manual OrderCreate | 38ms | **52ms** | 58ms |

⚠️ This README previously claimed *"All results meet <50ms p95 threshold ✅"*. That is wrong on
two counts: Manual OrderCreate's p95 (52ms) exceeds 50ms even in the estimate, and no measurement
was ever taken. The [ADR-009](./docs/architecture/adr/ADR-009-department-gateway-split.md) ingress
hop is also not reflected here.

Resource figures (Memory +3MB, CPU +1%) are estimates from the same source.

**First real numbers**: Sprint 0 (single service) and Sprint 4 (end-to-end with ingress).

## 🔒 Security

Authentication proxy pattern — the gateway validates the caller's JWT / API-KEY, extracts identity,
and forwards identity headers downstream. It never stores or forwards the caller's token.
See [ADR-006](./docs/architecture/adr/ADR-006-security-model.md).

| Area | State |
|------|-------|
| Auth design (JWT / API-KEY / NTLM + Redis token cache) | ✅ Approved, ⚠️ needs delta re-confirmation for the multi-service topology |
| Audit logging + PII redaction | 📋 Specified, not built |
| Per-department NTLM service accounts | ⏳ Awaiting security team decision |
| Core package CVE propagation (N services, one dependency) | 📋 Mitigated by floating version + CI gate + version telemetry |

⚠️ Nothing in this section is implemented yet.

## 📚 Architecture Decision Records

See the decision table under [Architecture](#️-architecture) above.
Full records live in [`docs/architecture/adr/`](./docs/architecture/adr/).

## 🗺️ Roadmap

| Sprint | Content | Core days | Dept days |
|--------|---------|-----------|-----------|
| **0** 🔴 | Environment + **SDK spike** — *gate: may invalidate ADR-001/002* | 5 | — |
| 1 | Core skeleton + JWT auth | 8 | — |
| 2 | API-KEY + downstream client + audit | 7.5 | — |
| 3 | NTLM + observability + contract tests | 6.5 | — |
| 4 | First department + deployment + **first real measurements** | 4 | 5 |
| 5 | Second department (triggered, not scheduled) | — | ≤1 |

**Total**: ~31 Core person-days + ~5 department person-days ≈ 6 weeks for one full-time engineer.
Estimate confidence: **medium-low** until the Sprint 0 gate passes.

### Blocked on external parties — needed now

| Item | Owner |
|------|-------|
| Internal NuGet feed | DevOps |
| .NET 8 SDK dev environment | — |
| ADR-009 approval | Architecture team |

Redis capacity, JWKS/API-KEY service SLAs, ingress path-routing capability, and the NTLM account
decision are needed before Sprints 1–4 respectively. See
[development plan §8](./docs/specs/development-plan.md).

## 🤝 Team

For formal development:
- Tech Lead: 1 FTE
- Developers: 1–2 FTEs
- ⚠️ `McpGateway.Core` needs **≥ 2** maintainers — N departments depend on it (bus factor)

## 📄 License

MIT License — see LICENSE file for details

## 🔗 Links

- [MCP Official Specification](https://modelcontextprotocol.io/)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Design document](./docs/tool-facade-design-doc.md) (v1.1)
- [Architecture review (grilling)](./docs/architecture/GRILLING-SUMMARY.md)
- [PoC report](./PoC-REPORT.md) — ⚠️ figures are estimates, see status correction above

---

## 💡 Key Takeaway

**The architecture decisions are solid. The evidence for them is not yet collected.**

Seven ADRs cover protocol choice, description strategy, startup validation, versioning, security,
and department split — each grilled and documented. That work stands.

What does not stand is the claim that a PoC validated the performance and accuracy assumptions.
It did not run. Sprint 0 exists to close that gap **before** committing 31 person-days —
which is exactly what the [architecture review](./docs/architecture/GRILLING-SUMMARY.md) asked for
the first time round.

**Next step**: run the Sprint 0 gate.