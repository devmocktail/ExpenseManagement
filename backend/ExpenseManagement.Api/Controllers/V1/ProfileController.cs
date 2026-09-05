using System.Text;
using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Features.Auth;
using ExpenseManagement.Application.Features.Profile;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The signed-in account: its profile, its preferences, its password, its
/// closure and its data export.
///
/// Every route here means "me". Nothing takes a user id, because
/// <see cref="IProfileService"/> and <see cref="IAuthService"/> resolve the
/// caller from the authenticated principal — an id in a route or a body would be
/// an attacker-chosen target rather than an identity.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profile")]
[Produces("application/json")]
public class ProfileController(
    IProfileService profileService,
    IAuthService authService,
    IDateTimeProvider clock) : ControllerBase
{
    /// <summary>Returns the signed-in user's profile, including role membership.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Get()
    {
        var profile = await profileService.GetAsync(HttpContext.RequestAborted);

        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    /// <summary>Updates the signed-in user's display name.</summary>
    /// <param name="request">The name to store. The email address is not settable here.</param>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Update(
        [FromBody] UpdateProfileRequest request)
    {
        var profile = await profileService.UpdateAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Profile updated successfully"));
    }

    /// <summary>
    /// Changes the signed-in user's password and signs every device out,
    /// including this one — on success the client is expected to send the user
    /// back to sign-in.
    /// </summary>
    /// <param name="request">The current password, the new one and its confirmation.</param>
    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        // Changing a password lives on IAuthService rather than IProfileService
        // because it has to revoke every refresh-token family — session state
        // this feature does not own.
        await authService.ChangePasswordAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse.Ok("Password changed successfully"));
    }

    /// <summary>
    /// Returns the signed-in user's settings. An account with no settings row
    /// answers with the defaults rather than a 404.
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(ApiResponse<UserSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserSettingsDto>>> GetSettings()
    {
        var settings = await profileService.GetSettingsAsync(HttpContext.RequestAborted);

        return Ok(ApiResponse<UserSettingsDto>.Ok(settings));
    }

    /// <summary>
    /// Applies the settings present on the request, leaves the rest alone and
    /// returns the full merged settings.
    /// </summary>
    /// <param name="request">
    /// A partial settings document — the settings screen sends only what moved,
    /// and an omitted member means "leave this alone".
    /// </param>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(ApiResponse<UserSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<UserSettingsDto>>> UpdateSettings(
        [FromBody] UpdateSettingsRequest request)
    {
        var settings = await profileService.UpdateSettingsAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse<UserSettingsDto>.Ok(settings, "Settings updated successfully"));
    }

    /// <summary>
    /// Closes the signed-in user's account after re-verifying the password:
    /// soft-deletes the user, revokes every refresh token and deactivates every
    /// push device.
    /// </summary>
    /// <param name="request">
    /// Carries the caller's current password. It travels in the body rather than
    /// the query string so it never lands in an access log or a proxy cache.
    /// </param>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse>> DeleteAccount(
        [FromBody] DeleteAccountRequest request)
    {
        await profileService.DeleteAccountAsync(request.Password, HttpContext.RequestAborted);

        return Ok(ApiResponse.Ok("Account deleted successfully"));
    }

    /// <summary>
    /// Exports the signed-in user's transactions in the half-open range
    /// <c>[from, to)</c> as a downloadable CSV or JSON file.
    /// </summary>
    /// <param name="request">The format and the optional date bounds, from the query string.</param>
    [HttpGet("export")]
    // Overrides the controller's [Produces("application/json")]: this action does
    // not negotiate, it answers in whichever media type the format asked for.
    [Produces("text/csv", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ExportTransactions(
        [FromQuery] ExportTransactionsRequest request)
    {
        var content = await profileService.ExportTransactionsAsync(
            request.Format,
            request.From,
            request.To,
            HttpContext.RequestAborted);

        // The one endpoint that deliberately breaks the envelope convention. What
        // comes back is a file the client saves or hands to a share sheet, so the
        // body has to be exactly the CSV or JSON document: an envelope would force
        // every consumer to unwrap and re-encode before the bytes were usable, and
        // JSON-escaping a CSV into a string field would be worse still. Failures
        // are unaffected — the exception middleware writes the envelope before any
        // of this runs, so the client's error path stays uniform.
        //
        // The charset is spelled out because text/* defaults to US-ASCII, which
        // would mis-decode any non-ASCII merchant, category or note.
        var contentType = $"{TransactionExport.ContentType(request.Format)}; charset=utf-8";

        // File(...) rather than Content(...): it is what writes the
        // Content-Disposition attachment filename the client saves under.
        return File(
            Encoding.UTF8.GetBytes(content),
            contentType,
            TransactionExport.FileName(request.Format, clock.UtcNow));
    }
}
