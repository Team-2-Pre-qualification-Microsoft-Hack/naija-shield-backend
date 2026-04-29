# Create Initial System Admin User

Since NaijaShield is invitation-only, you need to manually create the first SYSTEM_ADMIN user in Cosmos DB.

## Step 1: Generate Password Hash

Run the password hasher utility:

```bash
cd Utils
dotnet run --project ../naija-shield-backend.csproj PasswordHasher.cs
```

Or use the class directly in code:
```csharp
using naija_shield_backend.Utils;

var password = "YourSecurePassword123!";
var hash = PasswordHasher.HashPassword(password);
Console.WriteLine($"Hash: {hash}");
```

## Step 2: Create User Document in Cosmos DB

### Option A: Using Azure Portal

1. Open Azure Portal ? Cosmos DB ? NaijaShieldDB ? Users container
2. Click "New Item"
3. Paste the following JSON (replace the password hash and email):

```json
{
  "id": "USR-001",
  "name": "System Administrator",
  "email": "admin@yourdomain.com",
  "password": "$2a$12$YOUR_HASHED_PASSWORD_HERE",
  "role": "SYSTEM_ADMIN",
  "organisation": "NaijaShield",
  "status": "Active",
  "inviteToken": null,
  "inviteExpiry": null,
  "lastActive": "2025-04-27T10:00:00Z",
  "createdAt": "2025-04-27T10:00:00Z",
  "refreshToken": null,
  "refreshTokenExpiry": null,
  "failedLoginAttempts": 0,
  "lockoutUntil": null,
  "type": "user"
}
```

### Option B: Using Azure CLI

```bash
# Set your variables
COSMOS_ACCOUNT="your-cosmos-account-name"
DATABASE_NAME="NaijaShieldDB"
CONTAINER_NAME="Users"
RESOURCE_GROUP="your-resource-group"

# Create the document
az cosmosdb sql container create-update \
  --account-name $COSMOS_ACCOUNT \
  --database-name $DATABASE_NAME \
  --name $CONTAINER_NAME \
  --resource-group $RESOURCE_GROUP \
  --partition-key-path "/type"

# Insert document (requires Azure Data Explorer or SDK)
```

### Option C: Using C# SDK (Recommended for Testing)

Create a console app to insert the admin:

```csharp
using Microsoft.Azure.Cosmos;
using naija_shield_backend.Models;
using naija_shield_backend.Utils;

var cosmosConnectionString = "YOUR_COSMOS_CONNECTION_STRING";
var cosmosClient = new CosmosClient(cosmosConnectionString);

var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("NaijaShieldDB");
var container = await database.Database.CreateContainerIfNotExistsAsync("Users", "/type");

var adminUser = new User
{
    Id = "USR-001",
    Name = "System Administrator",
    Email = "admin@yourdomain.com",
    Password = PasswordHasher.HashPassword("YourSecurePassword123!"),
    Role = UserRole.SYSTEM_ADMIN,
    Organisation = "NaijaShield",
    Status = UserStatus.Active,
    InviteToken = null,
    InviteExpiry = null,
    LastActive = DateTime.UtcNow,
    CreatedAt = DateTime.UtcNow,
    RefreshToken = null,
    RefreshTokenExpiry = null,
    FailedLoginAttempts = 0,
    LockoutUntil = null,
    Type = "user"
};

await container.Container.CreateItemAsync(adminUser, new PartitionKey("user"));
Console.WriteLine("Admin user created successfully!");
```

## Step 3: Test Login

```bash
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourSecurePassword123!"
  }'
```

Expected response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "base64_refresh_token...",
  "user": {
    "id": "USR-001",
    "name": "System Administrator",
    "email": "admin@yourdomain.com",
    "role": "SYSTEM_ADMIN",
    "organisation": "NaijaShield"
  }
}
```

## Step 4: Invite Additional Users

Once logged in as admin, you can invite other users:

```bash
curl -X POST https://localhost:7000/api/auth/invite \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "email": "analyst@mtn.ng",
    "name": "New Analyst",
    "role": "SOC_ANALYST"
  }'
```

## Security Notes

1. **Never commit the initial admin password to version control**
2. **Use a strong password** (minimum 12 characters, mix of upper, lower, numbers, special chars)
3. **Store the admin credentials securely** (use a password manager)
4. **Change the default password** after first login (implement password change endpoint if needed)
5. **Delete any test/temporary admin users** in production
