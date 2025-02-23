using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta.Models.CloudLicensing;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;

[assembly: FunctionsStartup(typeof(i365.ReadReceipt.Tasks.Startup))]

namespace i365.ReadReceipt.Tasks;

public class Startup : FunctionsStartup
{
    
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        // Register JsonSerializerOptions in the dependency injection container
        builder.Services.AddSingleton(jsonSerializerOptions);
    }

    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        FunctionsHostBuilderContext context = builder.GetContext();

        builder.ConfigurationBuilder
            .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();
    }
}