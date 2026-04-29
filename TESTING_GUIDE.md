# NaijaShield Authentication Testing Guide

This guide provides step-by-step instructions for testing all authentication features.

## Prerequisites

1. Backend server running (Development: `https://localhost:7000`)
2. Initial SYSTEM_ADMIN user created (see `Scripts/CreateInitialAdmin.md`)
3. REST client installed (cURL, Postman, or REST Client VS Code extension)

---

## Test Suite

### 1. Login Tests

#### ? Test 1.1: Valid Login
**Endpoint:** `POST /api/auth/login`

**Request:**
```json
{
  "email": "admin@yourdomain.com",
  "password": "YourPassword123"
}
```

**Expected Result:**
- Status: 200 OK
- Response includes: `token`, `refreshToken`, `user` object
- User object has: `id`, `name`, `email`, `role`, `organisation`

**Validation:**
```bash
# Copy the token from response
TOKEN="paste_token_here"

# Decode JWT to verify claims (use jwt.io or jwt-cli)
jwt decode $TOKEN

# Should contain:
# - sub: USR-001
# - email: admin@yourdomain.com
# - role: SYSTEM_ADMIN
```

---

#### ? Test 1.2: Invalid Email
**Request:**
```json
{
  "email": "nonexistent@example.com",
  "password": "anypassword"
}
```

**Expected Result:**
- Status: 401 Unauthorized
- Error code: `INVALID_CREDENTIALS`

---

#### ? Test 1.3: Invalid Password
**Request:**
```json
{
  "email": "admin@yourdomain.com",
  "password": "wrongpassword"
}
```

**Expected Result:**
- Status: 401 Unauthorized
- Error code: `INVALID_CREDENTIALS`

---

#### ? Test 1.4: Rate Limiting (5 Failed Attempts)
**Steps:**
1. Make 5 login attempts with wrong password
2. Make 6th attempt

**Expected Result:**
- First 5 attempts: 401 Unauthorized
- 6th attempt: 429 Too Many Requests
- Error code: `RATE_LIMIT_EXCEEDED`
- Message mentions "15 minutes"

**Validation:**
Wait 15 minutes and retry login - should succeed.

---

#### ? Test 1.5: Missing Fields
**Request:**
```json
{
  "email": "admin@yourdomain.com"
}
```

**Expected Result:**
- Status: 400 Bad Request
- Error code: `INVALID_REQUEST`

---

### 2. Invitation Tests

#### ? Test 2.1: Create Invitation (as SYSTEM_ADMIN)
**Endpoint:** `POST /api/auth/invite`

**Headers:**
```
Authorization: Bearer {admin_token}
Content-Type: application/json
```

**Request:**
```json
{
  "email": "analyst@mtn.ng",
  "name": "Test Analyst",
  "role": "SOC_ANALYST"
}
```

**Expected Result:**
- Status: 201 Created
- Response includes: `inviteId`, `email`, `name`, `role`, `expiresAt`, `status`
- Check console logs for invitation email details
- Copy the `inviteToken` from logs for Test 3.1

---

#### ? Test 2.2: Create Invitation (as non-admin)
**Steps:**
1. Login as SOC_ANALYST or COMPLIANCE_OFFICER
2. Try to create invitation

**Expected Result:**
- Status: 403 Forbidden
- Error code: `INSUFFICIENT_PERMISSIONS`

---

#### ? Test 2.3: Create Invitation (duplicate email)
**Request:**
```json
{
  "email": "admin@yourdomain.com",
  "name": "Duplicate User",
  "role": "SOC_ANALYST"
}
```

**Expected Result:**
- Status: 409 Conflict
- Error code: `USER_ALREADY_EXISTS`

---

#### ? Test 2.4: Create Invitation (invalid role)
**Request:**
```json
{
  "email": "test@example.com",
  "name": "Test User",
  "role": "INVALID_ROLE"
}
```

**Expected Result:**
- Status: 400 Bad Request
- Error code: `INVALID_ROLE`

---

### 3. Accept Invitation Tests

#### ? Test 3.1: Accept Valid Invitation
**Endpoint:** `POST /api/auth/invite/accept`

**Request:**
```json
{
  "inviteToken": "{token_from_test_2.1}",
  "password": "NewPassword123!",
  "confirmPassword": "NewPassword123!"
}
```

