using System.Security.Claims;
using naija_shield_backend.DTOs;
using naija_shield_backend.Services;

namespace naija_shield_backend.Endpoints;

/// <summary>
/// Maps the 5 auth endpoints as minimal API route groups.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        // ===================================================
        // POST /api/auth/login — Public
        // ===================================================
        auth.MapPost("/login", async (LoginRequest request, AuthService authService) =>
        {
            return await authService.LoginAsync(request);
        })
        .AllowAnonymous()
        .WithName("Login")
        .Produces<AuthResponse>(200)
        .Produces<ErrorResponse>(401)
        .Produces<ErrorResponse>(429);

        // ===================================================
        // POST /api/auth/invite/accept — Public (invite token is auth)
        // ===================================================
        auth.MapPost("/invite/accept", async (InviteAcceptRequest request, AuthService authService) =>
        {
            return await authService.AcceptInviteAsync(request);
        })
        .AllowAnonymous()
        .WithName("AcceptInvite")
        .Produces<AuthResponse>(200)
        .Produces<ErrorResponse>(400);

        // ===================================================
        // POST /api/auth/invite — SYSTEM_ADMIN only
        // ===================================================
        auth.MapPost("/invite", async (InviteRequest request, AuthService authService, HttpContext context) =>
        {
            // Extract role from JWT claims (check both mapped and unmapped names)
            var roleClaim = context.User.FindFirst("role")?.Value
                          ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != "SYSTEM_ADMIN")
            {
                return Results.Json(new ErrorResponse
                {
                    Error = "INSUFFICIENT_PERMISSIONS",
                    Message = "Only System Admins can invite new users"
                }, statusCode: 403);
            }

            var userId = context.User.FindFirst("sub")?.Value
                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? string.Empty;

            return await authService.CreateInviteAsync(request, userId);
        })
        .RequireAuthorization()
        .WithName("CreateInvite")
        .Produces<InviteResponse>(201)
        .Produces<ErrorResponse>(403)
        .Produces<ErrorResponse>(409);

        // ===================================================
        // POST /api/auth/refresh — Public (refresh token is credential)
        // ===================================================
        auth.MapPost("/refresh", async (RefreshRequest request, AuthService authService) =>
        {
            return await authService.RefreshTokenAsync(request);
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .Produces(200)
        .Produces<ErrorResponse>(401);

        // ===================================================
        // GET /api/auth/users — SYSTEM_ADMIN only
        // ===================================================
        auth.MapGet("/users", async (AuthService authService, HttpContext context) =>
        {
            var roleClaim = context.User.FindFirst("role")?.Value
                          ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != "SYSTEM_ADMIN")
            {
                return Results.Json(new ErrorResponse
                {
                    Error = "INSUFFICIENT_PERMISSIONS",
                    Message = "Only System Admins can list users"
                }, statusCode: 403);
            }

            return await authService.GetUsersAsync();
        })
        .RequireAuthorization()
        .WithName("GetUsers")
        .Produces(200)
        .Produces<ErrorResponse>(403);

        // ===================================================
        // DELETE /api/auth/users/{id} — SYSTEM_ADMIN only
        // ===================================================
        auth.MapDelete("/users/{id}", async (string id, AuthService authService, HttpContext context) =>
        {
            var roleClaim = context.User.FindFirst("role")?.Value
                          ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != "SYSTEM_ADMIN")
            {
                return Results.Json(new ErrorResponse
                {
                    Error = "INSUFFICIENT_PERMISSIONS",
                    Message = "Only System Admins can remove users"
                }, statusCode: 403);
            }

            var adminId = context.User.FindFirst("sub")?.Value
                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? string.Empty;

            return await authService.DeleteUserAsync(id, adminId);
        })
        .RequireAuthorization()
        .WithName("DeleteUser")
        .Produces(200)
        .Produces<ErrorResponse>(400)
        .Produces<ErrorResponse>(403)
        .Produces<ErrorResponse>(404);

        // ===================================================
        // POST /api/auth/logout — Authenticated
        // ===================================================
        auth.MapPost("/logout", async (AuthService authService, HttpContext context) =>
        {
            var userId = context.User.FindFirst("sub")?.Value
                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Results.Json(new ErrorResponse
                {
                    Error = "TOKEN_EXPIRED",
                    Message = "Access token has expired"
                }, statusCode: 401);
            }

            return await authService.LogoutAsync(userId);
        })
        .RequireAuthorization()
        .WithName("Logout")
        .Produces(200);
    }
}
