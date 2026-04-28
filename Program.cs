using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.SemanticKernel;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using naija_shield_backend.Services;
using naija_shield_backend.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SECURE KEY VAULT CONNECTION
// ==========================================
string openAiKey;
string cosmosConnString;
string searchKey;
string signalrConnString;
string jwtSecret;
string? emailConnectionString = null;

if (builder.Environment.IsProduction())
{
    var keyVaultUri = new Uri("https://kv-naijashield-dev.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    
    Console.WriteLine("Fetching secrets from Azure Key Vault...");
    
    // ==========================================
    // 2. FETCH ALL KEYS FROM KEY VAULT
    // ==========================================
    openAiKey = builder.Configuration["OpenAI-Key"] ?? throw new InvalidOperationException("OpenAI-Key not found");
    cosmosConnString = builder.Configuration["Cosmos-Connection-String"] ?? throw new InvalidOperationException("Cosmos-Connection-String not found");
    searchKey = builder.Configuration["Search-Key"] ?? throw new InvalidOperationException("Search-Key not found");
    signalrConnString = builder.Configuration["SignalR-Connection-String"] ?? throw new InvalidOperationException("SignalR-Connection-String not found");
    jwtSecret = builder.Configuration["JWT-Secret"] ?? throw new InvalidOperationException("JWT-Secret not found");
    emailConnectionString = builder.Configuration["Email-Connection-String"];
    
    Console.WriteLine("All secrets successfully retrieved from Key Vault!");
}
else
{
    // ==========================================
    // 2. LOAD FROM LOCAL CONFIGURATION (Development)
    // ==========================================
    Console.WriteLine("Using local configuration (Development mode)");
    
    openAiKey = builder.Configuration["Secrets:OpenAI-Key"] ?? throw new InvalidOperationException("Secrets:OpenAI-Key not configured");
    cosmosConnString = builder.Configuration["Secrets:Cosmos-Connection-String"] ?? throw new InvalidOperationException("Secrets:Cosmos-Connection-String not configured");
    searchKey = builder.Configuration["Secrets:Search-Key"] ?? throw new InvalidOperationException("Secrets:Search-Key not configured");
    signalrConnString = builder.Configuration["Secrets:SignalR-Connection-String"] ?? throw new InvalidOperationException("Secrets:SignalR-Connection-String not configured");
    jwtSecret = builder.Configuration["Secrets:JWT-Secret"] ?? throw new InvalidOperationException("Secrets:JWT-Secret not configured");
    emailConnectionString = builder.Configuration["Secrets:Email-Connection-String"];
}

// ==========================================
// 3. INITIALIZE COSMOS DB
// ==========================================
var cosmosClient = new CosmosClient(cosmosConnString);

// Ensure database and container exist for authentication
var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "NaijaShieldDB";
var userContainerName = builder.Configuration["Cosmos:UserContainerName"] ?? "Users";

try
{
    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
    await database.Database.CreateContainerIfNotExistsAsync(userContainerName, "/id");
    Console.WriteLine($"Cosmos DB '{databaseName}' and container '{userContainerName}' ready!");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not initialize Cosmos DB: {ex.Message}");
}

// ==========================================
// 4. CONFIGURE SERVICES
// ==========================================
builder.Services.AddControllers();

builder.Services.AddSingleton(cosmosClient);

// Authentication services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Configuration["Jwt:Secret"] = jwtSecret;

if (!string.IsNullOrEmpty(emailConnectionString))
{
    builder.Configuration["Email:ConnectionString"] = emailConnectionString;
}

// JWT Authentication
var key = Encoding.UTF8.GetBytes(jwtSecret);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://naijashield.com", "http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Connect Semantic Kernel to Azure OpenAI
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: "gpt-5.4-mini",
        endpoint: "https://ai-hubnaijashielddev293511702953.openai.azure.com/", 
        apiKey: openAiKey
    );

var app = builder.Build();

// ==========================================
// 5. CONFIGURE MIDDLEWARE PIPELINE
// ==========================================
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRoleAuthorization();
app.MapControllers();

// ==========================================
// 6. TEST ENDPOINT
// ==========================================
app.MapGet("/api/test-scam", async (Kernel kernel) =>
{
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

app.Run();
