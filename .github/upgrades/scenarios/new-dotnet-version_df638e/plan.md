# .NET 10 Upgrade Plan

## 1. Objective and Scope

Upgrade `copilot-secure-mcp-azure-containers/solution/i365.Mcp.EchoMCPServer.csproj` from `net9.0` to `net10.0` and ensure the application builds and runs with updated package and API compatibility.

## 2. Planned Changes

### 2.1 Project target framework and container settings

Update `copilot-secure-mcp-azure-containers/solution/i365.Mcp.EchoMCPServer.csproj`:
- Change `TargetFramework` from `net9.0` to `net10.0`
- Update `ContainerBaseImage` from `mcr.microsoft.com/dotnet/sdk:9.0` to `mcr.microsoft.com/dotnet/sdk:10.0`

### 2.2 NuGet package updates

In `i365.Mcp.EchoMCPServer.csproj`, update packages per assessment recommendations:
- `Microsoft.AspNetCore.Authentication.JwtBearer` to `10.0.8`
- `Microsoft.Extensions.Hosting` to `10.0.8`

Keep other package versions unchanged unless build validation requires additional updates.

### 2.3 API compatibility remediation

Review and update code in:
- `copilot-secure-mcp-azure-containers/solution/Program.cs`
- `copilot-secure-mcp-azure-containers/solution/EchoTool.cs`

Focus on areas identified in assessment:
- Authentication/JWT event handler API compatibility
- Configuration binder usage (`GetValue<T>`) compatibility
- Logging API behavioral changes

Apply minimal code changes required for successful compile/runtime behavior on .NET 10.

## 3. Validation Plan

### 3.1 Restore and build

- Run restore for the solution
- Build solution in Debug configuration
- Success criteria: `0 errors` (warnings acceptable unless they indicate runtime risk)

### 3.2 Smoke checks

- Launch the app locally and verify startup succeeds
- Verify key configuration values load correctly
- Verify authentication middleware registration still initializes without exception

## 4. Execution Order

1. Update project file (`TargetFramework`, container image, packages)
2. Restore and build
3. Apply API compatibility fixes in code
4. Rebuild and run smoke checks
5. Final validation and commit

## 5. Risk and Mitigation

- Risk: Preview/rapid package drift for external packages (`ModelContextProtocol`)
  - Mitigation: Keep package pinned unless required for compatibility
- Risk: JWT event API signature changes
  - Mitigation: Compile-fix in `Program.cs` and validate startup path
- Risk: Configuration binding changes
  - Mitigation: Validate typed config retrieval paths during smoke test

## 6. Deliverables

- Updated `i365.Mcp.EchoMCPServer.csproj` targeting `net10.0`
- Any required code updates in `Program.cs` and/or `EchoTool.cs`
- Successful solution build on upgrade branch
