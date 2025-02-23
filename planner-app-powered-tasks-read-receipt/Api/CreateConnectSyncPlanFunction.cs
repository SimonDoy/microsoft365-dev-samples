using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta;
using Azure.Identity;
using System.Linq;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Solutions.BusinessScenarios.Item.Planner.GetPlan;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using i365.ReadReceipt.Tasks.Model;
using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;

namespace i365.ReadReceipt.Tasks
{
    public class ConnectSyncPlanFunctions
    {
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        public ConnectSyncPlanFunctions(IConfiguration configuration, JsonSerializerOptions jsonSerializerOptions)
        {
            _configuration = configuration;
            _jsonSerializerOptions = jsonSerializerOptions;
        }

        [FunctionName("CreateConnectSyncPlan")]
        public async System.Threading.Tasks.Task CreateConnectSyncPlanFunction([TimerTrigger("0 */30 * * * *", RunOnStartup = true)]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            try
            {
                // The client credentials flow requires that you request the
                // /.default scope, and pre-configure your permissions on the
                // app registration in Azure. An administrator must grant consent
                // to those permissions beforehand.
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                // using Azure.Identity;
                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var tenantId = _configuration.GetValue<string>("TenantId", "");
                var clientId = _configuration.GetValue<string>("ClientId", "");
                var clientSecret = _configuration.GetValue<string>("ClientSecret", "");

                var businessScenarioName = _configuration.GetValue<string>("BusinessScenarioName", "");
                string plannerName = _configuration.GetValue<string>("PlannerName", "");
                string groupId = _configuration.GetValue<string>("Microsoft365GroupId", "");
                BusinessScenarioGroupTarget theBusinessScenarioPlannerTaskTargetBase = new BusinessScenarioGroupTarget()
                {
                    GroupId = groupId,
                    TaskTargetKind = PlannerTaskTargetKind.Group
                };

                // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
                var clientSecretCredential = new ClientSecretCredential(
                    tenantId, clientId, clientSecret, options);

                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                
                var businessScenarios = await graphClient.Solutions.BusinessScenarios.GetAsync();
                Microsoft.Graph.Beta.Models.BusinessScenario theBusinessScenario = null;
                theBusinessScenario = businessScenarios.Value.FirstOrDefault(s => s.UniqueName == businessScenarioName);
                if (theBusinessScenario == null)
                {
                    var businessScenarioRequestObject = new Microsoft.Graph.Beta.Models.BusinessScenario()
                    {
                        UniqueName = businessScenarioName,
                        DisplayName = businessScenarioName,
                        OwnerAppIds = new System.Collections.Generic.List<string> { clientId },
                    };

                    theBusinessScenario = await graphClient.Solutions.BusinessScenarios.PostAsync(businessScenarioRequestObject);

                }

                var getPlanRequestBody = new GetPlanPostRequestBody()
                {
                    Target = theBusinessScenarioPlannerTaskTargetBase
                };

                // get the planner object.
                BusinessScenarioPlanReference theBusinessScenarioPlanReference = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.GetPlan.PostAsync(getPlanRequestBody);
                if (theBusinessScenarioPlanReference != null)
                {
                    log.LogInformation($"Plan created: {theBusinessScenarioPlanReference.Id}");
                    // update plan if the plan has the wrong name.
                    
                    var theBusinessScenarioPlanConfiguration = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.PlanConfiguration.GetAsync();
                    log.LogInformation($"Plan Configuration: {theBusinessScenarioPlanConfiguration.Id}");

                    string eTag = "*";
                    if (theBusinessScenarioPlanConfiguration.AdditionalData.ContainsKey("@odata.etag"))
                    {
                        eTag = theBusinessScenarioPlanConfiguration.AdditionalData["@odata.etag"].ToString();
                    }

                    bool planConfigurationNeedsUpdating = false;
                    var watchBucketName = "To Watch";
                    var planBucketNames = new string[] { "To Watch", "Watched" };
                    var enUSLocatization = theBusinessScenarioPlanConfiguration.Localizations.FirstOrDefault(l => l.LanguageTag == "en-us");
                    if (enUSLocatization == null)
                    {
                        enUSLocatization = new PlannerPlanConfigurationLocalization()
                        {
                            LanguageTag = "en-us",
                            PlanTitle = plannerName,
                            AdditionalData = new Dictionary<string, object>(),
                        };
                        theBusinessScenarioPlanConfiguration.Localizations.Add(enUSLocatization);
                    }
                    else
                    {
                        enUSLocatization.PlanTitle = plannerName;
                        foreach (var bucketInPlan in theBusinessScenarioPlanConfiguration.Buckets)
                        {
                            // check that there is a planner localization configuration for that bucket.
                            var bucketLocalization = enUSLocatization.Buckets.FirstOrDefault(b => b.ExternalBucketId == bucketInPlan.ExternalBucketId);
                            if (bucketLocalization == null)
                            {
                                // create bucket localization
                                bucketLocalization = new PlannerPlanConfigurationBucketLocalization()
                                {
                                    ExternalBucketId = bucketInPlan.ExternalBucketId,
                                    Name = bucketInPlan.ExternalBucketId,
                                    AdditionalData = new Dictionary<string, object>(),
                                };
                                enUSLocatization.Buckets.Add(bucketLocalization);
                            }
                        }

                        planConfigurationNeedsUpdating = true;

                        foreach (var bucketNameCheck in planBucketNames)
                        {
                            var defaultBucket = theBusinessScenarioPlanConfiguration.Buckets.FirstOrDefault(b => b.ExternalBucketId == bucketNameCheck);
                            if (defaultBucket == null)
                            {
                                theBusinessScenarioPlanConfiguration.Buckets.Add(new PlannerPlanConfigurationBucketDefinition()
                                {
                                    ExternalBucketId = bucketNameCheck,
                                });

                                planConfigurationNeedsUpdating = true;
                            }
                        }
                    }
                    

                    if(planConfigurationNeedsUpdating)
                    {
                        var updatedPlanConfiguration = new PlannerPlanConfiguration()
                        {
                            DefaultLanguage = "en-us",
                            Buckets = theBusinessScenarioPlanConfiguration.Buckets,
                            Localizations = theBusinessScenarioPlanConfiguration.Localizations
                        };
                        theBusinessScenarioPlanConfiguration = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.PlanConfiguration.PatchAsync(updatedPlanConfiguration, rc =>
                        {
                            rc.Headers.Add("If-Match", eTag);
                        });
                    }

                }



            }
            catch (Exception ex)
            {

                throw;
            }
        }

