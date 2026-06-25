# Skill: iThink 365 MCP Server

## Description

Build a production-ready .NET MCP (Model Context Protocol) server in the **iThink 365 way**:

- Secured by **Microsoft Entra ID** (JWT Bearer, scope `mcp.tools`)
- **Stateless** HTTP transport (no server-side session state)
- **OpenTelemetry** via Azure Monitor
- Health checks (`/health`, `/ready`)
- OAuth 2.0 discovery endpoints (`.well-known/oauth-protected-resource`, `.well-known/oauth-authorization-server`)
- CORS permissive for MCP client compatibility
- Containerised with .NET SDK container publish targeting Alpine Linux, multi-arch (`linux-x64`, `linux-arm64`)
- Deployed as a Linux container on Azure App Service (hosted in ACR)
- Built and released via Azure DevOps YAML pipelines

---

## When to Use This Skill

Use this skill when asked to:
- Create a new MCP server for iThink 365
- Add a new integration tool to an existing iThink 365 MCP server
- Review or fix authentication/authorisation on an iThink 365 MCP server
- Set up CI/CD pipelines or Azure infrastructure for an iThink 365 MCP server

---

## Canonical Architecture

```
{ServerName}-mcp-server/
  {SolutionName}.slnx                    ← solution file
  {ProjectName}/
    {ProjectName}.csproj                 ← .NET 10, container support
    Program.cs                           ← all wiring: auth, OTel, MCP, health
    appsettings.json
    appsettings.Development.json
    Configuration/
      AzureAdOptions.cs                  ← Entra ID config model
      {Service}Options.cs                ← service-specific config model
    Tools/
      {Domain}Tools.cs                   ← [McpServerToolType] classes
    Services/
      I{Service}ApiClient.cs             ← interface
      {Service}ApiClient.cs              ← typed HttpClient implementation
    Health/
      {Service}ReadinessHealthCheck.cs   ← readiness health check
  {ProjectName}.Tests/
    {ProjectName}.Tests.csproj
```

---

## Step-by-Step Build Guide

### 1. Create Solution and Project

```bash
dotnet new sln -n {SolutionName}
dotnet new web -n {ProjectName} --framework net10.0
dotnet sln {SolutionName}.slnx add {ProjectName}/{ProjectName}.csproj
dotnet new xunit -n {ProjectName}.Tests --framework net10.0
dotnet sln {SolutionName}.slnx add {ProjectName}.Tests/{ProjectName}.Tests.csproj
```

### 2. Configure `.csproj`

Use **both** `Microsoft.NET.Sdk` and `Microsoft.NET.Sdk.Publish` to enable `dotnet publish /t:PublishContainer`.

```xml
<Project Sdk="Microsoft.NET.Sdk;Microsoft.NET.Sdk.Publish">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <PropertyGroup Label="Package">
    <Product>{ProjectName}</Product>
    <Description>MCP Server for {Description}</Description>
    <PackageTags>mcp;dotnet</PackageTags>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <PropertyGroup>
    <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
    <ContainerRepository>ithink365/{lowercasemcpname}mcp</ContainerRepository>
    <ContainerRegistry>i365mcpdevsd-g9e8eqe4htdch9cw.azurecr.io</ContainerRegistry>
    <ContainerFamily>alpine</ContainerFamily>
    <ContainerRuntimeIdentifiers>linux-x64;linux-arm64</ContainerRuntimeIdentifiers>
    <ContainerBaseImage>mcr.microsoft.com/dotnet/sdk:10.0</ContainerBaseImage>
    <UserSecretsId>{new-guid}</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ContainerEnvironmentVariable Include="ASPNETCORE_URLS" Value="http://*:8080;http://*:5000" />
    <ContainerEnvironmentVariable Include="ASPNETCORE_HTTP_PORTS " Value="8080;5000" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.5.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.24.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.8" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.8" />
    <PackageReference Include="Microsoft.Identity.Web" Version="4.9.0" />
    <PackageReference Include="ModelContextProtocol" Version="1.3.0" />
    <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.3.0" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="ModelContextProtocol" />
    <Using Include="ModelContextProtocol.Server" />
    <Using Include="System.ComponentModel" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="appsettings.Development.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

> **Package versions**: Always use the latest stable versions. The versions above are the current baseline. Update `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.Extensions.Hosting` to match the `net10.0` SDK version in use.

---

### 3. Configuration Models

#### `Configuration/AzureAdOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace {Namespace}.Configuration;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    public string? Audience { get; init; }

    public bool DisableSecurity { get; init; } = false;
}
```

#### `Configuration/{Service}Options.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace {Namespace}.Configuration;

