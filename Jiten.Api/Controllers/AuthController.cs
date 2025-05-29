using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Services;
using Jiten.Core;
using Jiten.Core.Data.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly TokenService _tokenService;
    // private readonly IEmailSender _emailSender;
    private readonly UserDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly UrlEncoder _urlEncoder;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        RoleManager<IdentityRole> roleManager,
        TokenService tokenService,
        // IEmailSender emailSender,
        UserDbContext context,
        IConfiguration configuration,
        UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        // _emailSender = emailSender;
        _context = context;
        _configuration = configuration;
        _urlEncoder = urlEncoder;
    }

    // [HttpPost("register")]
    // public async Task<IActionResult> Register([FromBody] RegisterRequest model)
    // {
    //     if (!ModelState.IsValid) return BadRequest(ModelState);
    //
    //     var userExists = await _userManager.FindByNameAsync(model.Username);
    //     if (userExists != null) return Conflict(new { message = "Username already exists." });
    //
    //     var emailExists = await _userManager.FindByEmailAsync(model.Email);
    //     if (emailExists != null) return Conflict(new { message = "Email already registered." });
    //
    //     var user = new User { UserName = model.Username, Email = model.Email, SecurityStamp = Guid.NewGuid().ToString() };
    //
    //     var result = await _userManager.CreateAsync(user, model.Password);
    //     if (!result.Succeeded)
    //     {
    //         return BadRequest(new { message = "User creation failed.", errors = result.Errors.Select(e => e.Description) });
    //     }
    //
    //     // Assign default role
    //     await _userManager.AddToRoleAsync(user, UserRole.User.ToString());
    //
    //     // Send confirmation email
    //     var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    //     code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code)); // URL-safe token
    //
    //     var frontendBaseUrl = _configuration["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
    //     var callbackUrl = $"{frontendBaseUrl}/confirm-email?userId={user.Id}&code={code}";
    //
    //     await _emailSender.SendEmailAsync(model.Email, "Confirm your email",
    //                                       $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
    //
    //     return Ok(new { message = "Registration successful. Please check your email to confirm your account." });
    // }
    //
    // [HttpGet("confirm-email")]
    // public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string code)
    // {
    //     if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
    //         return BadRequest("User ID and code are required.");
    //
    //     var user = await _userManager.FindByIdAsync(userId);
    //     if (user == null)
    //         return NotFound("User not found.");
    //
    //     try
    //     {
    //         code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
    //     }
    //     catch (FormatException)
    //     {
    //         return BadRequest("Invalid token format.");
    //     }
    //
    //     var result = await _userManager.ConfirmEmailAsync(user, code);
    //     if (result.Succeeded)
    //         return Ok(new { message = "Email confirmed successfully. You can now log in." });
    //
    //     return BadRequest(new { message = "Email confirmation failed.", errors = result.Errors.Select(e => e.Description) });
    // }
    //
    // [HttpPost("resend-confirmation-email")]
    // public async Task<IActionResult> ResendConfirmationEmail([FromBody] ForgotPasswordRequest model) // Reusing DTO for email
    // {
    //     if (!ModelState.IsValid) return BadRequest(ModelState);
    //
    //     var user = await _userManager.FindByEmailAsync(model.Email);
    //     if (user == null)
    //     {
    //         // Don't reveal if the user exists or not for security reasons
    //         return Ok(new
    //                   {
    //                       message = "If an account with this email exists and is not confirmed, a new confirmation email has been sent."
    //                   });
    //     }
    //
    //     if (await _userManager.IsEmailConfirmedAsync(user))
    //     {
    //         return BadRequest(new { message = "This email is already confirmed." });
    //     }
    //
    //     var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    //     code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
    //
    //     var frontendBaseUrl = _configuration["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
    //     var callbackUrl = $"{frontendBaseUrl}/confirm-email?userId={user.Id}&code={code}";
    //
    //     await _emailSender.SendEmailAsync(model.Email, "Confirm your email",
    //                                       $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
    //
    //     return Ok(new { message = "A new confirmation email has been sent. Please check your email." });
    // }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.FindByNameAsync(model.UsernameOrEmail)
                   ?? await _userManager.FindByEmailAsync(model.UsernameOrEmail);

        if (user == null)
            return Unauthorized(new { message = "Invalid credentials." });

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Unauthorized(new { message = "Account locked due to too many failed login attempts. Please try again later." });

        if (result.IsNotAllowed) // e.g., email not confirmed
        {
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return Unauthorized(new { message = "Email not confirmed. Please check your email or resend confirmation." });
            }

            return Unauthorized(new { message = "Login not allowed for an unknown reason." });
        }

        if (result.RequiresTwoFactor)
        {
            // For Nuxt: return an indicator that 2FA is needed.
            // The client should then prompt for the 2FA code and call LoginWith2fa.
            return Ok(new { requiresTwoFactor = true, userId = user.Id });
        }

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid credentials." });

        var tokens = await _tokenService.GenerateTokensAsync(user, isTwoFactorLogin: false); // isTwoFactorLogin is false here
        await _context.SaveChangesAsync(); // Save refresh token
        return Ok(tokens);
    }

    [HttpPost("login-2fa")]
    public async Task<IActionResult> LoginWith2fa([FromBody] LoginWith2faRequest model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid request." });
        }

        // The "Authenticator" provider is the default for TOTP apps like Google Authenticator
        var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, model.RememberMachine,
                                                                            rememberClient: false);
        // `rememberClient: false` because JWTs are stateless. `RememberMachine` might be used by frontend to skip 2FA prompt for a period.

        if (result.IsLockedOut)
            return Unauthorized(new { message = "Account locked out." });

        if (result.IsNotAllowed)
            return Unauthorized(new { message = "Login not allowed." });

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid 2FA code." });

        var tokens = await _tokenService.GenerateTokensAsync(user, isTwoFactorLogin: true); // Mark as 2FA login
        await _context.SaveChangesAsync(); // Save refresh token
        return Ok(tokens);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // ... (Refresh logic as previously defined, ensure SaveChangesAsync is called for new refresh token)
        var principal = _tokenService.GetPrincipalFromExpiredToken(model.AccessToken);
        if (principal == null) return BadRequest(new { message = "Invalid access token or refresh token." });

        var userId = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return BadRequest(new { message = "Invalid token claims." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return BadRequest(new { message = "User not found." });

        var oldRefreshToken = await _context.RefreshTokens
                                            .FirstOrDefaultAsync(rt => rt.Token == model.RefreshToken && rt.UserId == userId);

        if (oldRefreshToken == null || oldRefreshToken.IsUsed || oldRefreshToken.IsRevoked || oldRefreshToken.ExpiryDate < DateTime.UtcNow)
        {
            // Potential token theft if an old or used token is presented.
            // Consider revoking all active refresh tokens for this user as a security measure.
            if (oldRefreshToken != null && (oldRefreshToken.IsUsed || oldRefreshToken.IsRevoked))
            {
                _context.RefreshTokens.Remove(oldRefreshToken); // Clean up if already invalid
                await _context.SaveChangesAsync();
            }
            // Optionally, revoke all user's refresh tokens if a compromised one is detected.
            // var userTokens = await _context.RefreshTokens.Where(rt => rt.UserId == userId && !rt.IsRevoked).ToListAsync();
            // foreach(var t in userTokens) t.IsRevoked = true;
            // _context.RefreshTokens.UpdateRange(userTokens);
            // await _context.SaveChangesAsync();

            return BadRequest(new { message = "Invalid or expired refresh token." });
        }

        // Mark old refresh token as used
        oldRefreshToken.IsUsed = true;
        _context.RefreshTokens.Update(oldRefreshToken);
        // Do NOT save changes yet, do it atomically with the new token generation.

        // Generate new tokens
        // Determine if the original login was via 2FA for the 'amr' claim
        bool wasTwoFactorLogin = principal.Claims.Any(c => c.Type == "amr" && c.Value == "mfa");
        var newTokens = await _tokenService.GenerateTokensAsync(user, wasTwoFactorLogin);

        await _context.SaveChangesAsync(); // Saves both the used old token and the new token

        return Ok(newTokens);
    }


    // [HttpPost("forgot-password")]
    // public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
    // {
    //     if (!ModelState.IsValid) return BadRequest(ModelState);
    //
    //     var user = await _userManager.FindByEmailAsync(model.Email);
    //     if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
    //     {
    //         // Don't reveal that the user does not exist or is not confirmed
    //         return Ok(new { message = "If your email address is registered and confirmed, you will receive a password reset link." });
    //     }
    //
    //     var code = await _userManager.GeneratePasswordResetTokenAsync(user);
    //     code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
    //
    //     var frontendBaseUrl = _configuration["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
    //     var callbackUrl = $"{frontendBaseUrl}/reset-password?email={_urlEncoder.Encode(user.Email)}&code={code}";
    //
    //     await _emailSender.SendEmailAsync(model.Email, "Reset Your Password",
    //                                       $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
    //
    //     return Ok(new { message = "If your email address is registered and confirmed, you will receive a password reset link." });
    // }
    //
    // [HttpPost("reset-password")]
    // public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
    // {
    //     if (!ModelState.IsValid) return BadRequest(ModelState);
    //
    //     var user = await _userManager.FindByEmailAsync(model.Email);
    //     if (user == null)
    //     {
    //         // Don't reveal that the user does not exist
    //         return BadRequest(new { message = "Password reset failed. Please try again or request a new link." });
    //     }
    //
    //     string decodedToken;
    //     try
    //     {
    //         decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
    //     }
    //     catch (FormatException)
    //     {
    //         return BadRequest(new { message = "Invalid token format. Password reset failed." });
    //     }
    //
    //     var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);
    //     if (result.Succeeded)
    //     {
    //         // Optionally, revoke all refresh tokens for the user upon password reset for security
    //         var userRefreshTokens = await _context.RefreshTokens
    //                                               .Where(rt => rt.UserId == user.Id && !rt.IsRevoked && !rt.IsUsed)
    //                                               .ToListAsync();
    //         foreach (var rt in userRefreshTokens) rt.IsRevoked = true;
    //         _context.RefreshTokens.UpdateRange(userRefreshTokens);
    //         await _context.SaveChangesAsync();
    //
    //         return Ok(new { message = "Password has been reset successfully." });
    //     }
    //
    //     return BadRequest(new { message = "Password reset failed.", errors = result.Errors.Select(e => e.Description) });
    // }

    [HttpPost("revoke-token")] // Revokes current user's refresh tokens
    [Authorize]
    public async Task<IActionResult> RevokeToken()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var userRefreshTokens = await _context.RefreshTokens
                                              .Where(rt => rt.UserId == userId && !rt.IsRevoked && !rt.IsUsed)
                                              .ToListAsync();

        if (!userRefreshTokens.Any())
        {
            return Ok(new { message = "No active refresh tokens to revoke." });
        }

        foreach (var token in userRefreshTokens)
        {
            token.IsRevoked = true; // Or token.IsUsed = true; if you prefer to mark them used
        }

        _context.RefreshTokens.UpdateRange(userRefreshTokens);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Refresh tokens revoked." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        var is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);

        return Ok(new
                  {
                      user.Id, user.UserName, user.Email, user.EmailConfirmed, Roles = roles, TwoFactorEnabled = is2faEnabled
                  });
    }
}