        // Existing functions...
        [FunctionName("CreateBusinessScenarioTask")]
        public async Task<IActionResult> CreateBusinessScenarioTask(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tasks")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"Creating task");

            try
            {
                var readReceiptTaskResponseAsString = await req.ReadAsStringAsync();
                var readReceiptTaskResponse = System.Text.Json.JsonSerializer.Deserialize<ReadReceiptTaskResponse>(readReceiptTaskResponseAsString, _jsonSerializerOptions);

                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var tenantId = _configuration.GetValue<string>("TenantId", "");
                var clientId = _configuration.GetValue<string>("ClientId", "");
                var clientSecret = _configuration.GetValue<string>("ClientSecret", "");
                var inDevelopmentMode = _configuration.GetValue<bool>("InDeveloperMode", false);
                var teamsAppId = _configuration.GetValue<string>("TeamsTaskAppId", "");
                var businessScenarioName = _configuration.GetValue<string>("BusinessScenarioName", "");

                if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(clientId) || String.IsNullOrEmpty(clientSecret))
                {
                    throw new ArgumentNullException("TenantId, ClientId and ClientSecret not configured.");
                }

                var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                var businessScenarios = await graphClient.Solutions.BusinessScenarios.GetAsync();
                var theBusinessScenario = businessScenarios.Value.FirstOrDefault(s => s.UniqueName == businessScenarioName);

                if (theBusinessScenario == null)
                {
                    return new NotFoundObjectResult("Business scenario not found.");
                }

                BusinessScenarioTask task = null;
                try
                {
                    var completedByUser = await graphClient.Users[readReceiptTaskResponse.UserPrincipalName].GetAsync();

                    string groupId = _configuration.GetValue<string>("Microsoft365GroupId", "");
                    var watchBucketName = "To Watch";
                    

                    var teamsAppExternalObjectId = readReceiptTaskResponse.ExternalId;
                    var teamsAppTaskTitle = $"Confirm You Have Read Content {readReceiptTaskResponse.ContentTitle}"; // do not include the word task in your title. do not include # in your title.
                    var teamsTabEntityId = "fac21461-7439-49d1-a2ce-f5ab67b67a91";
                    var teamsAppDevelopmentLinkUrl = $"https://ithinksharepointltd.sharepoint.com/_layouts/15/TeamsWorkBench.aspx?componentId=fac21461-7439-49d1-a2ce-f5ab67b67a91&subEntityId={teamsAppExternalObjectId}&teams&personal&forceLocale=en-gb&loadSPFX=true&debugManifestsFile=https://localhost:4321/temp/manifests.js";
                    var teamsAppLinkUrl = $"https://ithinksharepointltd.sharepoint.com/_layouts/15/teamshostedapp.aspx?componentId=fac21461-7439-49d1-a2ce-f5ab67b67a91&subEntityId={teamsAppExternalObjectId}&teams&personal&forceLocale=en-gb&loadSPFX=true";
                    if (inDevelopmentMode)
                    {
                        teamsAppLinkUrl = teamsAppDevelopmentLinkUrl;
                    }

                    var contextObject = $"{{\"appId\":\"{teamsAppId}\",\"entityId\":\"{teamsTabEntityId}\",\"contentUrl\":\"{teamsAppLinkUrl}\",\"name\":\"{teamsAppTaskTitle}\",\"openMode\":\"modal\"}}";

                    // this link works
                    //https://teams.microsoft.com/l/entity/bc6917af-6cb5-4894-9ef4-f00e82f423d0/aac21461-7439-49d1-a2ce-f5ab67b67a91?label=Complete%20Task&context=%7B%22subEntityId%22%3A%22Order%2312029%22%7D

                    // when using the spfx hosted teams you can use the following link
                    //https://ithinksharepointltd.sharepoint.com/_layouts/15/TeamsWorkBench.aspx?componentId=fac21461-7439-49d1-a2ce-f5ab67b67a91&teams&personal&forceLocale=en-gb&loadSPFX=true&debugManifestsFile=https://localhost:4321/temp/manifests.js

                    var encodedContextObject = System.Uri.EscapeDataString(contextObject);

                    // the teams link must use a stage link https://learn.microsoft.com/en-gb/microsoftteams/app-powered-tasks-in-planner#example
                    var teamsAppTaskLink = $"https://teams.microsoft.com/l/stage/{teamsAppId}/0?context={encodedContextObject}";
                    var encodedTeamsAppTaskLink = Encode(teamsAppTaskLink);

                    // setup the external references used by the teams powered app task.
                    var plannerExternalReferences = new PlannerExternalReferences();
                    var plannerExternalReference = new PlannerExternalReference();

                    plannerExternalReference.Alias = teamsAppTaskTitle;
                    plannerExternalReference.Type = "TeamsHostedApp";
                    plannerExternalReference.PreviewPriority = " !";
                    
                    plannerExternalReferences.AdditionalData.Add(encodedTeamsAppTaskLink, plannerExternalReference);

                    if (!String.IsNullOrEmpty(readReceiptTaskResponse.ContentTitle) && !String.IsNullOrEmpty(readReceiptTaskResponse.ContentUrl)) {
                        var contentExternalReference = new PlannerExternalReference();
                        var encodedContentLink = Encode(readReceiptTaskResponse.ContentUrl);
                        contentExternalReference.Alias = readReceiptTaskResponse.ContentTitle;
                        contentExternalReference.Type = "Word";
                        plannerExternalReferences.AdditionalData.Add(encodedContentLink, contentExternalReference);

                    }

                    var taskAssignee = new PlannerAssignment()
                    {
                        OdataType = "#microsoft.graph.plannerAssignment",
                        OrderHint = " !",
                    };

                    var userObjectId = completedByUser.Id;
                    var taskAssignments = new PlannerAssignments();
                    if (!taskAssignments.AdditionalData.ContainsKey(userObjectId))
                    {
                        taskAssignments.AdditionalData.Add(userObjectId, taskAssignee);
                    }

                    // create a task
                    var businessScenarioTask = new BusinessScenarioTask()
                    {
                        Title = teamsAppTaskTitle,
                        PercentComplete = 0,
                        StartDateTime = DateTime.Now,
                        DueDateTime = DateTime.Now.AddDays(1),
                        Target = new BusinessScenarioGroupTarget()
                        {
                            GroupId = groupId,
                            TaskTargetKind = PlannerTaskTargetKind.Group
                        },
                        BusinessScenarioProperties = new BusinessScenarioProperties
                        {
                            ExternalObjectId = teamsAppExternalObjectId,
                            ExternalBucketId = watchBucketName,
                        },
                        Details = new PlannerTaskDetails
                        {
                            
                            Description = $"Please read the content, {readReceiptTaskResponse.ContentTitle} and confirm that you have read the content",
                            References = plannerExternalReferences
                        },
                        Assignments = taskAssignments

                    };

                    await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks.PostAsync(businessScenarioTask);

                }
                catch (Exception ex)
                {
                    // do not throw
                    throw ex;
                }

                return new OkObjectResult(task);
            }
            catch (Exception ex)
            {
                log.LogError($"Error searching for task: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("SearchBusinessScenarioTask")]
        public async Task<IActionResult> SearchBusinessScenarioTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tasks/search/{externalId}")] HttpRequest req,
            string externalId,
            ILogger log)
        {
            log.LogInformation($"Searching for task with external ID: {externalId}");

            try
            {
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var tenantId = _configuration.GetValue<string>("TenantId", "");
                var clientId = _configuration.GetValue<string>("ClientId", "");
                var clientSecret = _configuration.GetValue<string>("ClientSecret", "");
                var businessScenarioName = _configuration.GetValue<string>("BusinessScenarioName", "");

                var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                var businessScenarios = await graphClient.Solutions.BusinessScenarios.GetAsync();
                var theBusinessScenario = businessScenarios.Value.FirstOrDefault(s => s.UniqueName == businessScenarioName);

                if (theBusinessScenario == null)
                {
                    return new NotFoundObjectResult("Business scenario not found.");
                }

                BusinessScenarioTask task = null;
                try
                {
                    var tasks = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"businessScenarioProperties/externalObjectId eq '{externalId}'";
                        requestConfiguration.QueryParameters.Expand = ["Details"];
                    });

                    task = tasks.Value.FirstOrDefault(t => t.BusinessScenarioProperties.ExternalObjectId == externalId);

                }
                catch (Exception ex)
                {
                    // do not throw
                }
                
                if (task == null)
                {
                    return new NotFoundObjectResult("Task not found.");
                }

                var contentUrl = "";
                var contentTitle = "";
                var userPrincipalName = await ResolveAssignedUser(graphClient, task, log);
                // cast {Microsoft.Kiota.Abstractions.Serialization.UntypedObject} to ExternalReference
                IList<KeyValuePair<string, PlannerExternalReference>> externalReferences = new List<KeyValuePair<string, PlannerExternalReference>>();
                foreach(var additionalDataNode in task.Details.References.AdditionalData)
                {
                    try
                    {
                        var jsonSerializerOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        };
                        var plannerExternalReferenceObject = additionalDataNode.Value;
                        var plannerExternalReferenceAsUntypedObject = plannerExternalReferenceObject as UntypedObject;
                        var serializedExternalReference = await KiotaJsonSerializer.SerializeAsStringAsync(plannerExternalReferenceAsUntypedObject);
                        var externalReferenceObjectElement = JsonDocument.Parse(serializedExternalReference);
                        
                        var externalReference = externalReferenceObjectElement.Deserialize<PlannerExternalReference>(jsonSerializerOptions); 
                        if(externalReference != null)
                        {
                            KeyValuePair<string, PlannerExternalReference> keyValuePair = new KeyValuePair<string, PlannerExternalReference>(additionalDataNode.Key, externalReference);
                            externalReferences.Add(keyValuePair);
                        }

                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error casting external reference: {ex.Message}");
                    }
                }

