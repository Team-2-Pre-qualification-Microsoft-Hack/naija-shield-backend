using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel;
using naija_shield_backend.Hubs;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. KEY VAULT — FETCH ALL SECRETS
// ==========================================
// URI comes from appsettings.json so it can be overridden per environment.
// All secrets are fetched once at startup; none are stored in appsettings.
var keyVaultUri = builder.Configuration["KeyVault:Uri"]
    ?? throw new InvalidOperationException("KeyVault:Uri is not configured in appsettings.json");

var credential = new DefaultAzureCredential();
var secretClient = new SecretClient(new Uri(keyVaultUri), credential);

Console.WriteLine("Fetching secrets from Azure Key Vault...");

// Actual secret names as they exist in the vault (differ from the spec's double-dash convention)
string openAiKey          = (await secretClient.GetSecretAsync("OpenAI-Key")).Value.Value;
string cosmosConnString   = (await secretClient.GetSecretAsync("Cosmos-Connection-String")).Value.Value;
string searchKey          = (await secretClient.GetSecretAsync("Search-Key")).Value.Value;        // retained for future use
string signalrConnString  = (await secretClient.GetSecretAsync("SignalR-Connection-String")).Value.Value;

Console.WriteLine("All Key Vault secrets retrieved successfully.");

// ==========================================
// 2. CONTROLLERS + SIGNALR
// ==========================================
builder.Services.AddControllers();

// Azure SignalR Service — connection string sourced from Key Vault above
builder.Services.AddSignalR().AddAzureSignalR(signalrConnString);

// ==========================================
// 3. SEMANTIC KERNEL — AZURE OPENAI
// ==========================================
// Non-secret config (endpoint, deployment name) lives in appsettings.json
var openAiEndpoint    = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured");
var deploymentName    = builder.Configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured");

builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName,
        endpoint: openAiEndpoint,
        apiKey: openAiKey);

// ==========================================
// 4. COSMOS DB — SINGLETON CLIENT
// ==========================================
// CosmosClient is thread-safe and designed to be shared; register as singleton.
// Database and container are created lazily on first write by CosmosIncidentRepository.
builder.Services.AddSingleton(new CosmosClient(cosmosConnString));

// ==========================================
// 5. HTTP CLIENTS
// ==========================================
// Named client for the Python AI sidecar (voice transcription + deepfake scoring)
var sidecarBaseUrl = builder.Configuration["AiSidecar:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient("AiSidecar", client =>
{
    client.BaseAddress = new Uri(sidecarBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ==========================================
// 6. DOMAIN SERVICES (DI REGISTRATIONS)
// ==========================================
// IPiiRedactionService — regex placeholder until Presidio is deployed
builder.Services.AddScoped<IPiiRedactionService, PlaceholderPiiRedactionService>();

// IAlertService — logs alert details; swap for Africa's Talking outbound SMS later
builder.Services.AddScoped<IAlertService, LoggingAlertService>();

// IThreatScoringService — calls Azure OpenAI via Semantic Kernel
builder.Services.AddScoped<IThreatScoringService, ThreatScoringService>();

// IIncidentRepository — writes to Cosmos DB; creates DB/container if missing
builder.Services.AddScoped<IIncidentRepository, CosmosIncidentRepository>();

// ==========================================
// 7. CORS
// ==========================================
// AllowCredentials() is required for SignalR negotiate handshake from the browser.
// The frontend origin must be explicit — AllowAnyOrigin() is incompatible with credentials.
var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ==========================================
// 8. BUILD & CONFIGURE PIPELINE
// ==========================================
var app = builder.Build();

app.UseCors("FrontendPolicy");
app.MapControllers();
app.MapHub<ThreatHub>("/hubs/threat");

// ==========================================
// 9. LEGACY SMOKE-TEST ENDPOINT (retained)
// ==========================================
app.MapGet("/api/test-scam", async (Kernel kernel) =>
{
    const string prompt = """
        You are an AI assistant. Please analyse this message and return a JSON object
        with 'decision' (BLOCK/ALLOW).
        Message: 'Hello, I would like to check my account balance.'
        """;
    try
    {
        var response = await kernel.InvokePromptAsync(prompt);
        return Results.Ok(new
        {
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
