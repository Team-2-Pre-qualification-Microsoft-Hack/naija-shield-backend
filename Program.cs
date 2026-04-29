using System.Text;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using naija_shield_backend.Endpoints;
using naija_shield_backend.Hubs;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. KEY VAULT — PRODUCTION ONLY
// ==========================================
// In Development, secrets come from appsettings.Development.json (git-ignored).
// In Production, they are pulled from Azure Key Vault via DefaultAzureCredential.
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    try
    {
        var keyVaultUri = new Uri("https://rg-naijashield-dev-key.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
        Console.WriteLine("Key Vault loaded successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Key Vault unavailable — falling back to App Settings: {ex.Message}");
    }
}

// ==========================================
// 2. READ SECRETS FROM CONFIGURATION
// ==========================================
string openAiKey        = builder.Configuration["OpenAI-Key"]
    ?? throw new InvalidOperationException("OpenAI-Key is not configured");
string cosmosConnString = builder.Configuration["Cosmos-Connection-String"]
    ?? throw new InvalidOperationException("Cosmos-Connection-String is not configured");
string signalrConnString = builder.Configuration["SignalR-Connection-String"]
    ?? throw new InvalidOperationException("SignalR-Connection-String is not configured");

// ==========================================
// 3. CONTROLLERS + SIGNALR
// ==========================================
builder.Services.AddControllers();
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
// 5. COSMOS DB — SINGLETON CLIENT + CONTAINER INIT
// ==========================================
var cosmosClient = new CosmosClient(cosmosConnString);
builder.Services.AddSingleton(cosmosClient);

Console.WriteLine("Initializing Cosmos DB containers...");
var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("NaijaShieldDB");
await database.Database.CreateContainerIfNotExistsAsync("Users", "/id");
await database.Database.CreateContainerIfNotExistsAsync("RefreshTokens", "/userId");
await database.Database.CreateContainerIfNotExistsAsync("SmsEvents", "/id");
Console.WriteLine("Cosmos DB containers ready.");

// ==========================================
// 6. JWT AUTHENTICATION
// ==========================================
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret  = jwtSection["Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSection["Issuer"],
        ValidateAudience         = true,
        ValidAudience            = jwtSection["Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode  = 401;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(
                new { error = "TOKEN_EXPIRED", message = "Access token has expired" });
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode  = 403;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(
                new { error = "INSUFFICIENT_PERMISSIONS", message = "You do not have permission to access this resource" });
        }
    };
});

builder.Services.AddAuthorization();

// ==========================================
// 7. HTTP CLIENTS
// ==========================================
var sidecarBaseUrl = builder.Configuration["AiSidecar:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddHttpClient("AiSidecar", client =>
{
    client.BaseAddress = new Uri(sidecarBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<AfricasTalkingService>();

// ==========================================
// 8. DOMAIN SERVICES (DI)
// ==========================================
// Threat pipeline
builder.Services.AddScoped<IPiiRedactionService,  PlaceholderPiiRedactionService>();
builder.Services.AddScoped<IThreatScoringService, ThreatScoringService>();
builder.Services.AddScoped<IIncidentRepository,   CosmosIncidentRepository>();
builder.Services.AddScoped<IAlertService,         LoggingAlertService>();

// Location lookup
builder.Services.AddSingleton<PhoneLocationService>();

// Auth pipeline
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<AuthService>();

// ==========================================
// 9. SWAGGER
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NaijaShield API", Version = "v1",
        Description = "Backend API for the NaijaShield cybersecurity platform" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
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
// 10. CORS
// ==========================================
// Dev uses AllowedOrigins array (localhost:3000 + localhost:5173).
// Prod falls back to Cors:FrontendOrigin (set the deployed URL there).
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(
                "https://naija-shield.vercel.app",
                "http://localhost:3000",
                "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ==========================================
// 11. BUILD + CONFIGURE PIPELINE
// ==========================================
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NaijaShield API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ThreatHub>("/hubs/threat");
app.MapAuthEndpoints();

// Smoke-test endpoint (retained from initial setup)
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
            status               = "Success",
            test_database_status = "Cosmos DB Client Initialized",
            ai_decision          = response.ToString()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ==========================================
// 7. DEVELOPMENT-ONLY: CREATE INITIAL ADMIN
// ==========================================
if (!app.Environment.IsProduction())
{
    app.MapPost("/api/dev/create-admin", async (IUserService userService, CosmosClient cosmosClient, IConfiguration configuration) =>
    {
        try
        {
            var databaseName = configuration["Cosmos:DatabaseName"] ?? "NaijaShieldDB";
            var containerName = configuration["Cosmos:UserContainerName"] ?? "Users";
            var container = cosmosClient.GetContainer(databaseName, containerName);

            // Try to delete existing admin if it exists
            try
            {
                await container.DeleteItemAsync<naija_shield_backend.Models.User>("USR-001", new PartitionKey("USR-001"));
                Console.WriteLine("Deleted existing admin user");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // User doesn't exist - that's fine
            }

            // Create fresh admin user with correct password hash
            var adminUser = new naija_shield_backend.Models.User
            {
                Id = "USR-001",
                Name = "Admin User",
                Email = "admin@naijashield.com",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                Role = naija_shield_backend.Models.UserRole.SYSTEM_ADMIN,
                Organisation = "NaijaShield",
                Status = naija_shield_backend.Models.UserStatus.Active,
                LastActive = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                FailedLoginAttempts = 0
            };

            await userService.CreateUserAsync(adminUser);

            return Results.Ok(new
            {
                message = "Admin user created successfully!",
                email = "admin@naijashield.com",
                password = "admin123",
                role = "SYSTEM_ADMIN",
                note = "Password is freshly hashed with BCrypt"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error: {ex.Message}");
        }
    });
}

// ==========================================
// 12. SEED DEFAULT ADMIN
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.SeedDefaultAdminAsync();
}

app.Run();
