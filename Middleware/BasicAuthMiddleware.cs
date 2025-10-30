using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Simple middleware that simulates Basic authentication using the Authorization header.
/// Checks paths that start with /users and returns 401 if credentials are missing/invalid.
/// On success it sets HttpContext.User with a Name claim.
/// </summary>
public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDictionary<string, string> _validUsers;

    public BasicAuthMiddleware(RequestDelegate next)
    {
        _next = next;
        // In-memory credentials - change or move to config if needed.
        _validUsers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "admin", "password" },
            { "user", "secret" }
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only protect /users endpoints (adjust as needed)
        if (context.Request.Path.StartsWithSegments("/users", StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                await Challenge(context, "Missing Authorization header");
                return;
            }

            var header = authHeader.ToString();
            if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                await Challenge(context, "Invalid Authorization scheme");
                return;
            }

            var token = header.Substring("Basic ".Length).Trim();
            string credentialString;
            try
            {
                var credBytes = Convert.FromBase64String(token);
                credentialString = Encoding.UTF8.GetString(credBytes);
            }
            catch
            {
                await Challenge(context, "Invalid Base64 token");
                return;
            }

            var parts = credentialString.Split(':', 2);
            if (parts.Length != 2)
            {
                await Challenge(context, "Invalid credential format");
                return;
            }

            var username = parts[0];
            var password = parts[1];

            if (!_validUsers.TryGetValue(username, out var expected) || expected != password)
            {
                await Challenge(context, "Invalid username or password");
                return;
            }

            // Authentication successful — set a simple principal
            var claims = new[] { new Claim(ClaimTypes.Name, username) };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Basic"));
        }

        await _next(context);
    }

    private static Task Challenge(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"UserManagementAPI\"";
        return context.Response.WriteAsync(message);
    }
}
