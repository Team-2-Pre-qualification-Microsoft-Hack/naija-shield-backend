# NaijaShield Authentication API - Quick Reference

## Base URL
- Development: `https://localhost:7000`
- Production: `https://api.naijashield.com` (update as needed)

## Authentication
All protected endpoints require a Bearer token in the Authorization header:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

---

## Endpoints

### 1. Login
**POST** `/api/auth/login`

Authenticates a user and returns JWT tokens.

**Request Body:**
```json
{
  "email": "analyst@mtn.ng",
  "password": "password123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-001",
    "name": "Emeka Okafor",
    "email": "analyst@mtn.ng",
    "role": "SOC_ANALYST",
    "organisation": "MTN Nigeria"
  }
}
```

**Error Responses:**
- **401 Unauthorized** - Invalid credentials
  ```json
  {
    "error": "INVALID_CREDENTIALS",
    "message": "Email or password is incorrect"
  }
  ```
- **429 Too Many Requests** - Account locked
  ```json
  {
    "error": "RATE_LIMIT_EXCEEDED",
    "message": "Account is locked due to too many failed login attempts. Please try again in 15 minutes."
  }
  ```

**Rate Limiting:**
- 5 failed attempts ? 15-minute account lockout

---

### 2. Accept Invitation
**POST** `/api/auth/invite/accept`

Accepts an invitation and sets the user's password. This is the signup endpoint.

**Request Body:**
```json
{
  "inviteToken": "abc123xyz789",
  "password": "NewPassword@1234",
  "confirmPassword": "NewPassword@1234"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-006",
    "name": "Segun Lawal",
    "email": "s.lawal@mtn.ng",
    "role": "COMPLIANCE_OFFICER",
    "organisation": "MTN Nigeria"
  }
}
```

**Error Responses:**
- **400 Bad Request** - Invalid or expired invite
  ```json
  {
    "error": "INVALID_INVITE",
    "message": "This invite link is invalid or has expired"
  }
  ```
- **400 Bad Request** - Passwords don't match
  ```json
  {
    "error": "PASSWORDS_DO_NOT_MATCH",
    "message": "Passwords do not match"
  }
  ```

**Notes:**
- Invite tokens expire after 48 hours
- User email and role are pre-set by admin
- User only sets their password

---

### 3. Create Invitation
**POST** `/api/auth/invite`

Creates a new user invitation. **SYSTEM_ADMIN only.**

**Authorization:** Required (SYSTEM_ADMIN role)

**Request Body:**
```json
{
  "email": "newperson@mtn.ng",
  "name": "New Person",
  "role": "SOC_ANALYST"
}
```

**Valid Roles:**
- `SOC_ANALYST`
- `COMPLIANCE_OFFICER`
- `SYSTEM_ADMIN`

**Success Response (201 Created):**
```json
{
  "inviteId": "INV-007",
  "email": "newperson@mtn.ng",
  "name": "New Person",
  "role": "SOC_ANALYST",
  "expiresAt": "2025-04-29T09:00:00Z",
  "status": "Pending"
}
```

**Error Responses:**
- **403 Forbidden** - Not a System Admin
  ```json
  {
    "error": "INSUFFICIENT_PERMISSIONS",
    "message": "Only System Admins can invite new users"
  }
  ```
- **409 Conflict** - Email already exists
  ```json
  {
    "error": "USER_ALREADY_EXISTS",
    "message": "A user with this email already exists"
  }
  ```

**Notes:**
- Sends invitation email with invite link
- Organisation is inherited from the admin creating the invitation
- Invite token expires in 48 hours

---

### 4. Refresh Token
**POST** `/api/auth/refresh`

