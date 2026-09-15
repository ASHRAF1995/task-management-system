using Injaz.Api.Data;
using Injaz.Api.Dtos;
using Injaz.Api.Entities;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Injaz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(AppDbContext db, IOptions<NotificationOptions> options) : ControllerBase
{
    private int DueSoonDays => Math.Max(0, options.Value.DueSoonDays);

    /// <summary>المهام المتأخرة أو التي يقترب موعدها ولم تكتمل.</summary>
    [HttpGet]
    public async Task<List<NotificationDto>> Get(CancellationToken ct)
    {
        var me = User.GetUserId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await PendingTasks(me, today.AddDays(DueSoonDays))
            .OrderBy(t => t.DueDate)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.DueDate,
                t.Status,
                IsRead = db.NotificationReads.Any(r => r.UserId == me && r.TaskId == t.Id),
            })
            .ToListAsync(ct);

        return items.Select(t => new NotificationDto(t.Id, t.Title, t.DueDate, t.Status, t.DueDate < today, t.IsRead)).ToList();
    }

    /// <summary>تعليم كل الإشعارات الحالية كمقروءة.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var me = User.GetUserId();
        var limit = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(DueSoonDays);
        var unread = await PendingTasks(me, limit)
            .Where(t => !db.NotificationReads.Any(r => r.UserId == me && r.TaskId == t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        db.NotificationReads.AddRange(unread.Select(id => new NotificationRead { UserId = me, TaskId = id }));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<TaskItem> PendingTasks(Guid me, DateOnly dueLimit)
    {
        var query = db.Tasks.AsNoTracking().Where(t => t.Status != TaskItemStatus.Done && t.DueDate <= dueLimit);
        if (!User.IsManager())
            query = query.Where(t => t.Assignments.Any(a => a.UserId == me));
        return query;
    }
}