                var relatedContent = externalReferences.FirstOrDefault(p => (p.Value.Type == "Word" || p.Value.Type == "PowerPoint" || p.Value.Type == "Other"));
                if (relatedContent.Key != null)
                {
                    contentTitle = relatedContent.Value.Alias;
                    // decode encoded uri
                    contentUrl = System.Uri.UnescapeDataString(relatedContent.Key);
                }

                var taskResponse = new ReadReceiptTaskResponse()
                {
                    Id = task.Id,
                    Description = task.HasDescription.HasValue && task.HasDescription.Value ? task.Details.Description : "",
                    ExternalId = externalId,
                    HasReadContent = false,
                    PercentComplete = task.PercentComplete,
                    UserPrincipalName = userPrincipalName,
                    UnderstandingLevel = "",
                    ConfirmationDate = task.CompletedDateTime,
                    ContentUrl = contentUrl,
                    ContentTitle = contentTitle
                };

                return new OkObjectResult(taskResponse);
                //return await req.CreateOkJsonResponse(taskResponse);
            }
            catch (Exception ex)
            {
                log.LogError($"Error searching for task: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("UpdateBusinessScenarioTask")]
        public async Task<IActionResult> UpdateBusinessScenarioTask(
          [HttpTrigger(AuthorizationLevel.Function, ["patch", "post"], Route = "tasks/{externalId}")] HttpRequest req,
          string externalId,
          ILogger log)
        {
            log.LogInformation($"Updating task with external ID: {externalId}");

            try
            {
                var businessScenarioTaskAsString = await req.ReadAsStringAsync();
                var businessScenarioTask = System.Text.Json.JsonSerializer.Deserialize<BusinessScenarioTask>(businessScenarioTaskAsString);
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var tenantId = _configuration.GetValue<string>("TenantId", "");
                var clientId = _configuration.GetValue<string>("ClientId", "");
                var clientSecret = _configuration.GetValue<string>("ClientSecret", "");
                var businessScenarioName = _configuration.GetValue<string>("BusinessScenarioName", "");

                //update with your test user object id.
                var testUserUPN = _configuration.GetValue<string>("TestUserUserPrincipalName", ""); ;
                

                var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                var businessScenarios = await graphClient.Solutions.BusinessScenarios.GetAsync();
                var theBusinessScenario = businessScenarios.Value.FirstOrDefault(s => s.UniqueName == businessScenarioName);

                if (theBusinessScenario == null)
                {
                    return new NotFoundObjectResult("Business scenario not found.");
                }

                BusinessScenarioTask taskToUpdate = null;
                try
                {
                    var tasks = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"businessScenarioProperties/externalObjectId eq '{externalId}'";
                    });

                    taskToUpdate = tasks.Value.FirstOrDefault(t => t.BusinessScenarioProperties.ExternalObjectId == externalId);

                }
                catch (Exception ex)
                {
                    // do not throw
                }

                if (taskToUpdate == null)
                {
                    return new NotFoundObjectResult("Task not found.");
                }
                var taskAssignee = new PlannerAssignment()
                {
                    OdataType = "#microsoft.graph.plannerAssignment",
                    OrderHint = " !",
                };

                var user = await graphClient.Users[testUserUPN].GetAsync();
                var userObjectId = user.Id;
                var taskAssignments = new PlannerAssignments();
                if (!taskToUpdate.Assignments.AdditionalData.ContainsKey(userObjectId))
                {
                    taskAssignments.AdditionalData.Add(userObjectId, taskAssignee);
                }
                
                

                var updateTask = new BusinessScenarioTask();
                updateTask.Assignments = taskAssignments;
                updateTask.PercentComplete = 50;
                updateTask.StartDateTime = DateTimeOffset.UtcNow;
                updateTask.DueDateTime = DateTimeOffset.UtcNow.AddDays(7);

                var taskDetails = new PlannerTaskDetails()
                {
                    Description = "Here is a description, you need to read this content."
                };

                var eTag = "*";
                if(taskToUpdate.AdditionalData.ContainsKey("@odata.etag"))
                {
                    eTag = taskToUpdate.AdditionalData["@odata.etag"].ToString();
                }

                // Update the task in Microsoft Graph
                await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].PatchAsync(updateTask, conf =>
                {
                    conf.Headers.Add("If-Match", eTag);
                });

                var taskDetailsToUpdate = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].Details.GetAsync();
                eTag = "*";
                if (taskDetailsToUpdate.AdditionalData.ContainsKey("@odata.etag"))
                {
                    eTag = taskDetailsToUpdate.AdditionalData["@odata.etag"].ToString();
                }

