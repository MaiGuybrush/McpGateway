# Domain Context & Glossary

This document serves as the canonical glossary for the McpGateway project, defining domain terms and ubiquitous language.

## Core Concepts

### **Tool Facade**
An adapter layer between AI Agents and internal enterprise APIs, using the MCP protocol to expose LLM-friendly tools with semantic encapsulation, field projection, parameter convergence, and authentication proxying.

### **Core Package (`McpGateway.Core`)**
The shared NuGet package used by all department gateways, handling cross-cutting concerns like MCP hosting, authentication proxying, health checks, audit logging with PII redaction, and startup validation.

### **Department Gateway (`McpGateway.{Dept}`)**
A department-specific MCP service (e.g., `McpGateway.Report`, `McpGateway.Spc`) running as an independent container with its own tools and configuration.

### **MCP (Model Context Protocol)**
An open standard protocol managed under the Agentic AI Foundation for communication between AI Agents and tools over transports like Streamable HTTP.

### **Auth Provider**
The authentication mechanism supported by MCP Gateway for verifying caller identity and managing downstream authentication:
- **`JWT`**: Bearer token authentication validated via JWKS endpoints.
- **`API-KEY`**: Service-to-service authentication validated against a central API-KEY service.
- **`NTLM`**: Windows Integrated Authentication using system accounts injected via environment variables (`NTLM_SERVICE_ACCOUNT` / `NTLM_SERVICE_PASSWORD`).
- **`None` / `Disabled`**: Disabled authentication (`"Enabled": false`) for local development or testing.

## Design Patterns

### **Semantic Encapsulation**
The process of transforming technical API contracts into LLM-friendly tool contracts with clear natural language descriptions, single-intent focus, and clean parameter naming.

### **Field Projection**
Filtering and transforming downstream API output at the gateway layer to return only necessary fields to the AI Agent, reducing token consumption.