public sealed class {Service}Options
{
    public const string SectionName = "{Section}";

    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    // Add service-specific settings here
}
```

---

### 4. Health Checks

#### `Health/{Service}ReadinessHealthCheck.cs`

```csharp
using {Namespace}.Configuration;
using {Namespace}.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace {Namespace}.Health;

public sealed class {Service}ReadinessHealthCheck(
    I{Service}ApiClient apiClient,
    IOptions<{Service}Options> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
            return HealthCheckResult.Unhealthy("{Service} configuration is missing.");

        try
        {
            var healthy = await apiClient.PingAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("{Service} API is reachable.")
                : HealthCheckResult.Unhealthy("{Service} API is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("{Service} readiness check failed.", ex);
        }
    }
}
```

---

### 5. `Program.cs` — Full Canonical Template

This is the authoritative wiring pattern. Do not deviate from the ordering of middleware (`UseAuthentication` → `UseAuthorization` after `UseCors`).

```csharp
using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using {Namespace}.Configuration;
using {Namespace}.Health;
using {Namespace}.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

const string mcpAccessPolicy = "mcp.tools";

// ── Resolve server URL ────────────────────────────────────────────────────────
var serverUrl = "https://localhost:7146";
var hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
if (!string.IsNullOrEmpty(hostName))
    serverUrl = $"https://{hostName}";

var httpMcpServerUrl = builder.Configuration["HttpMcpServerUrl"]
    ?? $"{serverUrl}/api/mcp";
var resourceDocumentationUrl = builder.Configuration["Mcp:ResourceDocumentationUrl"]
    ?? $"{serverUrl}/health";

// ── Configuration binding ─────────────────────────────────────────────────────
builder.Services
    .AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection(AzureAdOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(static o => !string.IsNullOrWhiteSpace(o.TenantId), "Azure AD TenantId is required.")
    .Validate(static o => !string.IsNullOrWhiteSpace(o.ClientId), "Azure AD ClientId is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<{Service}Options>()
    .Bind(builder.Configuration.GetSection({Service}Options.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── OpenTelemetry + Azure Monitor ─────────────────────────────────────────────
var azureMonitorConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];
if (!string.IsNullOrEmpty(azureMonitorConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    {
        options.ConnectionString = azureMonitorConnectionString;
    });
}

// ── Azure AD / Entra ID values ────────────────────────────────────────────────
var azureAdOptions = builder.Configuration.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>() ?? new();
var requiredScope = builder.Configuration["AzureAd:RequiredScope"] ?? "mcp.tools";
var disableSecurity = builder.Configuration.GetValue<bool>("AzureAd:DisableSecurity");
var authorizationServerUrl = $"https://login.microsoftonline.com/{azureAdOptions.TenantId}/v2.0";
var oauthAuthorizationEndpoint = authorizationServerUrl.Replace(
    "/v2.0", "/oauth2/v2.0", StringComparison.OrdinalIgnoreCase);
var requiredScopeWithResource = $"{httpMcpServerUrl.TrimEnd('/')}/{requiredScope.TrimStart('/')}";

// ── JWT Bearer authentication ─────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{azureAdOptions.TenantId}/v2.0";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = string.IsNullOrWhiteSpace(azureAdOptions.Audience)
                ? azureAdOptions.ClientId
                : azureAdOptions.Audience
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication.JwtBearer");

                logger.LogInformation(
                    "JWT message received for {Method} {Path}. Authorization header present: {HasAuthorizationHeader}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.Headers.ContainsKey("Authorization"));

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication.JwtBearer");

                var claims = context.Principal?.Claims ?? [];
                var subject = claims.FirstOrDefault(c =>
                    c.Type is "sub" or
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                var audience = claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                var scope = claims.FirstOrDefault(c =>
                    c.Type is "scp" or
                    "http://schemas.microsoft.com/identity/claims/scope")?.Value;
                var roles = claims.Where(c => c.Type == "roles").Select(c => c.Value).ToArray();

                logger.LogInformation(
                    "JWT token validated. Subject: {Subject}, Audience: {Audience}, Scope: {Scope}, Roles: {Roles}",
                    subject, audience, scope,
                    roles.Length == 0 ? "<none>" : string.Join(",", roles));

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication.JwtBearer");

                logger.LogWarning(context.Exception,
                    "JWT authentication failed for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication.JwtBearer");

                logger.LogWarning(
                    "JWT challenge triggered for {Method} {Path}. Error: {Error}; Description: {Description}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Error,
                    context.ErrorDescription);

                return Task.CompletedTask;
            },
            OnForbidden = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication.JwtBearer");

                var scope = context.HttpContext.User.FindFirst("scp")?.Value
                    ?? context.HttpContext.User.FindFirst(
                        "http://schemas.microsoft.com/identity/claims/scope")?.Value;

                logger.LogWarning(
                    "JWT token authenticated but authorization failed for {Method} {Path}. Scope claim: {Scope}",
                    context.Request.Method,
                    context.Request.Path,
                    scope ?? "<none>");

                if (context.Request.Path.StartsWithSegments("/api/mcp"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "forbidden",
                        error_description = "Token is valid but does not contain required scope for /api/mcp.",
                        required_scope = requiredScopeWithResource,
                        token_scope = scope ?? "<none>"
                    });
                }
            }
        };
    });

