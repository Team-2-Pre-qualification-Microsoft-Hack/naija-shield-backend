# 🛡️ NaijaShield Backend

> **AI-powered scam detection and voice warning system — built for Nigeria, backed by Azure.**

NaijaShield is a real-time fraud and scam detection platform designed to protect Nigerians from phone scams, fraudulent SMS messages, and social-engineering attacks. The backend is composed of two services working in tandem:

| Service | Stack | Purpose |
|---------|-------|---------|
| **Core API** | ASP.NET Core 9 (C#) | AI-driven scam analysis, data persistence, and frontend communication |
| **Voice Sidecar** | Python (FastAPI) | Speech-to-text transcription, text-to-speech warning generation |

---

## ✨ Key Features

- **AI Scam Analysis** — Uses Azure OpenAI (via Semantic Kernel) to classify incoming calls/messages as `BLOCK` or `ALLOW` in real-time.
- **Voice Warning Generation** — Produces localized audio warnings in Nigerian English using Azure's `en-NG-EzinneNeural` neural voice.
- **Speech-to-Text Transcription** — Transcribes live audio streams via Azure Cognitive Services Speech SDK.
- **Enterprise Authentication System** — Invitation-only user management with role-based access control (SOC Analyst, Compliance Officer, System Admin).
- **Secure Secrets Management** — All API keys and connection strings are fetched from Azure Key Vault at runtime; no secrets are stored in code.
- **Cosmos DB Persistence** — Stores call metadata, scam verdicts, analytics, and user data in Azure Cosmos DB.
- **CORS-Enabled** — Pre-configured for a Next.js frontend on `localhost:3000`.

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────┐
│                     Frontend (Next.js)               │
│                   http://localhost:3000               │
└──────────────────┬───────────────────┬───────────────┘
                   │                   │
            REST / SignalR        REST (audio)
                   │                   │
   ┌───────────────▼──────┐  ┌────────▼──────────────┐
   │   .NET Core API      │  │  Python Voice Sidecar  │
   │   (Port 5000/5001)   │  │  (Port 8000)           │
   │                      │  │                        │
   │  • Semantic Kernel    │  │  • Azure Speech SDK    │
   │  • Azure OpenAI      │  │  • Spitch TTS          │
   │  • Cosmos DB Client   │  │  • FastAPI             │
   └──────────┬───────────┘  └────────┬───────────────┘
              │                       │
              └──────────┬────────────┘
                         │
              ┌──────────▼──────────┐
              │  Azure Key Vault    │
              │  (Shared Secrets)   │
              └──────────┬──────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
   ┌──────────┐  ┌──────────────┐  ┌──────────┐
   │ Azure    │  │  Azure       │  │  Azure   │
   │ OpenAI   │  │  Cosmos DB   │  │  Speech  │
   └──────────┘  └──────────────┘  └──────────┘
```

---

## 🛠️ Tech Stack

### .NET Core API

| Dependency | Version | Purpose |
|------------|---------|---------|
| [Microsoft.SemanticKernel](https://github.com/microsoft/semantic-kernel) | 1.74.0 | Orchestrates AI prompts with Azure OpenAI |
| [Azure.Identity](https://learn.microsoft.com/en-us/dotnet/api/azure.identity) | 1.21.0 | Secure authentication to Azure services |
| [Azure.Security.KeyVault.Secrets](https://learn.microsoft.com/en-us/dotnet/api/azure.security.keyvault.secrets) | 4.10.0 | Fetches secrets from Azure Key Vault |
| [Microsoft.Azure.Cosmos](https://learn.microsoft.com/en-us/azure/cosmos-db/) | 3.59.0 | Cosmos DB NoSQL client |
| [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) | 4.0.3 | Secure password hashing |
| [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/) | 9.0.0 | JWT authentication middleware |
| [System.IdentityModel.Tokens.Jwt](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/) | 8.3.1 | JWT token generation and validation |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | 13.0.4 | JSON serialization |

### Python Voice Sidecar

| Dependency | Purpose |
|------------|---------|
| [FastAPI](https://fastapi.tiangolo.com/) | High-performance web framework for the voice endpoints |
| [uvicorn](https://www.uvicorn.org/) | ASGI server for running FastAPI |
| [azure-cognitiveservices-speech](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/) | Azure Speech SDK for TTS and STT |
| [azure-identity](https://pypi.org/project/azure-identity/) | Azure authentication |
| [azure-keyvault-secrets](https://pypi.org/project/azure-keyvault-secrets/) | Azure Key Vault client |
| [spitch](https://pypi.org/project/spitch/) | Alternative TTS engine (Nigerian voices) |
| [requests](https://pypi.org/project/requests/) | HTTP client for external API calls |

---

## 📋 Prerequisites

Before you begin, make sure you have the following installed and configured:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Python 3.10+](https://www.python.org/downloads/)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) — authenticated with `az login`
- An **Azure subscription** with the following resources provisioned:
  - Azure Key Vault (`rg-naijashield-dev-key`)
  - Azure OpenAI Service with a `gpt-5.4-mini` deployment
  - Azure Cosmos DB account
  - Azure Cognitive Services Speech resource (Sweden Central region)
  - Azure SignalR Service (for real-time communication)

---

## 🔐 Azure Key Vault Secrets

The application expects these secrets to be present in your Key Vault:

| Secret Name | Used By | Description |
|-------------|---------|-------------|
| `OpenAI-Key` | .NET API | API key for Azure OpenAI |
| `Cosmos-Connection-String` | .NET API | Cosmos DB connection string |
| `Search-Key` | .NET API | Azure AI Search key |
| `SignalR-Connection-String` | .NET API | Azure SignalR connection string |
| `JWT-Secret` | .NET API | JWT token signing key (minimum 32 characters) |
| `Spitch-API-Key` | Python Sidecar | API key for Spitch TTS |
| `Azure-Speech-Key` | Python Sidecar | Azure Cognitive Services Speech key |

---

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/Team-2-Pre-qualification-Microsoft-Hack/naija-shield-backend.git
cd naija-shield-backend
```

### 2. Authenticate with Azure

```bash
az login
```

> This is **required** — the application uses `DefaultAzureCredential` to authenticate with Azure Key Vault at startup.

### 3. Start the .NET Core API

```bash
dotnet restore
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

### 4. Start the Python Voice Sidecar

```bash
# Create and activate a virtual environment (recommended)
python -m venv venv

# Windows
venv\Scripts\activate

# macOS / Linux
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Run the sidecar
python main.py
```

The sidecar will start on `http://localhost:8000`.

---

## 📡 API Endpoints

### Authentication Endpoints (.NET Core API)

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `POST` | `/api/auth/login` | User login with email and password | No |
| `POST` | `/api/auth/invite/accept` | Accept invitation and set password | No |
| `POST` | `/api/auth/invite` | Create new user invitation (SYSTEM_ADMIN only) | Yes |
| `POST` | `/api/auth/refresh` | Refresh access token | No |
| `POST` | `/api/auth/logout` | Invalidate refresh token | Yes |

> **📖 See [API_REFERENCE.md](API_REFERENCE.md) for detailed authentication API documentation**

### Core API Endpoints (.NET Core API)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/test-scam` | Sends a test message to Azure OpenAI for scam classification and returns a `BLOCK` / `ALLOW` decision alongside the Cosmos DB connection status. |

### Python Voice Sidecar

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/health-voice` | Health check — confirms the sidecar is running and all voice API keys are loaded. |
| `GET` | `/api/generate-warning` | Generates a localized scam warning audio file (`azure_scam_warning.wav`) using Azure Neural TTS in Nigerian English. |

---

## 📁 Project Structure

```
naija-shield-backend/
├── Controllers/
│   └── AuthController.cs         # Authentication API endpoints
├── Services/
│   ├── AuthService.cs             # Authentication business logic
│   ├── UserService.cs             # User CRUD operations
│   ├── TokenService.cs            # JWT token generation
│   └── EmailService.cs            # Email invitation service
├── Models/
│   ├── User.cs                    # User entity model
│   ├── UserRole.cs                # Role constants
│   ├── UserStatus.cs              # Status constants
│   └── DTOs/
│       └── AuthDTOs.cs            # Request/Response DTOs
├── Middleware/
│   └── RoleAuthorizationMiddleware.cs  # Role-based permissions
├── Utils/
│   └── PasswordHasher.cs          # BCrypt password hashing
├── Scripts/
│   ├── CreateAdminUser.cs         # Admin user creation tool
│   └── CreateInitialAdmin.md      # Admin setup guide
├── Documentation/
│   ├── README_AUTH.md             # Authentication documentation
│   ├── API_REFERENCE.md           # API endpoint reference
│   ├── DEPLOYMENT_CHECKLIST.md    # Deployment guide
│   ├── TESTING_GUIDE.md           # Testing guide
│   ├── QUICK_START.md             # Quick start guide
│   ├── IMPLEMENTATION_SUMMARY.md  # Implementation overview
│   └── FILE_INDEX.md              # File structure index
├── Program.cs                     # .NET Core API entry point
├── naija-shield-backend.csproj    # .NET project file
├── naija-shield-backend.sln       # Visual Studio solution file
├── appsettings.json               # .NET app configuration
├── appsettings.Development.json   # .NET development overrides
├── Properties/
│   └── launchSettings.json        # Local development launch profiles
├── main.py                        # Python FastAPI voice sidecar
├── requirements.txt               # Python dependencies
├── azure_scam_warning.wav         # Generated audio sample
├── .gitignore                     # Git ignore rules
└── README.md                      # This file
```

---

## 🧪 Testing the Setup

After both services are running:

**1. Verify the .NET API is connected to Azure OpenAI:**

```bash
curl http://localhost:5000/api/test-scam
```

Expected response:
```json
{
  "status": "Success",
  "test_database_status": "Cosmos DB Client Initialized",
  "ai_decision": "{ \"decision\": \"ALLOW\" }"
}
```

**2. Test the authentication system:**

```bash
# Login (requires initial admin user setup - see QUICK_START.md)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourPassword123"
  }'
