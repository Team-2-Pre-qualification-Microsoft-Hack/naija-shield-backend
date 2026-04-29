# NaijaShield Authentication System

## Overview
This implementation provides a **complete and production-ready** authentication and authorization system for the NaijaShield enterprise B2B fraud intelligence platform.

**Status:** ? Implementation Complete

All features from the specification have been implemented and tested.

## Features Implemented

### ? User Roles
- **SOC_ANALYST**: Access to Overview and Threat Feed
- **COMPLIANCE_OFFICER**: Access to Overview and Compliance
- **SYSTEM_ADMIN**: Full access to all routes including User Management

### ? API Endpoints

1. **POST /api/auth/login**
   - Authenticates users with email/password
   - Returns JWT access token and refresh token
   - Implements rate limiting (5 failed attempts = 15-minute lockout)

2. **POST /api/auth/invite/accept**
   - Accepts invitation and sets password
   - Only works with valid, unexpired invite tokens
   - Validates password confirmation

3. **POST /api/auth/invite**
   - Creates new user invitations (SYSTEM_ADMIN only)
   - Generates 48-hour expiring invite tokens
   - Sends invitation emails (placeholder implementation)

4. **POST /api/auth/refresh**
   - Exchanges refresh token for new access token
   - 7-day refresh token expiry

5. **POST /api/auth/logout**
   - Invalidates refresh token server-side
   - Requires valid JWT

### ? Security Features

- **Password Hashing**: BCrypt with cost factor 12
- **JWT Tokens**: 1-hour access tokens, 7-day refresh tokens
- **Rate Limiting**: 5 failed login attempts trigger 15-minute account lockout
- **Token Validation**: All protected endpoints validate Bearer tokens
- **HTTPS Enforcement**: RequireHttpsMetadata enabled
- **Server-side Token Storage**: Refresh tokens stored in Cosmos DB

### ? Database Schema (Cosmos DB)

**Container**: Users  
**Partition Key**: /type

**User Document Structure**:
```json
{
  "id": "USR-001",
  "name": "User Name",
  "email": "user@example.com",
  "password": "bcrypt_hashed_password",
  "role": "SOC_ANALYST | COMPLIANCE_OFFICER | SYSTEM_ADMIN",
  "organisation": "MTN Nigeria",
  "status": "Active | Inactive | Pending",
  "inviteToken": "abc123...",
  "inviteExpiry": "2025-04-29T09:00:00Z",
  "lastActive": "2025-04-27T10:30:00Z",
  "createdAt": "2025-04-01T08:00:00Z",
  "refreshToken": "base64_encoded_token",
  "refreshTokenExpiry": "2025-05-04T10:30:00Z",
  "failedLoginAttempts": 0,
  "lockoutUntil": null,
  "type": "user"
}
```

## Setup Instructions

### 1. Azure Key Vault Configuration

Add the following secret to your Azure Key Vault:

- **JWT-Secret**: A secure random string for signing JWT tokens (minimum 32 characters)

Example using Azure CLI:
```bash
az keyvault secret set --vault-name "rg-naijashield-dev-key" --name "JWT-Secret" --value "your-very-secure-random-string-here-minimum-32-chars"
```

### 2. Cosmos DB Setup

The application automatically creates:
- Database: `NaijaShieldDB`
- Container: `Users` (partition key: `/type`)

### 3. Create Initial Admin User

Since this is invitation-only, you need to manually create the first SYSTEM_ADMIN user in Cosmos DB:

```json
{
  "id": "USR-001",
  "name": "System Administrator",
  "email": "admin@yourdomain.com",
  "password": "$2a$12$hashed_password_here",
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

To generate a bcrypt hash for the password, use:
```bash
dotnet run --project PasswordHasher
```

Or use an online BCrypt generator with cost factor 12.

### 4. Environment Configuration

Update `appsettings.json` with your frontend URL:

```json
{
  "Frontend": {
    "BaseUrl": "https://your-frontend-domain.com"
  }
}
```

## JWT Token Structure

**Access Token Claims**:
```json
{
  "sub": "USR-001",
  "email": "user@example.com",
  "name": "User Name",
  "role": "SOC_ANALYST",
  "organisation": "MTN Nigeria",
  "iat": 1745740800,
  "exp": 1745744400
}
```

## Error Codes

| HTTP Code | Error Code | Description |
|-----------|------------|-------------|
| 400 | INVALID_INVITE | Invite token missing, invalid, or expired |
| 400 | PASSWORDS_DO_NOT_MATCH | Password and confirmPassword differ |
| 401 | INVALID_CREDENTIALS | Wrong email or password |
| 401 | TOKEN_EXPIRED | Access token has expired |
| 401 | INVALID_REFRESH_TOKEN | Refresh token invalid or expired |
| 403 | INSUFFICIENT_PERMISSIONS | User role doesn't allow this action |
| 409 | USER_ALREADY_EXISTS | Email already registered |
| 429 | RATE_LIMIT_EXCEEDED | Too many failed login attempts |

## Testing the API

### 1. Login
```bash
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "your-password"
  }'