// ── Authorization policy ──────────────────────────────────────────────────────
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(mcpAccessPolicy, policy =>
        policy.RequireClaim(
            "http://schemas.microsoft.com/identity/claims/scope",
            requiredScope));

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Register typed HTTP client for the external API
builder.Services.AddHttpClient<I{Service}ApiClient, {Service}ApiClient>((sp, httpClient) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<{Service}Options>>().Value;
    httpClient.BaseAddress = new Uri(opts.BaseUrl);
    httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
});

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddCheck<{Service}ReadinessHealthCheck>("{service}-ready", tags: ["ready"]);

// ── MCP Server — stateless HTTP transport ─────────────────────────────────────
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => { options.Stateless = true; })
    .WithToolsFromAssembly(Assembly.GetExecutingAssembly());

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ── Exception handler ─────────────────────────────────────────────────────────
builder.Services.AddExceptionHandler(options =>
{
    options.ExceptionHandlingPath = "/error";
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// ── Diagnostic / info endpoints ───────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new
{
    service = "{ProjectName} ({DateTimeOffset.UtcNow:yyyy-MM-dd})",
    status = "running"
}));

app.MapGet("/error", () => Results.Problem(title: "An unexpected error occurred."));

// ── Health endpoints ──────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

// ── MCP endpoint ──────────────────────────────────────────────────────────────
var mcpEndpoint = app.MapMcp("/api/mcp");
if (!disableSecurity)
    mcpEndpoint.RequireAuthorization(mcpAccessPolicy);

// ── OAuth 2.0 discovery endpoints ────────────────────────────────────────────
app.MapGet(".well-known/oauth-protected-resource", () =>
    Results.Json(new
    {
        resource = httpMcpServerUrl,
        authorization_servers = new[] { authorizationServerUrl },
        scopes_supported = new[] { requiredScope, "openid", "profile", "offline_access", "User.Read" },
        code_challenge_methods_supported = new[] { "S256" },
        resource_name = "iThink 365 {ServerDisplayName} MCP Server",
        resource_documentation = resourceDocumentationUrl
    }));

app.MapGet(".well-known/oauth-authorization-server", () =>
    Results.Json(new
    {
        issuer = authorizationServerUrl,
        authorization_endpoint = $"{oauthAuthorizationEndpoint}/authorize",
        token_endpoint = $"{oauthAuthorizationEndpoint}/token",
        jwks_uri = $"{authorizationServerUrl}/discovery/v2.0/keys",
        response_types_supported = new[] { "code" },
        scopes_supported = new[] { requiredScopeWithResource, "openid", "profile", "offline_access", "User.Read" },
        code_challenge_methods_supported = new[] { "S256" }
    }));

