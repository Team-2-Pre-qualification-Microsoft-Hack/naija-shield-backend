using System.Text;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel;
using naija_shield_backend.Hubs;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. KEY VAULT — PRODUCTION ONLY
// ==========================================
// In Development, secrets are loaded from appsettings.Development.json (git-ignored).
// In Production, they are pulled from Azure Key Vault via DefaultAzureCredential.
if (builder.Environment.IsProduction())
{
    var keyVaultUri = new Uri("https://kv-naijashield-dev.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

// ==========================================
// 2. READ SECRETS FROM CONFIGURATION
// ==========================================
string openAiKey         = builder.Configuration["OpenAI-Key"]
    ?? throw new InvalidOperationException("OpenAI-Key is not configured");
string cosmosConnString  = builder.Configuration["Cosmos-Connection-String"]
    ?? throw new InvalidOperationException("Cosmos-Connection-String is not configured");
string searchKey         = builder.Configuration["Search-Key"]
    ?? throw new InvalidOperationException("Search-Key is not configured");
string signalrConnString = builder.Configuration["SignalR-Connection-String"]
    ?? throw new InvalidOperationException("SignalR-Connection-String is not configured");

// ==========================================
// 3. CONTROLLERS + SIGNALR
// ==========================================
builder.Services.AddControllers();

// Azure SignalR Service — connection string sourced from config above
builder.Services.AddSignalR().AddAzureSignalR(signalrConnString);

// ==========================================
// 4. SEMANTIC KERNEL — AZURE OPENAI
// ==========================================
var openAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured");
var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured");

builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName,
        endpoint: openAiEndpoint,
        apiKey: openAiKey);

// ==========================================
// 5. COSMOS DB — SINGLETON CLIENT
// ==========================================
builder.Services.AddSingleton(new CosmosClient(cosmosConnString));

// ==========================================
// 6. HTTP CLIENTS
// ==========================================
var sidecarBaseUrl = builder.Configuration["AiSidecar:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient("AiSidecar", client =>
{
    client.BaseAddress = new Uri(sidecarBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ==========================================
// 7. DOMAIN SERVICES (DI REGISTRATIONS)
// ==========================================
builder.Services.AddScoped<IPiiRedactionService, PlaceholderPiiRedactionService>();
builder.Services.AddScoped<IAlertService, LoggingAlertService>();
builder.Services.AddScoped<IThreatScoringService, ThreatScoringService>();
builder.Services.AddScoped<IIncidentRepository, CosmosIncidentRepository>();

// ==========================================
// 8. SWAGGER
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Naija Shield API", Version = "v1" });
});

// ==========================================
// 9. CORS
// ==========================================
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
// 10. BUILD & CONFIGURE PIPELINE
// ==========================================
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Naija Shield API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("FrontendPolicy");
app.MapControllers();
app.MapHub<ThreatHub>("/hubs/threat");

// ==========================================
// 11. LEGACY SMOKE-TEST ENDPOINT (retained)
// ==========================================
app.MapAuthEndpoints();
app.MapSmsEndpoints();

// Keep the original test endpoint
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

// ==========================================
// 8. SEED DEFAULT ADMIN
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.SeedDefaultAdminAsync();
}

app.Run();
