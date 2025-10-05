using Hangfire.Dashboard;
using Jiten.Core.Data.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Jiten.Api.Helpers;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IConfiguration _configuration;

    public HangfireAuthorizationFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext.Request.Cookies.TryGetValue("token", out var token))
        {
            var userRoles = ValidateTokenAndGetRoles(token);

            if (userRoles != null && userRoles.Contains(nameof(UserRole.Administrator)))
            {
                return true;
            }
        }

        return httpContext.User.Identity?.IsAuthenticated == true && httpContext.User.IsInRole(nameof(UserRole.Administrator));
    }

    /// <summary>
    /// Validates a JWT token and extracts user roles from its claims.
    /// </summary>
    /// <param name="token">The JWT token extracted from the cookie.</param>
    /// <returns>An array of roles if the token is valid, otherwise null.</returns>
    private string[]? ValidateTokenAndGetRoles(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var tokenValidationParameters = new TokenValidationParameters
                                            {
                                                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                                                ValidateIssuerSigningKey = true, ValidIssuer = jwtSettings["Issuer"],
                                                ValidAudience = jwtSettings["Audience"],
                                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
                                                ClockSkew = TimeSpan.Zero
                                            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

            if (!(validatedToken is JwtSecurityToken jwtToken) ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var roleClaims = principal.Claims.Where(c => c.Type == ClaimTypes.Role);
            if (!roleClaims.Any())
            {
                return [];
            }

            return roleClaims.Select(c => c.Value).ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}