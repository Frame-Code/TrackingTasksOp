using Application.Ports.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AttachmentController(IAttachmentService attachmentService) : ControllerBase
{
    [HttpGet("{id}/content")]
    public async Task<IActionResult> GetContent(int id)
    {
        try
        {
            var (content, contentType, fileName) = await attachmentService.GetAttachmentContentAsync(id);
            return File(content, contentType, fileName);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("work-package/{wpId}")]
    public async Task<IActionResult> GetByWorkPackage(int wpId)
    {
        var attachments = await attachmentService.GetAttachmentsAsync(wpId);
        return Ok(attachments);
    }
}
