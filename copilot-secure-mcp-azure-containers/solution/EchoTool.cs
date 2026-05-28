using Microsoft.Extensions.Logging;

[McpServerToolType]
public class EchoTool(ILogger<EchoTool> logger)
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public string Echo(string message)
    {
        logger.LogTrace("Echo called with message: {Message}", message);
        logger.LogDebug("Echo called with message: {Message}", message);
        logger.LogInformation("Echo called with message: {Message}", message);
        logger.LogWarning("Echo called with message: {Message}", message);
        logger.LogError("Echo called with message: {Message}", message);
        logger.LogCritical("Echo called with message: {Message}", message);

        return $"hello {message}";
    }
}