app.Run();

public partial class Program;
```

> **Middleware ordering is critical**: `UseAuthentication()` and `UseAuthorization()` must come **after** `UseCors()`. Health and discovery endpoints must be mapped **before** the MCP endpoint so they remain unauthenticated.

---

### 6. Tool Classes

Tools are discovered automatically via `WithToolsFromAssembly()`. Use constructor injection for services.

```csharp
using System.ComponentModel;
using System.Text.Json;
using {Namespace}.Services;
using ModelContextProtocol.Server;

namespace {Namespace}.Tools;

[McpServerToolType]
public class {Domain}Tools
{
    private readonly I{Service}ApiClient _client;
    private readonly ILogger<{Domain}Tools> _logger;

    public {Domain}Tools(I{Service}ApiClient client, ILogger<{Domain}Tools> logger)
    {
        _client = client;
        _logger = logger;
    }

    [McpServerTool, Description("Brief, clear description of what this tool does.")]
    public async Task<string> {ToolName}(
        [Description("Description of this parameter")] string parameter1,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing {ToolName} with {Parameter}", nameof({ToolName}), parameter1);
        var result = await _client.{MethodName}(parameter1, cancellationToken);
        return JsonSerializer.Serialize(result);
    }
}
```

**Rules for tools:**
- Every tool method must have `CancellationToken cancellationToken = default` as the last parameter.
- Return `string` (JSON-serialised). Use `System.Text.Json.JsonSerializer.Serialize`.
- `[Description]` attributes are mandatory on the class, every method, and every parameter — they become the MCP tool manifest.
- Use structured logging with `_logger` — never `Console.Write`.

---

### 7. `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "{Section}": {
    "BaseUrl": "https://api.example.com"
  },
  "AzureMonitor": {
    "ConnectionString": ""
  },
  "HttpMcpServerUrl": "https://localhost:7146/api/mcp",
  "Mcp": {
    "ResourceDocumentationUrl": "https://localhost:7146/health"
  },
  "AzureAd": {
    "TenantId": "f0272860-a49b-4746-9d1f-7072e4113e52",
    "ClientId": "{client-id-guid}",
    "Audience": "{client-id-guid}",
    "RequiredScope": "mcp.tools",
    "DisableSecurity": false
  }
}
```

**Important**: The `TenantId` is always `f0272860-a49b-4746-9d1f-7072e4113e52` (iThink 365 tenant). The `ClientId` and `Audience` are the App Registration for this specific MCP server.

#### `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Authentication": "Debug"
    }
  },
  "AzureAd": {
    "DisableSecurity": false
  }
}
```

---

### 8. Azure DevOps — Build Pipeline

Create `dev-ops/i365-{mcpname}-mcp-build-pipeline.yml`:

```yaml
# iThink 365 {ServerDisplayName} Mcp Container Build Pipeline
trigger:
  branches:
    include:
    - main
    - feature*
    - releases/*
  paths:
    include:
    - {server-folder}/**

name: "$(BuildDefinitionName)_$(SourceBranchName)_$(Date:yyyyMMdd)$(Rev:.r)"
variables:
  - name: buildConfiguration
    value: 'Release'
  - name: apiProjectPath
    value: '{ProjectName}.csproj'
  - name: apiProjectRoot
    value: '{server-folder}\{ProjectName}'
  - name: containerImageName
    value: 'container-mcp-image'

stages:
  - stage: buildCodeAndPublish
    jobs:
    - job: buildCodeAndPublish
      pool:
        vmImage: 'windows-latest'
      steps:
        - task: NuGetAuthenticate@1
          displayName: 'NuGet Authenticate'
          inputs:
            allowInternalNuGetServer: true

        - task: UseDotNet@2
          displayName: 'Install .NET 10.x SDK'
          inputs:
            version: 10.x
            performMultiLevelLookup: true

        - script: |
            dotnet restore
            dotnet build $(apiProjectPath) --configuration $(buildConfiguration)
            dotnet publish $(apiProjectPath) --configuration $(buildConfiguration) /t:PublishContainer --os linux --arch x64 -p ContainerArchiveOutputPath=$(Build.ArtifactStagingDirectory)/image/$(containerImageName).tar.gz
          displayName: 'Build and publish container image'
          workingDirectory: '$(Build.SourcesDirectory)/$(apiProjectRoot)'

        - task: CopyFiles@2
          displayName: 'Copy project outputs'
          inputs:
            SourceFolder: '$(Build.SourcesDirectory)/$(apiProjectRoot)/bin/$(buildConfiguration)'
            Contents: '**'
            TargetFolder: '$(Build.ArtifactStagingDirectory)/binaries'
            OverWrite: true

        - task: CopyFiles@2
          displayName: 'Copy project files'
          inputs:
            SourceFolder: '$(apiProjectRoot)'
            Contents: '**'
            TargetFolder: '$(Build.ArtifactStagingDirectory)/project'
            OverWrite: true

        - task: DotNetCoreCLI@2
          displayName: 'Execute unit tests'
          inputs:
            command: 'test'
            projects: '**/*Tests*/*.csproj'
            arguments: '--configuration $(buildConfiguration)'

        - task: PublishBuildArtifacts@1
          displayName: 'Publish artifact: drop'
          inputs:
            PathtoPublish: '$(Build.ArtifactStagingDirectory)'
            ArtifactName: 'drop'
```

### 9. Azure DevOps — Release Template

Create `dev-ops/templates/i365-{mcpname}-mcp-release-template-step.yml`:

```yaml
parameters:
- name: azureSubscriptionName
  default: ''
  type: string
- name: mcpProjectPath
  default: ''
  type: string

steps:
- task: UseDotNet@2
  displayName: 'Install .NET 10.x SDK'
  inputs:
    version: 10.x
    performMultiLevelLookup: true

- script: |
    dotnet publish ${{ parameters.mcpProjectPath }} --configuration $(buildConfiguration) /t:PublishContainer --os linux --arch x64 -p ContainerRegistry=$(AzureContainerRepositoryName)
  displayName: 'Push container to ACR'
  workingDirectory: '$(Pipeline.Workspace)/api/drop/project'

- task: AzureAppServiceSettings@1
  displayName: 'Configure Azure App Service settings'
  inputs:
    azureSubscription: '${{ parameters.azureSubscriptionName }}'
    appName: '$(AzureAppServiceName)'
    resourceGroupName: '$(AzureResourceGroupName)'
    appSettings: |
      [
        { "name": "Environment",                       "value": "$(Values_Environment)",    "slotSetting": false },
        { "name": "WEBSITES_ENABLE_APP_SERVICE_STORAGE","value": "true",                    "slotSetting": false },
        { "name": "AzureAd__TenantId",                 "value": "$(mcp.TenantId)",          "slotSetting": false },
        { "name": "AzureAd__Instance",                 "value": "$(mcp.Instance)",          "slotSetting": false },
        { "name": "AzureAd__ClientId",                 "value": "$(mcp.ClientId)",          "slotSetting": false },
        { "name": "AzureAd__ClientSecret",             "value": "$(mcp.ClientSecret)",      "slotSetting": false },
        { "name": "AzureAd__Audience",                 "value": "$(mcp.Audience)",          "slotSetting": false },
        { "name": "AzureAd__RequiredScope",            "value": "$(mcp.RequiredScope)",     "slotSetting": false },
        { "name": "AzureMonitor__ConnectionString",    "value": "$(mcp.AzureMonitorCS)",    "slotSetting": false },
        { "name": "HttpMcpServerUrl",                  "value": "$(mcp.httpMcpServerUrl)",  "slotSetting": false },
        { "name": "Mcp__ResourceDocumentationUrl",     "value": "$(mcp.resourceDocUrl)",    "slotSetting": false }
      ]

- task: AzureAppServiceManage@0
  inputs:
    Action: 'Restart Azure App Service'
    azureSubscription: '${{ parameters.azureSubscriptionName }}'
    WebAppName: '$(AzureAppServiceName)'
```

---

### 10. Bicep Infrastructure

Use the shared `i365-mcp-container-webapp.bicep` template in the `Infrastructure/` folder:

```bicep
// Deploy from Infrastructure/i365-mcp-container-webapp.bicep
// Parameters for a new MCP server:
param prefix string = 'i365'
param env string = 'dev'          // dev | test | uat | prod
param mcpname string = '{mcpname}' // e.g. freshdesk, toggl, xero
param acrRepository string = 'ithink365/{lowercasemcpname}mcp'
param acrImageTag string = 'latest'
param containerPort int = 8080
param alwaysOn bool = false        // true for prod
```

Deploy with:

```powershell
az deployment group create `
  --resource-group i365-mcp-dev-rg `
  --template-file Infrastructure/i365-mcp-container-webapp.bicep `
  --parameters prefix=i365 env=dev mcpname={mcpname} `
               acrRepository='ithink365/{lowercasemcpname}mcp' acrImageTag=latest
```

---

## Entra ID App Registration Requirements

Each MCP server needs its own App Registration in the `f0272860-a49b-4746-9d1f-7072e4113e52` tenant:

1. **Create App Registration** — name: `i365-mcp-{mcpname}-{env}`
2. **Expose an API** — add scope `mcp.tools` with admin consent
3. **Set Application ID URI** — e.g. `api://{client-id}`
4. **Note the `ClientId`** — use as both `AzureAd:ClientId` and `AzureAd:Audience`
5. **No client secrets needed** for the server itself — JWT validation uses public JWKS only

MCP clients (Claude Desktop, VS Code Copilot) authenticate via **OAuth 2.0 Authorization Code + PKCE** against the Entra ID tenant, then present a Bearer token with the `mcp.tools` scope.

---

## OpenTelemetry / Azure Monitor

The pattern uses `Azure.Monitor.OpenTelemetry.AspNetCore` which auto-instruments:
- ASP.NET Core requests (traces + metrics)
- HttpClient calls (outbound dependency tracking)
- Custom `ILogger` log entries

The `AzureMonitor:ConnectionString` is left empty in `appsettings.json` and populated at deploy time via App Service application settings. When the connection string is empty the server runs without telemetry (safe for local dev).

---

## Security Rules

| Rule | Enforcement |
|------|-------------|
| All `/api/mcp` calls require a valid Entra ID JWT | `RequireAuthorization(mcpAccessPolicy)` |
| JWT must contain `mcp.tools` scope claim | `RequireClaim("…/claims/scope", "mcp.tools")` |
| `AzureAd:DisableSecurity` must be `false` in all non-dev deployments | Release pipeline variable |
| HTTPS enforced | `UseHttpsRedirection()` + `httpsOnly: true` in Bicep |
| No secrets in `appsettings.json` committed to source | Use App Service settings / Key Vault references |
| CORS permissive (MCP clients can be any origin) | `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` |
| Health and discovery endpoints are unauthenticated | Mapped before `RequireAuthorization` |

---

## Common Patterns

### Per-user credential store (optional)
When the MCP server needs to act on behalf of the calling user (e.g. Toggl):
1. Extract the user subject claim from `IHttpContextAccessor` → `HttpContext.User`
2. Look up per-user credentials from Azure Blob Storage (container: `{service}-user-credentials`)
3. Inject into outbound HTTP client via a `DelegatingHandler`

### Shared/admin API key (simpler, e.g. Freshdesk)
Store a single API key in the App Service settings. Inject via `IOptions<{Service}Options>` into the HTTP client handler.

---

## Placeholders Reference

| Placeholder | Example |
|---|---|
| `{SolutionName}` | `i365.Mcp.Freshdesk` |
| `{ProjectName}` | `i365.Mcp.Freshdesk` |
| `{Namespace}` | `i365.Mcp.Freshdesk` |
| `{Description}` | `Freshdesk helpdesk integration` |
| `{lowercasemcpname}` | `freshdesk` |
| `{mcpname}` | `freshdesk` |
| `{server-folder}` | `freshdesk-mcp-server` |
| `{Service}` | `Freshdesk` |
| `{Section}` | `Freshdesk` |
| `{Domain}` | `Tickets` |
| `{ToolName}` | `GetTickets` |
| `{ServerDisplayName}` | `Freshdesk` |
| `{new-guid}` | Generate with `[guid]::NewGuid()` in PowerShell |
| `{client-id-guid}` | App Registration ClientId |
| `{DateTimeOffset.UtcNow:yyyy-MM-dd}` | Current date at scaffold time |
