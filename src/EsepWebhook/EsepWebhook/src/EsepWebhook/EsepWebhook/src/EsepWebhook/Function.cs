using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EsepWebhook
{
    public class Function
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation($"Received request");
            
            string slackUrl = Environment.GetEnvironmentVariable("SLACK_URL");
            if (string.IsNullOrEmpty(slackUrl))
            {
                context.Logger.LogError("SLACK_URL environment variable is not set.");
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = "SLACK_URL not configured" }),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            string message = "Webhook received";

            // Parse GitHub issue webhook
            if (!string.IsNullOrEmpty(request.Body))
            {
                try
                {
                    var webhook = JsonSerializer.Deserialize<JsonElement>(request.Body);
                    
                    // Check if it's a GitHub issue event
                    if (webhook.TryGetProperty("action", out JsonElement action) && 
                        webhook.TryGetProperty("issue", out JsonElement issue))
                    {
                        string actionType = action.GetString() ?? "unknown";
                        string issueTitle = issue.GetProperty("title").GetString() ?? "No title";
                        string issueUrl = issue.GetProperty("html_url").GetString() ?? "";
                        int issueNumber = issue.GetProperty("number").GetInt32();
                        
                        message = $"🎯 Issue #{issueNumber} was *{actionType}*\n" +
                                 $"*Title:* {issueTitle}\n" +
                                 $"*URL:* {issueUrl}";
                        
                        context.Logger.LogInformation($"GitHub issue event: {actionType} - {issueTitle}");
                    }
                    // Handle custom test messages
                    else if (webhook.TryGetProperty("message", out JsonElement msg))
                    {
                        message = msg.GetString() ?? "Test message";
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error parsing webhook: {ex.Message}");
                    message = "Error parsing webhook data";
                }
            }

            // Send to Slack
            var payload = new { text = message };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(slackUrl, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            context.Logger.LogInformation($"Slack response: {responseContent}");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new { 
                    message = "Webhook processed successfully",
                    slackMessage = message
                }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}