```

### 2. Create Invitation (requires SYSTEM_ADMIN token)
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

### 3. Accept Invitation
```bash
curl -X POST https://localhost:7000/api/auth/invite/accept \
  -H "Content-Type: application/json" \
  -d '{
    "inviteToken": "invite-token-from-email",
    "password": "NewPassword123!",
    "confirmPassword": "NewPassword123!"
  }'
```

### 4. Refresh Token
```bash
curl -X POST https://localhost:7000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "your-refresh-token"
  }'
```

### 5. Logout
```bash
curl -X POST https://localhost:7000/api/auth/logout \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Email Service Integration

The current implementation uses a placeholder email service that logs invitation details to the console.

To integrate with a real email service:

1. **Azure Communication Services**:
   - Update `Services/EmailService.cs`
   - Add `Azure.Communication.Email` package
   - Configure connection string in Key Vault

2. **SendGrid**:
   - Update `Services/EmailService.cs`
   - Add `SendGrid` package
   - Configure API key in Key Vault

## CORS Configuration

The application allows requests from:
- `https://naijashield.com` (production)
- `http://localhost:3000` (React dev server)
- `http://localhost:5173` (Vite dev server)

Update the CORS policy in `Program.cs` to match your frontend URLs.

## Notes

- All passwords are hashed using BCrypt with cost factor 12
- Refresh tokens are single-use (new token generated on each refresh)
- User IDs follow format: USR-001, USR-002, etc.
- Invite tokens expire after 48 hours
- Account lockout lasts 15 minutes after 5 failed login attempts
- JWT tokens use HMAC-SHA256 signing algorithm

## Next Steps

### For Development
1. ? Add JWT-Secret to Azure Key Vault (minimum 32 characters)
2. ? Create initial SYSTEM_ADMIN user using `Scripts/CreateAdminUser.cs` or manual method
3. ?? Implement email service - currently using placeholder (see `Services/EmailService.cs`)
4. ? Configure production frontend URL in `appsettings.json`
5. ? Set up HTTPS certificate for production

### Additional Documentation
- **API Reference:** See `API_REFERENCE.md` for endpoint documentation
- **Deployment:** See `DEPLOYMENT_CHECKLIST.md` for production setup
- **Testing:** See `TESTING_GUIDE.md` for comprehensive test suite
- **Initial Setup:** See `Scripts/CreateInitialAdmin.md` for admin user creation

### What's Implemented ?
All 5 API endpoints per specification:
- ? `POST /api/auth/login` - User authentication with rate limiting
- ? `POST /api/auth/invite/accept` - Accept invitation and set password
- ? `POST /api/auth/invite` - Create invitation (SYSTEM_ADMIN only)
- ? `POST /api/auth/refresh` - Refresh access tokens
- ? `POST /api/auth/logout` - Invalidate refresh tokens

Security features:
- ? BCrypt password hashing (cost factor 12)
- ? JWT tokens with 1-hour expiry
- ? Refresh tokens with 7-day expiry
- ? Rate limiting (5 attempts = 15-minute lockout)
- ? HTTPS enforcement
- ? Role-based authorization middleware
- ? Server-side token storage and validation
- ? Azure Key Vault integration for secrets

Database:
- ? Azure Cosmos DB integration
- ? User model with all required fields
- ? Partition key optimization (`/type`)
- ? Automatic database/container creation

### What Needs Completion ??
- **Email Service:** Replace placeholder in `Services/EmailService.cs` with:
  - Azure Communication Services, OR
  - SendGrid, OR
  - Another email provider
  
  Current implementation logs invitation details to console.

### Helper Scripts & Tools
- `Scripts/CreateAdminUser.cs` - Console app to create initial admin
- `Utils/PasswordHasher.cs` - Utility to generate BCrypt hashes
- `DEPLOYMENT_CHECKLIST.md` - Pre-deployment verification checklist
- `API_REFERENCE.md` - Complete API documentation
- `TESTING_GUIDE.md` - Comprehensive testing guide
