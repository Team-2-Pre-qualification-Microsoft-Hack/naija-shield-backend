using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using naija_shield_backend.Endpoints;
using naija_shield_backend.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SECURE KEY VAULT CONNECTION
// ==========================================
if (builder.Environment.IsProduction())
{
    var keyVaultUri = new Uri("https://rg-naijashield-dev-key.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

// ==========================================
// 2. COSMOS DB CONNECTION
// ==========================================
string cosmosConnString;

if (builder.Environment.IsProduction())
{
    var credential = new DefaultAzureCredential();
    var secretClient = new SecretClient(new Uri("https://rg-naijashield-dev-key.vault.azure.net/"), credential);

    Console.WriteLine("Fetching secrets from Azure Key Vault...");

    string openAiKey = (await secretClient.GetSecretAsync("OpenAI-Key")).Value.Value;
    cosmosConnString = (await secretClient.GetSecretAsync("Cosmos-Connection-String")).Value.Value;
    string searchKey = (await secretClient.GetSecretAsync("Search-Key")).Value.Value;
    string signalrConnString = (await secretClient.GetSecretAsync("SignalR-Connection-String")).Value.Value;
    string acsEmailConnString = (await secretClient.GetSecretAsync("ACS-Email-Connection-String")).Value.Value;
    string acsEmailSender = (await secretClient.GetSecretAsync("ACS-Email-Sender")).Value.Value;

    // Make ACS secrets available via IConfiguration for EmailService
    builder.Configuration["ACS-Email-Connection-String"] = acsEmailConnString;
    builder.Configuration["ACS-Email-Sender"] = acsEmailSender;

    Console.WriteLine("All .NET keys successfully retrieved!");

    // Connect Semantic Kernel to Azure OpenAI
    builder.Services.AddKernel()
        .AddAzureOpenAIChatCompletion(
            deploymentName: "gpt-5.4-mini",
            endpoint: "https://ai-hubnaijashielddev293511702953.openai.azure.com/",
            apiKey: openAiKey
        );
}
else
{
    // Development: read from appsettings.Development.json
    cosmosConnString = builder.Configuration["Cosmos-Connection-String"]
        ?? throw new InvalidOperationException("Cosmos-Connection-String not found in configuration");

    var openAiKey = builder.Configuration["OpenAI-Key"] ?? "";
    if (!string.IsNullOrEmpty(openAiKey))
    {
        builder.Services.AddKernel()
            .AddAzureOpenAIChatCompletion(
                deploymentName: "gpt-5.4-mini",
                endpoint: "https://ai-hubnaijashielddev293511702953.openai.azure.com/",
                apiKey: openAiKey
            );
    }
}

var cosmosClient = new CosmosClient(cosmosConnString);

// ==========================================
// 3. INITIALIZE COSMOS DB CONTAINERS
// ==========================================
Console.WriteLine("Initializing Cosmos DB database and containers...");
var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("NaijaShieldDB");
await database.Database.CreateContainerIfNotExistsAsync("Users", "/id");
await database.Database.CreateContainerIfNotExistsAsync("RefreshTokens", "/userId");
Console.WriteLine("Cosmos DB containers ready.");

// ==========================================
// 4. JWT AUTHENTICATION
// ==========================================
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false; // Keep our JWT claim names as-is
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Return spec-compliant error responses
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var error = new { error = "TOKEN_EXPIRED", message = "Access token has expired" };
            return context.Response.WriteAsJsonAsync(error);
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var error = new { error = "INSUFFICIENT_PERMISSIONS", message = "You do not have permission to access this resource" };
            return context.Response.WriteAsJsonAsync(error);
        }
    };
});

builder.Services.AddAuthorization();

// ==========================================
// 4b. SWAGGER / OPENAPI
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NaijaShield API",
        Version = "v1",
        Description = "Backend API for the NaijaShield cybersecurity platform"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ==========================================
// 5. REGISTER SERVICES (DI)
// ==========================================
builder.Services.AddSingleton(cosmosClient);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<AuthService>();

// ==========================================
// 6. CORS
// ==========================================
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NaijaShield API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 7. MAP ENDPOINTS
// ==========================================
app.MapAuthEndpoints();

// Keep the original test endpoint
app.MapGet("/api/test-scam", async (Kernel kernel) =>
{
    string systemPrompt = @"You are an AI assistant. Please analyze this message and return a JSON object with 'decision' (BLOCK/ALLOW). 
    Message: 'Hello, I would like to check my account balance.'";

    try
    {
        var response = await kernel.InvokePromptAsync(systemPrompt);
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