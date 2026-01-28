using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.AddConsole(cfg =>
{
    cfg.LogToStandardErrorThreshold = LogLevel.Trace;
    cfg.FormatterName = "json";
});

var serverUrl = "http://localhost:5000";

builder.Configuration.AddEnvironmentVariables();

var hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
if(!String.IsNullOrEmpty(hostName))
{
    serverUrl = "https://" + hostName;
}



builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie();


builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer().WithToolsFromAssembly().WithHttpTransport(configureOptions => { configureOptions.Stateless = true; });

var app = builder.Build();

app.MapMcp("mcp");

await app.RunAsync();
