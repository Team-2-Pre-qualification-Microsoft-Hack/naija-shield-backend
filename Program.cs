using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SECURE KEY VAULT CONNECTION
// ==========================================
string keyVaultUri = "https://rg-naijashield-dev-key.vault.azure.net/";
var credential = new DefaultAzureCredential(); // Uses your logged-in Azure CLI profile
var secretClient = new SecretClient(new Uri(keyVaultUri), credential);

Console.WriteLine("Fetching secrets from Azure Key Vault...");

// ==========================================
// 2. FETCH ALL KEYS EXACTLY AS NAMED
// ==========================================
string openAiKey = (await secretClient.GetSecretAsync("OpenAI-Key")).Value.Value;
string cosmosConnString = (await secretClient.GetSecretAsync("Cosmos-Connection-String")).Value.Value;
string searchKey = (await secretClient.GetSecretAsync("Search-Key")).Value.Value;
string signalrConnString = (await secretClient.GetSecretAsync("SignalR-Connection-String")).Value.Value;

Console.WriteLine("All 4 .NET keys successfully retrieved!");

// ==========================================
// 3. INITIALIZE SERVICES
// ==========================================
// Connect to Cosmos DB
var cosmosClient = new CosmosClient(cosmosConnString);

// Connect Semantic Kernel to Azure OpenAI
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: "gpt-5.4-mini", // Must match your deployment name in Azure Foundry
        endpoint: "https://ai-hubnaijashielddev293511702953.openai.azure.com/", 
        apiKey: openAiKey
    );

var app = builder.Build();

// ==========================================
// 4. PLUG & PLAY TEST ENDPOINT
// ==========================================
app.MapGet("/api/test-scam", async (Kernel kernel) =>
{
    // The prompt simulating a transcribed call or SMS
    string systemPrompt = @"You are an AI assistant. Please analyze this message and return a JSON object with 'decision' (BLOCK/ALLOW). 
    Message: 'Hello, I would like to check my account balance.'";
    
    try 
    {
        var response = await kernel.InvokePromptAsync(systemPrompt);
        return Results.Ok(new { 
            status = "Success",
            test_database_status = "Cosmos DB Client Initialized",
            ai_decision = response.ToString() 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();