**Expected Result:**
- Status: 200 OK
- Response includes: `token`, `refreshToken`, `user`
- User has `role: "SOC_ANALYST"`
- User status changes from "Pending" to "Active"

**Validation:**
Login with the new credentials to verify account is active.

---

#### ? Test 3.2: Accept with Mismatched Passwords
**Request:**
```json
{
  "inviteToken": "{valid_token}",
  "password": "Password123!",
  "confirmPassword": "DifferentPassword123!"
}
```

**Expected Result:**
- Status: 400 Bad Request
- Error code: `PASSWORDS_DO_NOT_MATCH`

---

#### ? Test 3.3: Accept with Invalid Token
**Request:**
```json
{
  "inviteToken": "invalid-token-xyz",
  "password": "Password123!",
  "confirmPassword": "Password123!"
}
```

**Expected Result:**
- Status: 400 Bad Request
- Error code: `INVALID_INVITE`

---

#### ? Test 3.4: Accept Expired Invitation
**Steps:**
1. Create invitation
2. Manually update `inviteExpiry` in Cosmos DB to past date
3. Try to accept

**Expected Result:**
- Status: 400 Bad Request
- Error code: `INVALID_INVITE`
- Message mentions "expired"

---

### 4. Token Refresh Tests

#### ? Test 4.1: Valid Token Refresh
**Endpoint:** `POST /api/auth/refresh`

**Request:**
```json
{
  "refreshToken": "{refresh_token_from_login}"
}
```

**Expected Result:**
- Status: 200 OK
- New `token` and `refreshToken` returned
- Old refresh token is invalidated

**Validation:**
Try using old refresh token again - should fail with 401.

---

#### ? Test 4.2: Invalid Refresh Token
**Request:**
```json
{
  "refreshToken": "invalid-refresh-token"
}
```

**Expected Result:**
- Status: 401 Unauthorized
- Error code: `INVALID_REFRESH_TOKEN`

---

#### ? Test 4.3: Expired Refresh Token
**Steps:**
1. Login and get refresh token
2. Manually update `refreshTokenExpiry` in Cosmos DB to past date
3. Try to refresh

**Expected Result:**
- Status: 401 Unauthorized
- Error code: `INVALID_REFRESH_TOKEN`

---

### 5. Logout Tests

#### ? Test 5.1: Valid Logout
**Endpoint:** `POST /api/auth/logout`

**Headers:**
```
Authorization: Bearer {access_token}
```

**Expected Result:**
- Status: 200 OK
- Message: "Logged out successfully"
- Refresh token invalidated in database

**Validation:**
Try to use the refresh token - should fail with 401.

---

#### ? Test 5.2: Logout Without Token
**Headers:**
```
(No Authorization header)
```

**Expected Result:**
- Status: 401 Unauthorized

---

### 6. Role-Based Access Tests

#### Setup: Create Test Users
Create one user of each role:
1. **SOC_ANALYST**: `analyst@test.com`
2. **COMPLIANCE_OFFICER**: `compliance@test.com`
3. **SYSTEM_ADMIN**: `admin@test.com` (already exists)

---

#### ? Test 6.1: SOC_ANALYST Access
**Login as:** `analyst@test.com`

**Should succeed (200 OK):**
- GET `/api/overview`
- GET `/api/threat-feed`

**Should fail (403 Forbidden):**
- GET `/api/compliance`
- GET `/api/user-management`
- GET `/api/settings`
- POST `/api/auth/invite`

---

#### ? Test 6.2: COMPLIANCE_OFFICER Access
**Login as:** `compliance@test.com`

**Should succeed (200 OK):**
- GET `/api/overview`
- GET `/api/compliance`

**Should fail (403 Forbidden):**
- GET `/api/threat-feed`
- GET `/api/user-management`
- GET `/api/settings`
- POST `/api/auth/invite`

---

#### ? Test 6.3: SYSTEM_ADMIN Access
**Login as:** `admin@test.com`

**Should succeed (200 OK):**
- GET `/api/overview`
- GET `/api/threat-feed`
- GET `/api/compliance`
- GET `/api/user-management`
- GET `/api/settings`
- POST `/api/auth/invite`

---

### 7. Security Tests

#### ? Test 7.1: Password Hashing
**Validation:**
1. Query Cosmos DB for any user
2. Check `password` field
3. Should start with `$2a$12$` (BCrypt with cost 12)
4. Should NOT be plain text

