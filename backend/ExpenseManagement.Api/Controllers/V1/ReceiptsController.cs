using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Receipts;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The image evidence attached to the caller's own transactions.
///
/// No action takes a user id. <see cref="IReceiptService"/> resolves the owner
/// from the authenticated principal and reports someone else's receipt as 404
/// rather than 403, so this controller adds no ownership check of its own —
/// a second one here could only disagree with the first.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/receipts")]
[Produces("application/json")]
public class ReceiptsController(IReceiptService service) : ControllerBase
{
    /// <summary>
    /// Uploads an image and attaches it to one of the caller's transactions.
    /// </summary>
    /// <param name="form">The transaction id and the image, as multipart parts.</param>
    [HttpPost]
    [Consumes("multipart/form-data")]
    // Matches the multipart body limit in Program.cs: 10 MB of image plus
    // headroom for multipart framing. Refused at the transport, before a byte
    // reaches the validator or the disk.
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    // 413 is written by the server before MVC runs, so it is the one response
    // from this action that is not the envelope.
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ReceiptDto>>> Upload([FromForm] UploadReceiptForm form)
    {
        // Opened rather than copied to a buffer: the service streams these bytes
        // to storage, and a 10 MB photo has no business sitting in memory first.
        await using var content = form.File.OpenReadStream();

        var result = await service.UploadAsync(
            form.TransactionId,
            content,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            HttpContext.RequestAborted);

        // No explicit `version` route value: an explicitly-supplied value beats
        // the ambient one during link generation, so passing "1.0" emits a
        // Location of /api/v1.0/... while every other call the client makes is
        // /api/v1/.... Letting the ambient value carry through keeps the two
        // spellings identical.
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<ReceiptDto>.Ok(result, "Receipt created successfully"));
    }

    /// <summary>Gets the metadata for one of the caller's receipts.</summary>
    /// <param name="id">The receipt id.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReceiptDto>>> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id, HttpContext.RequestAborted);

        return Ok(ApiResponse<ReceiptDto>.Ok(result));
    }

    /// <summary>
    /// Streams the stored image itself. The only endpoint here that answers with
    /// raw bytes instead of the envelope, because an &lt;Image&gt; source has to
    /// receive an image; failures still arrive as JSON, written by the exception
    /// middleware outside MVC's content negotiation.
    /// </summary>
    /// <param name="id">The receipt id.</param>
    [HttpGet("{id:guid}/content")]
    [Produces("image/jpeg", "image/png", "image/webp", "image/heic")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid id)
    {
        var receipt = await service.GetContentAsync(id, HttpContext.RequestAborted);

        // private, never public: this is one user's receipt behind a bearer
        // token, so a shared proxy or CDN must never hold a copy it could hand
        // to the next caller. The device may cache it for an hour — the bytes
        // are immutable for the life of the receipt.
        Response.Headers.CacheControl = "private, max-age=3600";

        // FileStreamResult disposes the stream once the response is written.
        return File(receipt.Content, receipt.ContentType);
    }

    /// <summary>Deletes one of the caller's receipts and the stored image.</summary>
    /// <param name="id">The receipt id.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await service.DeleteAsync(id, HttpContext.RequestAborted);

        // 200 with the envelope, not 204: the client unwraps a body on every
        // response and a 204 has none.
        return Ok(ApiResponse.Ok("Receipt deleted successfully"));
    }
}

/// <summary>
/// The multipart body of a receipt upload.
///
/// Bound as one model rather than as two [FromForm] parameters: Swashbuckle
/// refuses to describe an action that mixes per-parameter [FromForm] with an
/// IFormFile, and fails the whole Swagger document rather than that one
/// operation — so the shape of this binding is load-bearing for the docs, not
/// just a style choice.
/// </summary>
public sealed class UploadReceiptForm
{
    /// <summary>The transaction the receipt belongs to. Ownership is checked server-side.</summary>
    [Required]
    public Guid TransactionId { get; set; }

    /// <summary>
    /// The image. A missing part fails model binding and is answered 400 by the
    /// configured invalid-model-state factory, so there is no null check in the
    /// action.
    /// </summary>
    [Required]
    public IFormFile File { get; set; } = null!;
}
