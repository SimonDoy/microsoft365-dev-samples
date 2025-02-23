# Introduction 
This application is made of an Api and Microsoft Teams App built using SharePoint Framework (SPFx).
The application is a demo and makes up part of a solution. You can read more about it and how to set it up on [my blog](https://www.simondoy.com).

## Api Application Settings
Please find an example of the Api Application Settings.

{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_INPROC_NET8_ENABLED": "1",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "TenantId": "[your tenant id]",
    "ClientId": "[your client id]",
    "ClientSecret": "[your client secret]",
    "Microsoft365GroupId": "[your Microsoft 365 Group / Teams Id]",
    "BusinessScenarioName": "[Name of Business Scenario to Create]",
    "PlannerName": "[Name of Planner Plan to use]",
    "TeamsTaskAppId": "[The App Id for your Teams App that will host the Task]",
    "InDeveloperMode": true,
    "TestUserPrincipalName": "[The email address of your test user bob@domain.com]"
  },
  "Host": {
    "LocalHttpPort": 7165,
    "CORS": "*"
  }
}