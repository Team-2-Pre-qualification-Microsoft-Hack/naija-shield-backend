using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using naija_shield_backend.DTOs;
using naija_shield_backend.Models;
using User = naija_shield_backend.Models.User;

namespace naija_shield_backend.Services;

/// <summary>
/// Core authentication service handling login, invite, refresh, and logout flows.
/// </summary>
public class AuthService
{
    private readonly Container _usersContainer;
    private readonly Container _refreshTokensContainer;
    private readonly TokenService _tokenService;
    private readonly RateLimitService _rateLimitService;
    private readonly EmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _config;

    public AuthService(
        CosmosClient cosmosClient,
        TokenService tokenService,
        RateLimitService rateLimitService,
        EmailService emailService,
        ILogger<AuthService> logger,
        IConfiguration config)
    {
        var database = cosmosClient.GetDatabase("NaijaShieldDB");
        _usersContainer = database.GetContainer("Users");
        _refreshTokensContainer = database.GetContainer("RefreshTokens");
        _tokenService = tokenService;
        _rateLimitService = rateLimitService;
        _emailService = emailService;
        _logger = logger;
        _config = config;
    }

    // =============================================
    // LOGIN
    // =============================================
    public async Task<IResult> LoginAsync(LoginRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            }, statusCode: 401);
        }

        // Rate limit check
        if (_rateLimitService.IsRateLimited(request.Email))
        {
            return Results.Json(new ErrorResponse
            {
                Error = "RATE_LIMIT_EXCEEDED",
                Message = "Too many login attempts. Please try again in 15 minutes."
            }, statusCode: 429);
        }

        // Find user by email
        var user = await FindUserByEmailAsync(request.Email);
        if (user == null || user.Status != UserStatus.Active)
        {
            _rateLimitService.RecordFailedAttempt(request.Email);
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            }, statusCode: 401);
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            _rateLimitService.RecordFailedAttempt(request.Email);
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            }, statusCode: 401);
        }

        // Success — reset rate limit and issue tokens
        _rateLimitService.ResetAttempts(request.Email);

        // Update lastActive
        user.LastActive = DateTime.UtcNow;
        await _usersContainer.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));

        return Results.Ok(await BuildAuthResponseAsync(user));
    }

    // =============================================
    // ACCEPT INVITE (Signup)
    // =============================================
    public async Task<IResult> AcceptInviteAsync(InviteAcceptRequest request)
    {
        // Validate passwords match
        if (request.Password != request.ConfirmPassword)
        {
            return Results.Json(new ErrorResponse
            {
                Error = "PASSWORDS_DO_NOT_MATCH",
                Message = "Passwords do not match"
            }, statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.InviteToken))
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_INVITE",
                Message = "This invite link is invalid or has expired"
            }, statusCode: 400);
        }

        // Find user by invite token
        var user = await FindUserByInviteTokenAsync(request.InviteToken);
        if (user == null || user.InviteExpiry == null || user.InviteExpiry < DateTime.UtcNow)
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_INVITE",
                Message = "This invite link is invalid or has expired"
            }, statusCode: 400);
        }

        // Set password, activate user, clear invite
        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        user.Status = UserStatus.Active;
        user.InviteToken = null;
        user.InviteExpiry = null;
        user.LastActive = DateTime.UtcNow;

        await _usersContainer.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));

        return Results.Ok(await BuildAuthResponseAsync(user));
    }

    // =============================================
    // CREATE INVITE (Admin only)
    // =============================================
    public async Task<IResult> CreateInviteAsync(InviteRequest request, string adminUserId)
    {
        // Validate role
        if (!Enum.TryParse<UserRole>(request.Role, out var role))
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_INVITE",
                Message = "Invalid role specified. Must be SOC_ANALYST, COMPLIANCE_OFFICER, or SYSTEM_ADMIN"
            }, statusCode: 400);
        }

        // Check for existing user
        var existingUser = await FindUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Results.Json(new ErrorResponse
            {
                Error = "USER_ALREADY_EXISTS",
                Message = "A user with this email already exists"
            }, statusCode: 409);
        }

        // Generate user ID
        var userId = await GenerateUserIdAsync();
        var inviteToken = Guid.NewGuid().ToString("N"); // 32-char hex
        var expiresAt = DateTime.UtcNow.AddHours(48);

        var newUser = new User
        {
            Id = userId,
            Name = request.Name,
            Email = request.Email.ToLowerInvariant(),
            Password = string.Empty, // Set when invite is accepted
            Role = role,
            Organisation = await GetOrganisationForAdmin(adminUserId),
            Status = UserStatus.Pending,
            InviteToken = inviteToken,
            InviteExpiry = expiresAt,
            LastActive = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _usersContainer.CreateItemAsync(newUser, new PartitionKey(newUser.Id));

        // Send invite email via Azure Communication Services
        var emailSent = await _emailService.SendInviteEmailAsync(
            newUser.Email, newUser.Name, newUser.Role.ToString(), inviteToken);

        if (!emailSent)
        {
            _logger.LogWarning(
                "Invite created for {Email} but email delivery failed. Token: {Token}",
                newUser.Email, inviteToken);
        }

        return Results.Json(new InviteResponse
        {
            InviteId = userId,
            InviteToken = inviteToken,
            Email = newUser.Email,
            Name = newUser.Name,
            Role = newUser.Role.ToString(),
            ExpiresAt = expiresAt,
            Status = "Pending",
            EmailSent = emailSent
        }, statusCode: 201);
    }

    // =============================================
    // REFRESH TOKEN
    // =============================================
    public async Task<IResult> RefreshTokenAsync(RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_REFRESH_TOKEN",
                Message = "Refresh token is invalid or has expired. Please log in again."
            }, statusCode: 401);
        }

        // Find the stored refresh token
        var storedToken = await FindRefreshTokenAsync(request.RefreshToken);
        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_REFRESH_TOKEN",
                Message = "Refresh token is invalid or has expired. Please log in again."
            }, statusCode: 401);
        }

        // Find the user
        User user;
        try
        {
            var response = await _usersContainer.ReadItemAsync<User>(
                storedToken.UserId, new PartitionKey(storedToken.UserId));
            user = response.Resource;
        }
        catch (CosmosException)
        {
            return Results.Json(new ErrorResponse
            {
                Error = "INVALID_REFRESH_TOKEN",
                Message = "Refresh token is invalid or has expired. Please log in again."
            }, statusCode: 401);
        }

        // Revoke old refresh token
        await _refreshTokensContainer.DeleteItemAsync<Models.RefreshToken>(
            storedToken.Id, new PartitionKey(storedToken.UserId));

        // Issue new token pair
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();

        int refreshDays = int.Parse(_config.GetSection("Jwt")["RefreshTokenExpiryDays"] ?? "7");
        var newRefreshToken = new Models.RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAt = DateTime.UtcNow
        };
        await _refreshTokensContainer.CreateItemAsync(newRefreshToken, new PartitionKey(user.Id));

        return Results.Ok(new { token = newAccessToken, refreshToken = newRefreshTokenStr });
    }

    // =============================================
    // LOGOUT
    // =============================================
    public async Task<IResult> LogoutAsync(string userId)
    {
        // Delete all refresh tokens for this user
        var query = _refreshTokensContainer.GetItemLinqQueryable<Models.RefreshToken>()
            .Where(t => t.UserId == userId)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            foreach (var token in batch)
            {
                await _refreshTokensContainer.DeleteItemAsync<Models.RefreshToken>(
                    token.Id, new PartitionKey(token.UserId));
            }
        }

        return Results.Ok(new { message = "Logged out successfully" });
    }

    // =============================================
    // LIST USERS (Admin only)
    // =============================================
    public async Task<IResult> GetUsersAsync()
    {
        var query = _usersContainer.GetItemLinqQueryable<User>(
            requestOptions: new QueryRequestOptions { MaxItemCount = -1 })
            .ToFeedIterator();

        var users = new List<object>();
        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            foreach (var u in batch)
            {
                users.Add(new
                {
                    id           = u.Id,
                    name         = u.Name,
                    email        = u.Email,
                    role         = u.Role.ToString(),
                    status       = u.Status.ToString(),
                    organisation = u.Organisation,
                    lastActive   = u.LastActive,
                    createdAt    = u.CreatedAt,
                    invitePending = u.Status == UserStatus.Pending
                });
            }
        }

        return Results.Ok(new { total = users.Count, users });
    }

    // =============================================
    // SEED DEFAULT ADMIN
    // =============================================
    public async Task SeedDefaultAdminAsync()
    {
        // Check if any users exist
        var query = _usersContainer.GetItemLinqQueryable<User>()
            .Take(1)
            .ToFeedIterator();

        var result = await query.ReadNextAsync();
        if (result.Count > 0) return; // Users already exist

        var admin = new User
        {
            Id = "USR-001",
            Name = "System Administrator",
            Email = "admin@naijashield.ng",
            Password = BCrypt.Net.BCrypt.HashPassword("Admin@NaijaShield2025", workFactor: 12),
            Role = UserRole.SYSTEM_ADMIN,
            Organisation = "NaijaShield",
            Status = UserStatus.Active,
            InviteToken = null,
            InviteExpiry = null,
            LastActive = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _usersContainer.CreateItemAsync(admin, new PartitionKey(admin.Id));
        Console.WriteLine("Default SYSTEM_ADMIN seeded: admin@naijashield.ng / Admin@NaijaShield2025");
    }

    // =============================================
    // HELPER METHODS
    // =============================================

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        int refreshDays = int.Parse(_config.GetSection("Jwt")["RefreshTokenExpiryDays"] ?? "7");

        // Store refresh token server-side
        var refreshToken = new Models.RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAt = DateTime.UtcNow
        };
        await _refreshTokensContainer.CreateItemAsync(refreshToken, new PartitionKey(user.Id));

        return new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshTokenStr,
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString(),
                Organisation = user.Organisation
            }
        };
    }

    private async Task<User?> FindUserByEmailAsync(string email)
    {
        var query = _usersContainer.GetItemLinqQueryable<User>()
            .Where(u => u.Email == email.ToLowerInvariant())
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            var user = batch.FirstOrDefault();
            if (user != null) return user;
        }
        return null;
    }

    private async Task<User?> FindUserByInviteTokenAsync(string inviteToken)
    {
        var query = _usersContainer.GetItemLinqQueryable<User>()
            .Where(u => u.InviteToken == inviteToken)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            var user = batch.FirstOrDefault();
            if (user != null) return user;
        }
        return null;
    }

    private async Task<Models.RefreshToken?> FindRefreshTokenAsync(string token)
    {
        var query = _refreshTokensContainer.GetItemLinqQueryable<Models.RefreshToken>()
            .Where(t => t.Token == token)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            var rt = batch.FirstOrDefault();
            if (rt != null) return rt;
        }
        return null;
    }

    private async Task<string> GenerateUserIdAsync()
    {
        // Count existing users to generate next ID
        var query = _usersContainer.GetItemLinqQueryable<User>(
            requestOptions: new QueryRequestOptions { MaxItemCount = -1 })
            .Select(u => u.Id)
            .ToFeedIterator();

        int maxNum = 0;
        while (query.HasMoreResults)
        {
            var batch = await query.ReadNextAsync();
            foreach (var id in batch)
            {
                if (id.StartsWith("USR-") && int.TryParse(id[4..], out int num))
                {
                    maxNum = Math.Max(maxNum, num);
                }
            }
        }

        return $"USR-{(maxNum + 1):D3}";
    }

    private async Task<string> GetOrganisationForAdmin(string adminUserId)
    {
        try
        {
            var response = await _usersContainer.ReadItemAsync<User>(
                adminUserId, new PartitionKey(adminUserId));
            return response.Resource.Organisation;
        }
        catch
        {
            return "NaijaShield";
        }
    }
}