Exchanges a refresh token for new access and refresh tokens.

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBh..."
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh..."
}
```

**Error Response:**
- **401 Unauthorized** - Invalid or expired refresh token
  ```json
  {
    "error": "INVALID_REFRESH_TOKEN",
    "message": "Refresh token is invalid or has expired. Please log in again."
  }
  ```

**Notes:**
- Old refresh token is invalidated
- New tokens are issued
- Refresh tokens expire after 7 days

---

### 5. Logout
**POST** `/api/auth/logout`

Invalidates the user's refresh token.

**Authorization:** Required

**Request Body:** Empty `{}`

**Success Response (200 OK):**
```json
{
  "message": "Logged out successfully"
}
```

**Notes:**
- Client should also clear access token from memory
- Refresh token is invalidated server-side

---

## JWT Token Structure

### Access Token Claims
```json
{
  "sub": "USR-001",              // User ID
  "email": "analyst@mtn.ng",     // User email
  "name": "Emeka Okafor",        // Full name
  "role": "SOC_ANALYST",         // User role
  "organisation": "MTN Nigeria", // Organisation
  "iat": 1745740800,             // Issued at (Unix timestamp)
  "exp": 1745744400              // Expires at (1 hour after iat)
}
```

### Token Expiry
- **Access Token:** 1 hour
- **Refresh Token:** 7 days
- **Invite Token:** 48 hours

---

## User Roles & Permissions

### SOC_ANALYST
**Can access:**
- `/api/overview` - View Overview dashboard
- `/api/threat-feed` - View & investigate Threat Feed

**Cannot access:**
- `/api/compliance`
- `/api/user-management`
- `/api/settings`

### COMPLIANCE_OFFICER
**Can access:**
- `/api/overview` - View Overview dashboard
- `/api/compliance` - View Compliance page, generate reports

**Cannot access:**
- `/api/threat-feed`
- `/api/user-management`
- `/api/settings`

### SYSTEM_ADMIN
**Can access:**
- All routes above PLUS:
- `/api/user-management` - Manage users
- `/api/settings` - System settings
- `/api/auth/invite` - Create invitations

---

## Error Response Format

All errors follow this format:

```json
{
  "error": "ERROR_CODE",
  "message": "Human readable message"
}
```

### Error Codes Reference

| HTTP | Error Code | Description |
|------|-----------|-------------|
| 400 | `INVALID_INVITE` | Invite token is missing, invalid, or expired |
| 400 | `PASSWORDS_DO_NOT_MATCH` | Password and confirmPassword fields differ |
| 400 | `INVALID_REQUEST` | Required fields missing or invalid |
| 401 | `INVALID_CREDENTIALS` | Wrong email or password on login |
| 401 | `TOKEN_EXPIRED` | Access token has expired |
| 401 | `INVALID_REFRESH_TOKEN` | Refresh token is invalid or expired |
| 401 | `INVALID_TOKEN` | Malformed or invalid authentication token |
| 403 | `INSUFFICIENT_PERMISSIONS` | User's role does not allow this action |
| 409 | `USER_ALREADY_EXISTS` | Email already registered in the system |
| 429 | `RATE_LIMIT_EXCEEDED` | Too many login attempts (locked for 15 min) |

---

## Testing Examples

### Using cURL

#### Login
```bash
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourPassword123"
  }'
```

#### Create Invitation
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

#### Accept Invitation
```bash
curl -X POST https://localhost:7000/api/auth/invite/accept \
  -H "Content-Type: application/json" \
  -d '{
    "inviteToken": "abc123xyz789",
    "password": "NewPassword123!",
    "confirmPassword": "NewPassword123!"
  }'
```

#### Refresh Token
```bash
curl -X POST https://localhost:7000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'
```

#### Logout
```bash
curl -X POST https://localhost:7000/api/auth/logout \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### Using JavaScript (Frontend)

```javascript
// Login
const loginResponse = await fetch('https://api.naijashield.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'password123'
  })
});

const { token, refreshToken, user } = await loginResponse.json();

// Store tokens (use secure storage)
localStorage.setItem('accessToken', token);
localStorage.setItem('refreshToken', refreshToken);

// Make authenticated request
const response = await fetch('https://api.naijashield.com/api/overview', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

// Handle token expiry
if (response.status === 401) {
  // Refresh token
  const refreshResponse = await fetch('https://api.naijashield.com/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });
  
  if (refreshResponse.ok) {
    const { token: newToken, refreshToken: newRefreshToken } = await refreshResponse.json();
    localStorage.setItem('accessToken', newToken);
    localStorage.setItem('refreshToken', newRefreshToken);
    // Retry original request
  } else {
    // Redirect to login
    window.location.href = '/login';
  }
}
```

---

## Security Notes

1. **HTTPS Only:** All endpoints must be accessed via HTTPS in production
2. **Password Requirements:** Minimum 8 characters (recommend 12+)
3. **Token Storage:** Store access tokens in memory, refresh tokens in secure storage
4. **CORS:** Frontend domain must be whitelisted in backend CORS policy
5. **No PII in Logs:** Passwords, tokens, and PII are never logged
6. **BCrypt Cost Factor:** 12 (industry standard)

---

**Version:** 1.0  
**Last Updated:** April 2025  
**Contact:** Backend Team
