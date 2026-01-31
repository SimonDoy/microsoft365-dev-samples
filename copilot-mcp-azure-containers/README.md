# Building a Custom MCP Server as a Container hosted in Azure with .NET MCP SDK and Integrating with Copilot Studio

This guide provides step-by-step instructions to build a custom MCP (Model Control Protocol) server as a containerized application hosted in Azure. It uses the .NET MCP SDK to create the MCP server and integrate it with Copilot Studio so that agent.
For more information about the process please check out [this blog post](https://simondoy.com/2026/01/28/how-to-build-a-custom-mcp-server-with-the-net-mcp-sdk-host-as-an-azure-container-and-connect-to-copilot-studio/)

## Visual Studio Solution

The following code base provides a very simple MCP server; its purpose is to allow you to see how to configure the Visual Studio Project to enable publishing the code as a container to an Azure Container Repository.
[Check out the Simple MCP Server project](./solution/README.md)



## Dev Ops Pipelines
The following pipelines are provided to help you get started with building and deploying the MCP server container to Azure Container Repository and then deploying it to Azure App Services.
[Check out the Dev Ops Pipelines](./dev-ops/README.md)

