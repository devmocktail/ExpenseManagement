using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// Account lifecycle and session issuance: registration, sign-in, refresh-token
/// rotation, sign-out and the password-reset pair.
/// </summary>
/// <remarks>
/// Changing a known password is not here — the client sends that to
/// <c>PUT /profile/password</c>, because it is an authenticated profile
/// operation rather than a way to obtain a session.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates an account and signs the new user straight in.</summary>
    /// <param name="request">The sign-up details. The password is confirmed twice and the terms must be accepted.</param>
    /// <returns>The token pair and the new user's profile.</returns>
    [HttpPost("register")]
    // Sign-up is by definition reachable without a token.
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request, CurrentClient(), HttpContext.RequestAborted);

        // 200 rather than 201 + CreatedAtAction: this controller exposes no
        // GetById to point a Location header at, and the account itself is not
        // addressable here — the client reads the session out of the body.
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Account created successfully"));
    }

    /// <summary>Verifies credentials and starts a new refresh-token family.</summary>
    /// <param name="request">The email address and password to authenticate.</param>
    /// <returns>The token pair and the signed-in user's profile.</returns>
    [HttpPost("login")]
    // Signing in is the act of obtaining a token, so it cannot require one.
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    // Bad credentials, a locked account and an unknown address all arrive as the
    // same 422 with one message, so a caller cannot enumerate registered emails.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request, CurrentClient(), HttpContext.RequestAborted);

        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Signed in successfully"));
    }

    /// <summary>Exchanges a refresh token for its successor in the same family.</summary>
    /// <param name="request">The refresh token issued by the previous sign-in or rotation.</param>
    /// <returns>A fresh token pair. The presented refresh token stops working.</returns>
    [HttpPost("refresh")]
    // The access token is expected to be expired at this point, so requiring a
    // valid one would make the whole rotation unusable.
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokensDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthTokensDto>>> Refresh(
        [FromBody] RefreshRequest request)
    {
        var result = await authService.RefreshAsync(request, CurrentClient(), HttpContext.RequestAborted);

        return Ok(ApiResponse<AuthTokensDto>.Ok(result, "Session refreshed successfully"));
    }

    /// <summary>Revokes the presented refresh token and every token in its family.</summary>
    /// <param name="request">The refresh token identifying the session to end.</param>
    /// <returns>Success, including when the token was already unknown or spent.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> Logout([FromBody] LogoutRequest request)
    {
        await authService.LogoutAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse.Ok("Signed out successfully"));
    }

    /// <summary>Sends a password-reset token to the address when it belongs to an active account.</summary>
    /// <param name="request">The email address to send the reset token to.</param>
    /// <returns>Success in every case.</returns>
    [HttpPost("forgot-password")]
    // Someone who has forgotten their password has no token to present.
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    // Deliberate anti-enumeration behaviour: this always answers 200 with the
    // same message whether or not the address is registered. A 404 here would
    // turn the endpoint into a free membership oracle for any email list.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request)
    {
        await authService.ForgotPasswordAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse.Ok(
            "If that email address has an account, a reset link is on its way."));
    }

    /// <summary>Consumes a reset token, sets the new password and signs out every device.</summary>
    /// <param name="request">The email address, the token from the reset email, and the new password twice.</param>
    /// <returns>Success once the password has been changed.</returns>
    [HttpPost("reset-password")]
    // The caller is locked out by definition, so this endpoint has to be
    // reachable with no token; the emailed reset token is the credential.
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    // An expired, spent or wrong token is a 422 with one generic message, for
    // the same reason the login failures are indistinguishable.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        await authService.ResetPasswordAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse.Ok("Password reset successfully. Please sign in again."));
    }

    /// <summary>
    /// Transport facts about the calling connection, for the session rows a user
    /// is shown in their device list.
    /// </summary>
    /// <remarks>
    /// Read from the connection and the headers rather than from the request
    /// body, so neither value is attacker-chosen. Both are handed to the service
    /// and never logged here or echoed back in a response.
    /// </remarks>
    private ClientContext CurrentClient()
    {
        var userAgent = Request.Headers.UserAgent.ToString();

        return new ClientContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            // An absent header binds to an empty string; null records "unknown"
            // honestly instead of storing a blank that looks like a real value.
            string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
    }
}
