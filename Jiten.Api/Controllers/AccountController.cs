using System.Text;
using System.Text.Encodings.Web;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Core.Data.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Jiten.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly UrlEncoder _urlEncoder;

    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _urlEncoder = urlEncoder;
    }

    // [HttpPost("change-password")]
    // public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
    // {
    //     if (!ModelState.IsValid) return BadRequest(ModelState);
    //
    //     var user = await _userManager.GetUserAsync(User);
    //     if (user == null) return Unauthorized("User not found.");
    //
    //     var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
    //     if (!changePasswordResult.Succeeded)
    //     {
    //         return BadRequest(new
    //                           {
    //                               message = "Failed to change password.", errors = changePasswordResult.Errors.Select(e => e.Description)
    //                           });
    //     }
    //
    //     // Optional: Sign the user out or refresh their session/tokens
    //     // await _signInManager.RefreshSignInAsync(user); // If using cookies
    //     // For JWT, client might need to re-login or you might issue new tokens immediately,
    //     // but generally not required just for password change if tokens are still valid.
    //
    //     return Ok(new { message = "Password changed successfully." });
    // }
    //
    // [HttpGet("2fa/setup")]
    // public async Task<IActionResult> SetupAuthenticator()
    // {
    //     var user = await _userManager.GetUserAsync(User);
    //     if (user == null) return Unauthorized("User not found.");
    //
    //     // Load the user's authenticator key and generate a new one if it doesn't exist
    //     var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
    //     if (string.IsNullOrEmpty(unformattedKey))
    //     {
    //         await _userManager.ResetAuthenticatorKeyAsync(user);
    //         unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
    //     }
    //
    //     var sharedKey = FormatKey(unformattedKey); // Helper to format the key nicely
    //     var authenticatorUri = GenerateQrCodeUri(user.Email, unformattedKey);
    //
    //     // Optionally, generate recovery codes if user doesn't have them.
    //     // var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
    //
    //     return Ok(new AuthenticatorResponse
    //               {
    //                   SharedKey = sharedKey, AuthenticatorUri = authenticatorUri,
    //                   // RecoveryCodes = recoveryCodes // Send if generated
    //               });
    // }
    //
    // [HttpPost("2fa/enable")]
    // public async Task<IActionResult> EnableAuthenticator([FromBody] EnableAuthenticatorRequest model)
    // {
    //     var user = await _userManager.GetUserAsync(User);
    //     if (user == null) return Unauthorized("User not found.");
    //
    //     var verificationCode = model.VerificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
    //
    //     // "Authenticator" is the token provider name for TOTP apps
    //     var is2faTokenValid =
    //         await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);
    //
    //     if (!is2faTokenValid)
    //     {
    //         return BadRequest(new { message = "Invalid verification code." });
    //     }
    //
    //     await _userManager.SetTwoFactorEnabledAsync(user, true);
    //     _logger.LogInformation("User with ID '{UserId}' has enabled 2FA.", user.Id);
    //
    //     // It's good practice to generate recovery codes when 2FA is enabled
    //     if (await _userManager.CountRecoveryCodesAsync(user) == 0)
    //     {
    //         var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10); // Generate 10 codes
    //         return Ok(new { message = "2FA has been enabled. Please save these recovery codes.", recoveryCodes });
    //     }
    //
    //     return Ok(new { message = "2FA has been enabled successfully." });
    // }
    //
    // [HttpPost("2fa/disable")]
    // public async Task<IActionResult> DisableAuthenticator()
    // {
    //     var user = await _userManager.GetUserAsync(User);
    //     if (user == null) return Unauthorized("User not found.");
    //
    //     if (!await _userManager.GetTwoFactorEnabledAsync(user))
    //     {
    //         return BadRequest(new { message = "2FA is not currently enabled." });
    //     }
    //
    //     var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
    //     if (!disable2faResult.Succeeded)
    //     {
    //         return BadRequest(new { message = "Failed to disable 2FA.", errors = disable2faResult.Errors.Select(e => e.Description) });
    //     }
    //
    //     await _userManager.ResetAuthenticatorKeyAsync(user); // Also reset the key
    //     _logger.LogInformation("User with ID '{UserId}' has disabled 2FA.", user.Id);
    //     return Ok(new { message = "2FA has been disabled successfully." });
    // }
    //
    // [HttpGet("2fa/recovery-codes")]
    // public async Task<IActionResult> GetRecoveryCodes()
    // {
    //     var user = await _userManager.GetUserAsync(User);
    //     if (user == null) return Unauthorized("User not found.");
    //
    //     if (!await _userManager.GetTwoFactorEnabledAsync(user))
    //     {
    //         return BadRequest(new { message = "2FA is not enabled for this account." });
    //     }
    //
    //     var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
    //     if (recoveryCodes == null || !recoveryCodes.Any())
    //     {
    //         return StatusCode(500, "Failed to generate recovery codes.");
    //     }
    //
    //     // Frontend should prompt user to save these securely and acknowledge they have.
    //     return Ok(new { recoveryCodes });
    // }

    private string FormatKey(string unformattedKey)
    {
        // Formats the key into groups of 4 characters for easier manual entry
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private string GenerateQrCodeUri(string email, string unformattedKey)
    {
        // The issuer should ideally be your application name, URL-encoded
        var issuer = _urlEncoder.Encode("YourAppName"); // Get from config if possible
        var userIdentifier = _urlEncoder.Encode(email); // Or username

        return string.Format(
                             AuthenticatorUriFormat,
                             issuer,
                             userIdentifier,
                             unformattedKey);
    }
}