using BCrypt.Net;
using naija_shield_backend.Models;
using naija_shield_backend.Models.DTOs;

namespace naija_shield_backend.Services;

public interface IAuthService
{
    Task<(bool success, AuthResponse? response, ErrorResponse? error)> LoginAsync(LoginRequest request, string ipAddress);
    Task<(bool success, AuthResponse? response, ErrorResponse? error)> AcceptInviteAsync(InviteAcceptRequest request);
    Task<(bool success, InviteResponse? response, ErrorResponse? error)> CreateInviteAsync(InviteRequest request, string adminUserId);
    Task<(bool success, AuthResponse? response, ErrorResponse? error)> RefreshTokenAsync(RefreshTokenRequest request);
    Task<bool> LogoutAsync(string userId);
}

public class AuthService : IAuthService
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserService userService, 
        ITokenService tokenService, 
        ILogger<AuthService> logger,
        IEmailService emailService)
    {
        _userService = userService;
        _tokenService = tokenService;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<(bool success, AuthResponse? response, ErrorResponse? error)> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await _userService.GetUserByEmailAsync(request.Email);
        
        if (user is null)
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            });
        }

        // Check if account is locked
        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        {
            var remainingMinutes = (int)(user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
            return (false, null, new ErrorResponse
            {
                Error = "RATE_LIMIT_EXCEEDED",
                Message = $"Account is locked due to too many failed login attempts. Please try again in {remainingMinutes} minutes."
            });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;
            
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                await _userService.UpdateUserAsync(user);
                
                return (false, null, new ErrorResponse
                {
                    Error = "RATE_LIMIT_EXCEEDED",
                    Message = "Too many failed login attempts. Account locked for 15 minutes."
                });
            }

            await _userService.UpdateUserAsync(user);
            
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            });
        }

        // Check if user is active
        if (user.Status != UserStatus.Active)
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_CREDENTIALS",
                Message = "Email or password is incorrect"
            });
        }

        // Reset failed login attempts
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        user.LastActive = DateTime.UtcNow;

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Store refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _userService.UpdateUserAsync(user);

        return (true, new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Organisation = user.Organisation
            }
        }, null);
    }

    public async Task<(bool success, AuthResponse? response, ErrorResponse? error)> AcceptInviteAsync(InviteAcceptRequest request)
    {
        // Validate passwords match
        if (request.Password != request.ConfirmPassword)
        {
            return (false, null, new ErrorResponse
            {
                Error = "PASSWORDS_DO_NOT_MATCH",
                Message = "Passwords do not match"
            });
        }

        var user = await _userService.GetUserByInviteTokenAsync(request.InviteToken);
        
        if (user is null || !user.InviteExpiry.HasValue || user.InviteExpiry.Value < DateTime.UtcNow)
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_INVITE",
                Message = "This invite link is invalid or has expired"
            });
        }

        // Hash password with bcrypt cost factor 12
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);

        // Update user
        user.Password = hashedPassword;
        user.Status = UserStatus.Active;
        user.InviteToken = null;
        user.InviteExpiry = null;
        user.LastActive = DateTime.UtcNow;

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _userService.UpdateUserAsync(user);

        return (true, new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Organisation = user.Organisation
            }
        }, null);
    }

    public async Task<(bool success, InviteResponse? response, ErrorResponse? error)> CreateInviteAsync(InviteRequest request, string adminUserId)
    {
        // Validate role
        if (!UserRole.IsValid(request.Role))
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_ROLE",
                Message = "Invalid role specified"
            });
        }

        // Check if user already exists
        var existingUser = await _userService.GetUserByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return (false, null, new ErrorResponse
            {
                Error = "USER_ALREADY_EXISTS",
                Message = "A user with this email already exists"
            });
        }

        // Get admin user for organisation
        var adminUser = await _userService.GetUserByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_ADMIN",
                Message = "Invalid admin user"
            });
        }

        // Generate invite token
        var inviteToken = Guid.NewGuid().ToString("N");
        var inviteExpiry = DateTime.UtcNow.AddHours(48);

        // Create pending user
        var userId = await _userService.GenerateNextUserIdAsync();
        var newUser = new User
        {
            Id = userId,
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            Organisation = adminUser.Organisation,
            Status = UserStatus.Pending,
            InviteToken = inviteToken,
            InviteExpiry = inviteExpiry,
            CreatedAt = DateTime.UtcNow
        };

        await _userService.CreateUserAsync(newUser);

        // Send invitation email
        await _emailService.SendInvitationEmailAsync(request.Email, request.Name, inviteToken);

        return (true, new InviteResponse
        {
            InviteId = userId,
            Email = request.Email,
            Name = request.Name,
            Role = request.Role,
            ExpiresAt = inviteExpiry,
            Status = UserStatus.Pending
        }, null);
    }

    public async Task<(bool success, AuthResponse? response, ErrorResponse? error)> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var user = await _userService.GetUserByRefreshTokenAsync(request.RefreshToken);
        
        if (user is null || !user.RefreshTokenExpiry.HasValue || user.RefreshTokenExpiry.Value < DateTime.UtcNow)
        {
            return (false, null, new ErrorResponse
            {
                Error = "INVALID_REFRESH_TOKEN",
                Message = "Refresh token is invalid or has expired. Please log in again."
            });
        }

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Update refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _userService.UpdateUserAsync(user);

        return (true, new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Organisation = user.Organisation
            }
        }, null);
    }

    public async Task<bool> LogoutAsync(string userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user is null) return false;

        // Invalidate refresh token
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;

        await _userService.UpdateUserAsync(user);
        return true;
    }
}
