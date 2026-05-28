using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);
const string mcpAccessPolicy = "mcp.tools";

// Configure logging
builder.Logging.AddConsole(cfg =>
{
    cfg.LogToStandardErrorThreshold = LogLevel.Trace;
    cfg.FormatterName = "json";
});

var serverUrl = "http://localhost:5000";

builder.Services.AddHttpContextAccessor();
builder.Configuration.AddEnvironmentVariables();

var requiredScope = builder.Configuration["AzureAd:RequiredScope"] ?? "mcp.tools";
var azureAdInstance = builder.Configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
var azureAdTenantId = builder.Configuration["AzureAd:TenantId"];
var authorizationServerUrl = $"{azureAdInstance}{azureAdTenantId}/v2.0";
var oauthAuthorizationEndpoint = authorizationServerUrl.Replace(
    "/v2.0",
    "/oauth2/v2.0",
    StringComparison.OrdinalIgnoreCase);

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);


//Configure JWT Bearer authentication with custom events
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var existingEvents = options.Events;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = async context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.JwtBearer");

            logger.LogInformation("JWT message received for {Method} {Path}. Authorization header present: {HasAuthorizationHeader}",
                context.Request.Method,
                context.Request.Path,
                context.Request.Headers.ContainsKey("Authorization"));

            if (existingEvents?.OnMessageReceived is not null)
            {
                await existingEvents.OnMessageReceived(context);
            }
        },
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.JwtBearer");

            var claims = context.Principal?.Claims ?? Enumerable.Empty<System.Security.Claims.Claim>();
            var subject = claims.FirstOrDefault(c => c.Type is "sub" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            var audience = claims.FirstOrDefault(c => c.Type == "aud")?.Value;
            var scope = claims.FirstOrDefault(c => c.Type is "scp" or "http://schemas.microsoft.com/identity/claims/scope")?.Value;
            var roles = claims.Where(c => c.Type == "roles").Select(c => c.Value).ToArray();

            logger.LogInformation("JWT token validated. Subject: {Subject}, Audience: {Audience}, Scope: {Scope}, Roles: {Roles}",
                subject,
                audience,
                scope,
                roles.Length == 0 ? "<none>" : string.Join(",", roles));

            if (existingEvents?.OnTokenValidated is not null)
            {
                await existingEvents.OnTokenValidated(context);
            }
        },
        OnAuthenticationFailed = async context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.JwtBearer");

            logger.LogWarning(context.Exception,
                "JWT authentication failed for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (existingEvents?.OnAuthenticationFailed is not null)
            {
                await existingEvents.OnAuthenticationFailed(context);
            }
        },
        OnChallenge = async context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.JwtBearer");

            logger.LogWarning("JWT challenge triggered for {Method} {Path}. Error: {Error}; Description: {Description}",
                context.Request.Method,
                context.Request.Path,
                context.Error,
                context.ErrorDescription);

            if (existingEvents?.OnChallenge is not null)
            {
                await existingEvents.OnChallenge(context);
            }
        },
        OnForbidden = async context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.JwtBearer");

            var scope = context.HttpContext.User.FindFirst("scp")?.Value
                ?? context.HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            logger.LogWarning("JWT token authenticated but authorization failed for {Method} {Path}. Scope claim: {Scope}",
                context.Request.Method,
                context.Request.Path,
                scope ?? "<none>");

            if (existingEvents?.OnForbidden is not null)
            {
                await existingEvents.OnForbidden(context);
            }
        }
    };
});

// Configure OpenID Connect authentication for interactive browser-based login
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = authorizationServerUrl;
    options.ClientId = builder.Configuration.GetValue<string?>("AzureAd:ClientId");
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("offline_access");
    if (!string.IsNullOrEmpty(requiredScope))
    {
        options.Scope.Add(requiredScope);
    }
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(mcpAccessPolicy, policy =>
        policy.RequireClaim("http://schemas.microsoft.com/identity/claims/scope", requiredScope));

// MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => { options.Stateless = true; })
    .WithToolsFromAssembly();

// Add CORS for HTTP transport support in browsers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();



// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable CORS
app.UseCors();


app.MapGet("/health", () => $"Secure MCP server running deployed: UTC: {DateTime.UtcNow}, use /api/mcp path to use the tools");

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/api/mcp").RequireAuthorization(mcpAccessPolicy);

app.MapGet("/.well-known/oauth-protected-resource", (HttpContext httpContext) =>
{
    var baseUrl = BuildResourceUrl(httpContext.Request);

    return Results.Json(new
    {
        resource = $"{baseUrl}/api/mcp",
        authorization_servers = new[] { authorizationServerUrl },
        scopes_supported = new[] { requiredScope, "openid", "profile", "offline_access", "User.Read" },
        code_challenge_methods_supported = new[]
        {
            "S256"
        },
        resource_name = "iThink 365 Toggl MCP Server",
        resource_documentation = $"{baseUrl}/health"
    });
});

app.MapGet(".well-known/oauth-authorization-server", (HttpContext httpContext) =>
{
    var baseUrl = BuildResourceUrl(httpContext.Request);
    var requiredScopeWithResource = $"{baseUrl.TrimEnd('/')}/{requiredScope.TrimStart('/')}";

    return Results.Json(new
    {
        issuer = authorizationServerUrl,
        authorization_endpoint = $"{oauthAuthorizationEndpoint}/authorize",
        token_endpoint = $"{oauthAuthorizationEndpoint}/token",
        jwks_uri = $"{oauthAuthorizationEndpoint}/discovery/v2.0/keys",
        response_types_supported = new[] { "code" },
        scopes_supported = new[] { requiredScopeWithResource, "openid", "profile", "offline_access", "User.Read" },
        code_challenge_methods_supported = new[]
        {
            "S256"
        }
    });
});

try
{
    app.Run();
}
catch (Exception)
{

    throw;
}


// Helper method to build the resource URL dynamically based on the incoming request. This is useful for scenarios where the server might be accessed through different URLs (e.g., localhost during development and a custom domain in production).
static string BuildResourceUrl(HttpRequest request)
{
    var pathBase = request.PathBase.HasValue ? request.PathBase.Value! : "/";
    if (!pathBase.EndsWith('/'))
    {
        pathBase += "/";
    }

    return $"{request.Scheme}://{request.Host}";
}