                // update the task details.
                taskDetails = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].Details.PatchAsync(taskDetails, conf =>
                {
                    conf.Headers.Add("If-Match", eTag);
                });

                return new OkObjectResult(taskToUpdate);
            }
            catch (Exception ex)
            {
                log.LogError($"Error searching for task: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("CompleteReadReceiptTaskBusinessScenarioTask")]
        public async Task<IActionResult> CompleteReadReceiptTaskBusinessScenarioTask(
          [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tasks/{externalId}/complete")] HttpRequest req,
          string externalId,
          ILogger log)
        {
            log.LogInformation($"Completing task with external ID: {externalId}");

            try
            {
                var readReceiptTaskResponseAsString = await req.ReadAsStringAsync();
                var readReceiptTaskResponse = System.Text.Json.JsonSerializer.Deserialize<ReadReceiptTaskResponse>(readReceiptTaskResponseAsString, _jsonSerializerOptions);
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                var tenantId = _configuration.GetValue<string>("TenantId", "");
                var clientId = _configuration.GetValue<string>("ClientId", "");
                var clientSecret = _configuration.GetValue<string>("ClientSecret", "");
                var businessScenarioName = _configuration.GetValue<string>("BusinessScenarioName", "");

                var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                var businessScenarios = await graphClient.Solutions.BusinessScenarios.GetAsync();
                var theBusinessScenario = businessScenarios.Value.FirstOrDefault(s => s.UniqueName == businessScenarioName);

                if (theBusinessScenario == null)
                {
                    return new NotFoundObjectResult("Business scenario not found.");
                }

                BusinessScenarioTask taskToUpdate = null;
                try
                {
                    var tasks = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"businessScenarioProperties/externalObjectId eq '{externalId}'";
                    });

                    taskToUpdate = tasks.Value.FirstOrDefault(t => t.BusinessScenarioProperties.ExternalObjectId == externalId);

                }
                catch (Exception ex)
                {
                    // do not throw
                }

                if (taskToUpdate == null)
                {
                    return new NotFoundObjectResult("Task not found.");
                }

                // validate user
                var completedByUser = await graphClient.Users[readReceiptTaskResponse.UserPrincipalName].GetAsync();

                var updateTask = new BusinessScenarioTask();
                updateTask.PercentComplete = 100;
                

                var taskDetails = new PlannerTaskDetails()
                {
                    Description = $"Read Receipt Task has been completed. \r\n Has read content? {readReceiptTaskResponse.HasReadContent} .\r\n Level of understanding: {readReceiptTaskResponse.UnderstandingLevel}. \r\n Confirmed on: {readReceiptTaskResponse.ConfirmationDate}"
                };

                var eTag = "*";
                if (taskToUpdate.AdditionalData.ContainsKey("@odata.etag"))
                {
                    eTag = taskToUpdate.AdditionalData["@odata.etag"].ToString();
                }

                // Update the task in Microsoft Graph
                await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].PatchAsync(updateTask, conf =>
                {
                    conf.Headers.Add("If-Match", eTag);
                });

                var taskDetailsToUpdate = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].Details.GetAsync();
                eTag = "*";
                if (taskDetailsToUpdate.AdditionalData.ContainsKey("@odata.etag"))
                {
                    eTag = taskDetailsToUpdate.AdditionalData["@odata.etag"].ToString();
                }

                // update the task details.
                taskDetails = await graphClient.Solutions.BusinessScenarios[theBusinessScenario.Id].Planner.Tasks[taskToUpdate.Id].Details.PatchAsync(taskDetails, conf =>
                {
                    conf.Headers.Add("If-Match", eTag);
                });

                return new OkObjectResult(taskToUpdate);
            }
            catch (Exception ex)
            {
                log.LogError($"Error searching for task: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }


        /// <summary>
        /// Encodes the url of an external reference to be compatible with a OData property naming requirements.
        /// </summary>
        /// <param name="externalReferenceUrl">Url to encode</param>
        /// <returns>Encoded Url</returns>
        private static string Encode(string externalReferenceUrl)
        {
            //var Conversions = new string[,] { { "%", "%25" }, { "@", "%40" }, { ".", "%2E" }, { ":", "%3A" }, { "#", "%23" }, { "{", "%7B" }, { "}", "%7D" }, { "\"", "%22" } };
            var Conversions = new string[,] { { "%", "%25" }, { "@", "%40" }, { ".", "%2E" }, { ":", "%3A" }, { "#", "%23" } };
            if (string.IsNullOrEmpty(externalReferenceUrl))
            {
                throw new ArgumentNullException(nameof(externalReferenceUrl));
            }

            for (int i = 0; i < Conversions.GetLength(0); i++)
            {
                externalReferenceUrl = externalReferenceUrl.Replace(Conversions[i, 0], Conversions[i, 1]);
            }

            return externalReferenceUrl;
        }

        /// <summary>
        /// Decodes an encoded the url of an external reference.
        /// </summary>
        /// <param name="externalReferenceUrl">Url to decode</param>
        /// <returns>Decoded Url</returns>
        private static string Decode(string externalReferenceUrl)
        {
            var Conversions = new string[,] { { "%", "%25" }, { "@", "%40" }, { ".", "%2E" }, { ":", "%3A" }, { "#", "%23" } };
            if (string.IsNullOrEmpty(externalReferenceUrl))
            {
                throw new ArgumentNullException(nameof(externalReferenceUrl));
            }

            for (int i = Conversions.GetLength(0) - 1; i >= 0; i--)
            {
                externalReferenceUrl = externalReferenceUrl.Replace(Conversions[i, 1], Conversions[i, 0]);
            }

            return externalReferenceUrl;
        }

        // write a function called Resolve Assigned user by reading the guid from the assigned task and using graph api to find it. 
        // If no user is assigned then return back a blank string.
        private async Task<string> ResolveAssignedUser(GraphServiceClient graphClient, BusinessScenarioTask task, ILogger log)
        {
            if (task.Assignments == null || !task.Assignments.AdditionalData.Any())
            {
                log.LogInformation("No user assigned to the task.");
                return string.Empty;
            }

            var userObjectId = task.Assignments.AdditionalData.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(userObjectId))
            {
                log.LogInformation("No user GUID found in the task assignments.");
                return string.Empty;
            }

            try
            {
                var user = await graphClient.Users[userObjectId].GetAsync();
                return user.UserPrincipalName;
            }
            catch (Exception ex)
            {
                log.LogError($"Error retrieving user with GUID {userObjectId}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
