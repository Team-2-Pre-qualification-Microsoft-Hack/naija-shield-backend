# ?? Postman Testing Guide for NaijaShield

## ? NEW! Development Endpoint Added

I've added a special endpoint just for you to create the admin user directly from Postman!

---

## ?? **Quick Start**

### **Step 1: Restart Your App**

Stop the app (Ctrl+C) and restart:

```powershell
dotnet run
```

**Wait for:**
```
Now listening on: http://localhost:5209
```

---

### **Step 2: Create Admin in Postman**

**NEW ENDPOINT (Development Only):**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/dev/create-admin`
- **Headers:** None needed
- **Body:** None needed

**Click Send**

**Response:**
```json
{
  "message": "Admin user created successfully!",
  "email": "admin@naijashield.com",
  "password": "admin123",
  "role": "SYSTEM_ADMIN"
}
```

? **Done! Admin created without Azure Portal!**

---

### **Step 3: Test Login**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/auth/login`
- **Header:** `Content-Type: application/json`
- **Body:**
```json
{
  "email": "admin@naijashield.com",
  "password": "admin123"
}
```

**Response:**
```json
{
  "token": "eyJhbGci...",
  "refreshToken": "dGhpc...",
  "user": {
    "id": "USR-001",
    "name": "Admin User",
    "email": "admin@naijashield.com",
    "role": "SYSTEM_ADMIN"
  }
}
```

**Copy the token!**

---

### **Step 4: Test Create Invitation**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/auth/invite`
- **Headers:**
  - `Content-Type: application/json`
  - `Authorization: Bearer PASTE_TOKEN_HERE`
- **Body:**
```json
{
  "email": "analyst@test.com",
  "name": "Test Analyst",
  "role": "SOC_ANALYST"
}
```

**Response:**
```json
{
  "inviteId": "USR-002",
  "email": "analyst@test.com",
  "name": "Test Analyst",
  "role": "SOC_ANALYST",
  "expiresAt": "2025-04-30T...",
  "status": "Pending"
}
```

**Check console** for invite token!

---

### **Step 5: Test Accept Invitation**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/auth/invite/accept`
- **Header:** `Content-Type: application/json`
- **Body:**
```json
{
  "inviteToken": "PASTE_FROM_CONSOLE",
  "password": "Analyst123!",
  "confirmPassword": "Analyst123!"
}
```

---

### **Step 6: Test Refresh Token**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/auth/refresh`
- **Header:** `Content-Type: application/json`
- **Body:**
```json
{
  "refreshToken": "YOUR_REFRESH_TOKEN"
}
```

---

### **Step 7: Test Logout**

- **Method:** `POST`
- **URL:** `http://localhost:5209/api/auth/logout`
- **Headers:**
  - `Content-Type: application/json`
  - `Authorization: Bearer YOUR_TOKEN`
- **Body:**
```json
{
  "refreshToken": "YOUR_REFRESH_TOKEN"
}
```

---

## ?? **All Endpoints**

| # | Endpoint | Method | Auth | Description |
|---|----------|--------|------|-------------|
| 1 | `/api/dev/create-admin` | POST | ? | **Create admin user** |
| 2 | `/api/auth/login` | POST | ? | User login |
| 3 | `/api/auth/invite` | POST | ? Admin | Create invitation |
| 4 | `/api/auth/invite/accept` | POST | ? | Accept invitation |
| 5 | `/api/auth/refresh` | POST | ? | Refresh token |
| 6 | `/api/auth/logout` | POST | ? Yes | Logout |
| 7 | `/api/test-scam` | GET | ? | AI scam detection |

---

## ?? **Security Note**

?? The `/api/dev/create-admin` endpoint is **ONLY available in Development mode**.

In production, this endpoint **does not exist** (for security).

---

## ?? **You're All Set!**

With Postman, you can now:
1. ? Create admin user (one click!)
2. ? Test all auth endpoints
3. ? No Azure Portal needed
4. ? No PowerShell scripts needed

**Happy Testing! ??**