---

#### ? Test 7.2: Token Expiry
**Steps:**
1. Login and get access token
2. Wait 1 hour (or manually change JWT expiry for testing)
3. Try to access protected endpoint

**Expected Result:**
- Status: 401 Unauthorized
- Header: `Token-Expired: true`

---

#### ? Test 7.3: No Secrets in Logs
**Validation:**
1. Check application logs
2. Verify NO passwords, tokens, or PII are logged
3. Error messages should not expose sensitive data

---

#### ? Test 7.4: HTTPS Enforcement
**Steps:**
1. Try to access API via HTTP (not HTTPS)

**Expected Result:**
- Request should be rejected or redirected to HTTPS

---

### 8. Edge Cases

#### ? Test 8.1: Malformed JWT
**Steps:**
1. Create invalid JWT: `Bearer invalid.jwt.token`
2. Try to access protected endpoint

**Expected Result:**
- Status: 401 Unauthorized

---

#### ? Test 8.2: JWT with Wrong Secret
**Steps:**
1. Create JWT signed with different secret
2. Try to access protected endpoint

**Expected Result:**
- Status: 401 Unauthorized

---

#### ? Test 8.3: Tampered JWT Claims
**Steps:**
1. Get valid JWT
2. Modify claims (e.g., change role to SYSTEM_ADMIN)
3. Try to access protected endpoint

**Expected Result:**
- Status: 401 Unauthorized (signature validation fails)

---

## Automated Testing Script

### Postman Collection
Import this collection into Postman:

```json
{
  "info": {
    "name": "NaijaShield Auth Tests",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "1. Login - Valid",
      "request": {
        "method": "POST",
        "header": [],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"email\": \"{{admin_email}}\",\n  \"password\": \"{{admin_password}}\"\n}",
          "options": { "raw": { "language": "json" } }
        },
        "url": { "raw": "{{base_url}}/api/auth/login" }
      }
    }
  ],
  "variable": [
    {
      "key": "base_url",
      "value": "https://localhost:7000"
    },
    {
      "key": "admin_email",
      "value": "admin@yourdomain.com"
    },
    {
      "key": "admin_password",
      "value": "your_password"
    }
  ]
}
```

---

## Testing Checklist

Use this checklist to track your testing progress:

- [ ] 1.1: Valid login works
- [ ] 1.2: Invalid email rejected
- [ ] 1.3: Invalid password rejected
- [ ] 1.4: Rate limiting after 5 attempts
- [ ] 1.5: Missing fields rejected
- [ ] 2.1: Admin can create invitation
- [ ] 2.2: Non-admin cannot create invitation
- [ ] 2.3: Duplicate email rejected
- [ ] 2.4: Invalid role rejected
- [ ] 3.1: Can accept valid invitation
- [ ] 3.2: Mismatched passwords rejected
- [ ] 3.3: Invalid token rejected
- [ ] 3.4: Expired invitation rejected
- [ ] 4.1: Token refresh works
- [ ] 4.2: Invalid refresh token rejected
- [ ] 4.3: Expired refresh token rejected
- [ ] 5.1: Logout invalidates refresh token
- [ ] 5.2: Logout without token rejected
- [ ] 6.1: SOC_ANALYST permissions correct
- [ ] 6.2: COMPLIANCE_OFFICER permissions correct
- [ ] 6.3: SYSTEM_ADMIN permissions correct
- [ ] 7.1: Passwords properly hashed
- [ ] 7.2: Token expiry enforced
- [ ] 7.3: No secrets in logs
- [ ] 7.4: HTTPS enforced
- [ ] 8.1: Malformed JWT rejected
- [ ] 8.2: Wrong secret rejected
- [ ] 8.3: Tampered claims rejected

---

## Troubleshooting

### Issue: "JWT Secret not configured"
**Solution:** Ensure JWT-Secret exists in Azure Key Vault

### Issue: All logins return 500 error
**Solution:** Check Cosmos DB connection string and container setup

### Issue: Tokens expire immediately
**Solution:** Check server time synchronization (NTP)

### Issue: CORS errors
**Solution:** Verify frontend URL in CORS policy in `Program.cs`

### Issue: Rate limiting not working
**Solution:** Verify `failedLoginAttempts` and `lockoutUntil` fields in Cosmos DB

---

**Version:** 1.0  
**Last Updated:** April 2025