```

Expected response includes `token`, `refreshToken`, and `user` object.

> **📖 For complete authentication setup, see [QUICK_START.md](QUICK_START.md)**

**3. Verify the Python sidecar:**

```bash
curl http://localhost:8000/api/health-voice
```

Expected response:
```json
{
  "status": "Online",
  "message": "Python Sidecar is ready for Jambonz/WebRTC WebSocket streams.",
  "keys_loaded": {
    "spitch": "Loaded (Ready for Text-To-Speech)",
    "azure_speech": "Loaded (Ready for Speech-To-Text)"
  }
}
```

**4. Generate a warning audio file:**

```bash
curl http://localhost:8000/api/generate-warning
```

This will produce `azure_scam_warning.wav` in the project root.

---

## 📚 Documentation

### Authentication System
- **[QUICK_START.md](QUICK_START.md)** - Get authentication running in 10 minutes
- **[README_AUTH.md](README_AUTH.md)** - Complete authentication system documentation
- **[API_REFERENCE.md](API_REFERENCE.md)** - Detailed API endpoint reference
- **[DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)** - Pre-deployment verification
- **[TESTING_GUIDE.md](TESTING_GUIDE.md)** - Comprehensive testing guide
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Implementation overview
- **[FILE_INDEX.md](FILE_INDEX.md)** - Project file structure reference

### User Roles
NaijaShield implements a strict invitation-only authentication system with three roles:

| Role | Access Level | Permissions |
|------|-------------|-------------|
| **SOC Analyst** | `SOC_ANALYST` | View overview, investigate threats |
| **Compliance Officer** | `COMPLIANCE_OFFICER` | View overview, generate compliance reports |
| **System Admin** | `SYSTEM_ADMIN` | Full access, user management, invitations |

### Security Features
- ✅ BCrypt password hashing (cost factor 12)
- ✅ JWT authentication (1-hour access tokens)
- ✅ Refresh token rotation (7-day expiry)
- ✅ Rate limiting (5 failed attempts = 15-min lockout)
- ✅ Role-based authorization
- ✅ Azure Key Vault secret management

---

## 🤝 Contributing

1. **Fork** the repository
2. **Create a feature branch** — `git checkout -b feature/your-feature-name`
3. **Commit your changes** — `git commit -m "Add: description of your change"`
4. **Push to your fork** — `git push origin feature/your-feature-name`
5. **Open a Pull Request** targeting the `main` branch

---

## 📝 License

This project was developed as part of the **Microsoft Pre-qualification Hackathon** by **Team 2**.

---

<p align="center">
  Built with ❤️ for Nigeria 🇳🇬
</p>
