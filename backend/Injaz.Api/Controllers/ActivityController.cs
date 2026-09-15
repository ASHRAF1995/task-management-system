using Injaz.Api.Data;
using Injaz.Api.Dtos;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/activity")]
public class ActivityController(AppDbContext db) : ControllerBase
{
    /// <summary>آخر النشاطات على المهام المتاحة للمستخدم.</summary>
    [HttpGet]
    public async Task<List<ActivityDto>> GetRecent([FromQuery] int take = 10, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var query = db.TaskActivities.AsNoTracking();
        if (!User.IsManager())
        {
            var me = User.GetUserId();
            query = query.Where(a => a.Task!.Assignments.Any(x => x.UserId == me));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new ActivityDto(a.Id, a.TaskId, a.Task!.Title, a.UserId, a.User!.Name, a.Text, a.Type, a.CreatedAt))
            .ToListAsync(ct);
    }
}
