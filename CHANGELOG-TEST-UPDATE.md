# Test Strategy Update - MCP Tool Registration

## Summary
Refactored test strategy following MCP tool registration fixes:

### Changes Made
1. **Added System.Net.Http.Json reference** to test projects
2. **Transitioned from ToolBase to MCP Server pattern**
3. **Deleted obsolete test files**
4. **Created new MCP-focused tests**

### New Test Structure
- Integration Tests: 4 test methods covering tool discovery and execution
- E2E Tests: 2 test methods covering MCP server capabilities
- All tests marked for manual execution via [Skip] attribute

### Test Execution
```bash
cd src/McpGateway.Report
dotnet run --urls "http://localhost:5100"

# In separate terminal
dotnet test tests/McpGateway.Report.IntegrationTests/
dotnet test tests/McpGateway.Report.E2ETests/
```

## Test Coverage
- ✅ Tool registration (tools/list endpoint)
- ✅ Tool execution (tools/call endpoint)
- ✅ Parameter handling (workCenter, productLine, startDate, endDate)
- ✅ Default behavior when parameters are null
- ✅ Response format validation

## Files Modified
- Test projects nf9 configuration
- McpToolRegistrationTests.cs (NEW)
- McpServerE2ETests.cs (NEW)
- Removed: QueryWipToolTests.cs, SemanticKernelE2ETests.cs, PydanticAIE2ETests.cs

## Notes
- Tests require manual Gateway startup (not automated in CI)
- All tests marked [Skip] with MANUAL RUN message
- SSE response parsing helper implemented for all test classes