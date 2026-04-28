# How to Update appsettings.Development.json with Team Secrets

## Step 1: Receive Secrets from Team

Your teammate should send you something like:

```
OpenAI Key: sk-proj-abc123def456...
Cosmos DB: AccountEndpoint=https://naijashield-cosmos.documents.azure.com:443/;AccountKey=xyz789...
Search Key: ABC123DEF456...
SignalR: Endpoint=https://naijashield-signalr.service.signalr.net;AccessKey=qrs789...
```

## Step 2: Update Your Local Config

Open `appsettings.Development.json` and replace the placeholders:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "sk-proj-abc123def456...",  // ? Paste here
    "Cosmos-Connection-String": "AccountEndpoint=https://naijashield-cosmos.documents.azure.com:443/;AccountKey=xyz789...",  // ? Paste here
    "Search-Key": "ABC123DEF456...",  // ? Paste here
    "SignalR-Connection-String": "Endpoint=https://naijashield-signalr.service.signalr.net;AccessKey=qrs789...",  // ? Paste here
    "JWT-Secret": "hackathon-dev-jwt-secret-at-least-32-characters-long-for-local-development",  // ? This is fine as-is
    "Email-Connection-String": ""  // ? Leave empty
  },
  "Frontend": {
    "BaseUrl": "http://localhost:3000"
  },
  "Email": {
    "SenderAddress": "DoNotReply@naijashield.com"
  }
}
```

## Step 3: Save and Run

1. Save the file (Ctrl+S)
2. Run the application:
   ```sh
   dotnet run
   ```

3. You should see:
   ```
   ??  Using local configuration (Development mode)
   ??  DO NOT use this mode in production!
   Local configuration loaded successfully.
   Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
   ```

## ? Success!

Your app should now be running at:
- HTTPS: https://localhost:7000
- HTTP: http://localhost:5000

## ?? Security Reminder

- ? `appsettings.Development.json` is already in `.gitignore`
- ? Your secrets will NOT be committed to Git
- ? **DO NOT** push this file to GitHub
- ? **DO NOT** screenshot it and share publicly

## Verify .gitignore

Quick check - run this command:

```sh
git status
```

You should **NOT** see `appsettings.Development.json` in the list of changed files.

If you do see it, run:
```sh
git restore appsettings.Development.json
```

---

Happy coding! ??
