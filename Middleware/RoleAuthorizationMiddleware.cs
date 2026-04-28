using naija_shield_backend.Models;
using System.Security.Claims;

namespace naija_shield_backend.Middleware;

public class RoleAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RoleAuthorizationMiddleware> _logger;

    private readonly Dictionary<string, string[]> _routePermissions = new()
    {
        { "/overview", new[] { UserRole.SOC_ANALYST, UserRole.COMPLIANCE_OFFICER, UserRole.SYSTEM_ADMIN } },
        { "/threat-feed", new[] { UserRole.SOC_ANALYST, UserRole.SYSTEM_ADMIN } },
        { "/compliance", new[] { UserRole.COMPLIANCE_OFFICER, UserRole.SYSTEM_ADMIN } },
        { "/user-management", new[] { UserRole.SYSTEM_ADMIN } },
        { "/settings", new[] { UserRole.SYSTEM_ADMIN } }
    };

    public RoleAuthorizationMiddleware(RequestDelegate next, ILogger<RoleAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for non-API routes or auth endpoints
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        
        if (!path.StartsWith("/api/") || path.StartsWith("/api/auth/"))
        {
            await _next(context);
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        // Get user role from claims
        var userRole = context.User.FindFirst("role")?.Value ?? string.Empty;

        if (string.IsNullOrEmpty(userRole))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "INSUFFICIENT_PERMISSIONS",
                message = "User role not found in token"
            });
            return;
        }

        // Check route permissions for frontend routes
        foreach (var route in _routePermissions)
        {
            if (path.Contains(route.Key.ToLower()))
            {
                if (!route.Value.Contains(userRole))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "INSUFFICIENT_PERMISSIONS",
                        message = $"Access denied. Required role: {string.Join(" or ", route.Value)}"
                    });
                    return;
                }
                break;
            }
        }

        await _next(context);
    }
}

public static class RoleAuthorizationMiddlewareExtensions
{
    public static IApplicationBuilder UseRoleAuthorization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RoleAuthorizationMiddleware>();
    }
}
