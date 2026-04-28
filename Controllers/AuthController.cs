using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.Models;
using naija_shield_backend.Models.DTOs;
using naija_shield_backend.Services;
using System.Security.Claims;

namespace naija_shield_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_REQUEST",
                Message = "Email and password are required"
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, response, error) = await _authService.LoginAsync(request, ipAddress);

        if (!success)
        {
            return error!.Error == "RATE_LIMIT_EXCEEDED" 
                ? StatusCode(429, error) 
                : Unauthorized(error);
        }

        return Ok(response);
    }

    [HttpPost("invite/accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] InviteAcceptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InviteToken) || 
            string.IsNullOrWhiteSpace(request.Password) || 
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_REQUEST",
                Message = "All fields are required"
            });
        }

        var (success, response, error) = await _authService.AcceptInviteAsync(request);

        if (!success)
        {
            return BadRequest(error);
        }

        return Ok(response);
    }

    [HttpPost("invite")]
    [Authorize(Roles = UserRole.SYSTEM_ADMIN)]
    public async Task<IActionResult> CreateInvite([FromBody] InviteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Name) || 
            string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_REQUEST",
                Message = "Email, name, and role are required"
            });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value 
                     ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "INVALID_TOKEN",
                Message = "Invalid authentication token"
            });
        }

        var (success, response, error) = await _authService.CreateInviteAsync(request, userId);

        if (!success)
        {
            return error!.Error == "USER_ALREADY_EXISTS" 
                ? Conflict(error) 
                : BadRequest(error);
        }

        return StatusCode(201, response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_REQUEST",
                Message = "Refresh token is required"
            });
        }

        var (success, response, error) = await _authService.RefreshTokenAsync(request);

        if (!success)
        {
            return Unauthorized(error);
        }

        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value 
                     ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "INVALID_TOKEN",
                Message = "Invalid authentication token"
            });
        }

        await _authService.LogoutAsync(userId);

        return Ok(new LogoutResponse
        {
            Message = "Logged out successfully"
        });
    }
}
