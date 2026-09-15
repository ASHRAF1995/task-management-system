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
[Route("api/tasks")]
public class TasksController(AppDbContext db, FileStorage storage, IOptions<StorageOptions> storageOptions) : ControllerBase
{
    private static readonly Dictionary<TaskItemStatus, string> StatusLabels = new()
    {
        [TaskItemStatus.Todo] = "لم تبدأ",
        [TaskItemStatus.Progress] = "قيد التنفيذ",
        [TaskItemStatus.Review] = "بانتظار المراجعة",
        [TaskItemStatus.Done] = "مكتملة",
    };

    /// <summary>المهام المتاحة للمستخدم: المدير يرى الكل، والموظف يرى المسندة إليه فقط.</summary>
    [HttpGet]
    public async Task<List<TaskSummaryDto>> GetAll([FromQuery] TaskItemStatus? status, [FromQuery] string? search, CancellationToken ct)
    {
        var query = VisibleTasks().AsNoTracking();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TaskSummaryDto(
                t.Id, t.Title, t.Description, t.Priority, t.Status, t.StartDate, t.DueDate,
                t.Assignments.Select(a => a.UserId).ToList(),
                t.CreatedById, t.CreatedAt, t.UpdatedAt,
                t.Logs.Sum(l => (decimal?)l.Hours) ?? 0,
                t.Attachments.Count))
            .ToListAsync(ct);
    }

    /// <summary>تفاصيل مهمة كاملة مع سجل العمل والمرفقات والنشاط.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDetailsDto>> Get(Guid id, CancellationToken ct)
    {
        var task = await VisibleTasks().AsNoTracking()
            .Include(t => t.Assignments)
            .Include(t => t.Logs).ThenInclude(l => l.User)
            .Include(t => t.Attachments).ThenInclude(a => a.User)
            .Include(t => t.Activities).ThenInclude(a => a.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return task is null ? TaskNotFound() : ToDetails(task);
    }

    /// <summary>إنشاء مهمة جديدة وإسنادها (للمدير فقط).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.TaskManagers)]
    public async Task<ActionResult<TaskDetailsDto>> Create(TaskRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(request, ct);
        if (error is not null) return error;

        var me = User.GetUserId();
        var task = new TaskItem { CreatedById = me };
        Apply(task, request);
        task.Assignments.AddRange(request.AssigneeIds.Distinct().Select(uid => new TaskAssignment { UserId = uid }));
        task.Activities.Add(new TaskActivity { UserId = me, Text = "أنشأ المهمة", Type = "create" });

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        var created = await Get(task.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, created.Value);
    }

    /// <summary>تعديل تفاصيل المهمة والإسناد (للمدير فقط).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.TaskManagers)]
    public async Task<ActionResult<TaskDetailsDto>> Update(Guid id, TaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Assignments).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return TaskNotFound();

        var error = await ValidateAsync(request, ct);
        if (error is not null) return error;

        var me = User.GetUserId();
        var newIds = request.AssigneeIds.Distinct().ToList();
        var oldIds = task.Assignments.Select(a => a.UserId).ToList();
        var added = newIds.Except(oldIds).ToList();
        var removed = oldIds.Except(newIds).ToList();
        var oldStatus = task.Status;

        Apply(task, request);
        task.Assignments.RemoveAll(a => removed.Contains(a.UserId));
        task.Assignments.AddRange(added.Select(uid => new TaskAssignment { UserId = uid }));

        var names = await db.Users.Where(u => added.Contains(u.Id) || removed.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        AddActivity(task, me, added.Count > 0
            ? $"عدّل المهمة وأسندها إلى {string.Join("، ", added.Select(x => names[x]))}"
            : "عدّل تفاصيل المهمة");
        if (removed.Count > 0)
            AddActivity(task, me, $"ألغى إسناد المهمة عن {string.Join("، ", removed.Select(x => names.GetValueOrDefault(x, "حساب محذوف")))}");
        if (oldStatus != task.Status)
            AddActivity(task, me, $"غيّر الحالة من «{StatusLabels[oldStatus]}» إلى «{StatusLabels[task.Status]}»", "status");

        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>حذف مهمة مع كل سجلاتها ومرفقاتها (للمدير فقط).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.TaskManagers)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return TaskNotFound();

        var files = task.Attachments.Select(a => a.StoredName).ToList();
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        files.ForEach(storage.Delete);
        return NoContent();
    }

    /// <summary>تحديث حالة المهمة (المدير أو أحد المكلّفين).</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TaskDetailsDto>> UpdateStatus(Guid id, UpdateStatusRequest request, CancellationToken ct)
    {
        var task = await VisibleTasks().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return TaskNotFound();

        var newStatus = request.Status!.Value;
        if (task.Status != newStatus)
        {
            AddActivity(task, User.GetUserId(),
                $"غيّر الحالة من «{StatusLabels[task.Status]}» إلى «{StatusLabels[newStatus]}»", "status");
            task.Status = newStatus;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return await Get(id, ct);
    }

    /// <summary>إضافة سجل عمل بالساعات.</summary>
    [HttpPost("{id:guid}/logs")]
    public async Task<ActionResult<WorkLogDto>> AddLog(Guid id, CreateWorkLogRequest request, CancellationToken ct)
    {
        var task = await VisibleTasks().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return TaskNotFound();

        var me = User.GetUserId();
        var log = new WorkLog { TaskId = id, UserId = me, Text = request.Text.Trim(), Hours = request.Hours };
        db.WorkLogs.Add(log);
        AddActivity(task, me, $"أضاف سجل عمل ({request.Hours:0.##} ساعة): {log.Text}", "log");
        await db.SaveChangesAsync(ct);

        var userName = await db.Users.Where(u => u.Id == me).Select(u => u.Name).FirstAsync(ct);
        return new WorkLogDto(log.Id, me, userName, log.Text, log.Hours, log.CreatedAt);
    }

    /// <summary>رفع ملف أو أكثر كمرفقات للمهمة.</summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60 * 1024 * 1024)]
    public async Task<ActionResult<List<AttachmentDto>>> Upload(Guid id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var task = await VisibleTasks().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return TaskNotFound();
        if (files.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "اختر ملفاً واحداً على الأقل.");

        var opts = storageOptions.Value;
        foreach (var file in files)
        {
            if (file.Length == 0)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"الملف «{file.FileName}» فارغ.");
            if (file.Length > opts.MaxFileSizeBytes)
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: $"الملف «{file.FileName}» أكبر من الحد المسموح ({opts.MaxFileSizeBytes / 1024 / 1024} م.ب).");
            if (opts.BlockedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"نوع الملف «{file.FileName}» غير مسموح.");
        }

        var me = User.GetUserId();
        var saved = new List<Attachment>();
        foreach (var file in files)
        {
            var storedName = await storage.SaveAsync(file, ct);
            var fileName = Path.GetFileName(file.FileName);
            var attachment = new Attachment
            {
                TaskId = id,
                UserId = me,
                FileName = fileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes = file.Length,
                StoredName = storedName,
            };
            db.Attachments.Add(attachment);
            AddActivity(task, me, $"أرفق الملف «{fileName}»", "attachment");
            saved.Add(attachment);
        }

        await db.SaveChangesAsync(ct);
        var userName = await db.Users.Where(u => u.Id == me).Select(u => u.Name).FirstAsync(ct);
        return saved.Select(a => ToDto(a, userName)).ToList();
    }

    // ---------------- Helpers ----------------

    private IQueryable<TaskItem> VisibleTasks()
    {
        if (User.IsManager()) return db.Tasks;
        var me = User.GetUserId();
        return db.Tasks.Where(t => t.Assignments.Any(a => a.UserId == me));
    }

    private ObjectResult TaskNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "المهمة غير موجودة أو ليست لديك صلاحية عليها.");

    private async Task<ActionResult?> ValidateAsync(TaskRequest request, CancellationToken ct)
    {
        if (request.DueDate < request.StartDate)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "تاريخ الاستحقاق يجب أن يكون بعد تاريخ البدء.");

        var ids = request.AssigneeIds.Distinct().ToList();
        if (ids.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "اختر موظفاً واحداً على الأقل.");

        var validCount = await db.Users.CountAsync(u => ids.Contains(u.Id) && !u.IsDeleted && u.Role == UserRole.Employee, ct);
        if (validCount != ids.Count)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "بعض الموظفين المحددين غير موجودين.");

        return null;
    }

    private static void Apply(TaskItem task, TaskRequest r)
    {
        task.Title = r.Title.Trim();
        task.Description = r.Description.Trim();
        task.Priority = r.Priority!.Value;
        task.Status = r.Status!.Value;
        task.StartDate = r.StartDate!.Value;
        task.DueDate = r.DueDate!.Value;
        task.UpdatedAt = DateTime.UtcNow;
    }

    private void AddActivity(TaskItem task, Guid userId, string text, string type = "update") =>
        db.TaskActivities.Add(new TaskActivity { TaskId = task.Id, UserId = userId, Text = text, Type = type });

    private static AttachmentDto ToDto(Attachment a, string userName) =>
        new(a.Id, a.UserId, userName, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt, $"/api/attachments/{a.Id}");

    private static TaskDetailsDto ToDetails(TaskItem t) => new(
        t.Id, t.Title, t.Description, t.Priority, t.Status, t.StartDate, t.DueDate,
        t.Assignments.Select(a => a.UserId).ToList(),
        t.CreatedById, t.CreatedAt, t.UpdatedAt,
        t.Logs.OrderByDescending(l => l.CreatedAt)
            .Select(l => new WorkLogDto(l.Id, l.UserId, l.User?.Name ?? "", l.Text, l.Hours, l.CreatedAt)).ToList(),
        t.Attachments.OrderByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a, a.User?.Name ?? "")).ToList(),
        t.Activities.OrderByDescending(a => a.CreatedAt)
            .Select(a => new ActivityDto(a.Id, t.Id, t.Title, a.UserId, a.User?.Name ?? "", a.Text, a.Type, a.CreatedAt)).ToList());
}
