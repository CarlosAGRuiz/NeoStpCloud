using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
[Route("Account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MfaController(IAuthService auth, IMfaService mfa, ICurrentUser currentUser) : Controller
{
    private const string EnrollmentView = "~/Views/Account/MfaEnrollment.cshtml";
    private const string VerificationView = "~/Views/Account/MfaVerification.cshtml";

    [HttpGet("MfaEnrollment")]
    [AllowMfaChallenge(SessionClaims.MfaEnroll)]
    public IActionResult Enrollment() => View(EnrollmentView, new MfaViewModel());

    [HttpPost("MfaBegin")]
    [ValidateAntiForgeryToken]
    [AllowMfaChallenge(SessionClaims.MfaEnroll)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    public async Task<IActionResult> Begin(CancellationToken ct)
    {
        if (currentUser.UserId is not int userId) return Unauthorized();
        var result = await mfa.IniciarEnrolamientoAsync(userId, ct);
        if (result.IsFailure) ModelState.AddModelError(string.Empty, result.Error!);
        return View(EnrollmentView, new MfaViewModel { Enrollment = result.Value });
    }

    [HttpPost("MfaConfirm")]
    [ValidateAntiForgeryToken]
    [AllowMfaChallenge(SessionClaims.MfaEnroll)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    public async Task<IActionResult> Confirm([Bind(nameof(MfaViewModel.Code))] MfaViewModel model, CancellationToken ct)
    {
        if (currentUser.UserId is not int userId) return Unauthorized();
        if (!ModelState.IsValid) return View(EnrollmentView, new MfaViewModel());
        var result = await mfa.ConfirmarEnrolamientoAsync(userId, model.Code.Trim(), Context(), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(EnrollmentView, new MfaViewModel());
        }
        await auth.LogoutAsync(null, Context(), ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Recovery codes exist only in this no-store response, not TempData/session/URL.
        return View("~/Views/Account/MfaRecovery.cshtml", result.Value);
    }

    [HttpGet("MfaVerification")]
    [AllowMfaChallenge(SessionClaims.MfaVerify)]
    public IActionResult Verification() => View(VerificationView, new MfaViewModel());

    [HttpPost("MfaVerification")]
    [ValidateAntiForgeryToken]
    [AllowMfaChallenge(SessionClaims.MfaVerify)]
    [EnableRateLimiting(AuthRateLimiting.Mfa)]
    public async Task<IActionResult> Verify([Bind(nameof(MfaViewModel.Code))] MfaViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(VerificationView, new MfaViewModel());
        var result = await auth.VerifyMfaChallengeAsync(model.Code.Trim(), Context(), ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(VerificationView, new MfaViewModel());
        }
        await SessionCookieSignIn.SignInAsync(HttpContext, result.Value!.User);
        return Redirect("/");
    }

    private AuthContext Context() => new()
    {
        SessionId = Guid.TryParse(User.FindFirstValue(SessionClaims.Id), out var id) ? id : null,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(), TraceId = HttpContext.TraceIdentifier
    };
}
