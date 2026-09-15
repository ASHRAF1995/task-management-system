using Injaz.Api.Data;
using Injaz.Api.Entities;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/attachments")]
public class AttachmentsController(AppDbContext db, FileStorage storage) : ControllerBase
{
    /// <summary>تنزيل مرفق (المدير أو أحد المكلّفين بالمهمة).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var attachment = await FindAccessibleAsync(id, ct);
        if (attachment is null) return NotFoundProblem();

        var stream = storage.OpenRead(attachment.StoredName);
        if (stream is null) return NotFoundProblem();

        return File(stream, attachment.ContentType, attachment.FileName);
    }

    /// <summary>حذف مرفق (المدير أو من رفع الملف).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var attachment = await FindAccessibleAsync(id, ct);
        if (attachment is null) return NotFoundProblem();

        var me = User.GetUserId();
        if (!User.IsManager() && attachment.UserId != me)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "يمكنك حذف الملفات التي رفعتها فقط.");

        db.Attachments.Remove(attachment);
        db.TaskActivities.Add(new TaskActivity
        {
            TaskId = attachment.TaskId,
            UserId = me,
            Text = $"حذف الملف «{attachment.FileName}»",
            Type = "attachment",
        });
        await db.SaveChangesAsync(ct);
        storage.Delete(attachment.StoredName);
        return NoContent();
    }

    private Task<Attachment?> FindAccessibleAsync(Guid id, CancellationToken ct)
    {
        var query = db.Attachments.Where(a => a.Id == id);
        if (!User.IsManager())
        {
            var me = User.GetUserId();
            query = query.Where(a => a.Task!.Assignments.Any(x => x.UserId == me));
        }
        return query.FirstOrDefaultAsync(ct);
    }

    private ObjectResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "الملف غير موجود.");
}
