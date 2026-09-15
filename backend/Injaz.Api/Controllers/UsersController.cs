using Injaz.Api.Data;
using Injaz.Api.Dtos;
using Injaz.Api.Entities;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    /// <summary>قائمة المستخدمين الفعّالين (مدير النظام، ثم المديرون، ثم الموظفون).</summary>
    [HttpGet]
    public async Task<List<UserDto>> GetAll(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Role == UserRole.Super ? 0 : u.Role == UserRole.Manager ? 1 : 2)
            .ThenBy(u => u.CreatedAt)
            .ToListAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }

    /// <summary>إنشاء حساب مدير أو موظف (لمدير النظام فقط).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Super)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var role = request.Role ?? UserRole.Employee;
        if (role == UserRole.Super)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "لا يمكن إنشاء حساب مدير نظام من هنا.");

        var email = UserFactory.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "هذا البريد الإلكتروني مسجل بالفعل.");

        var count = await db.Users.CountAsync(ct);
        var name = request.Name.Trim();
        var user = new User
        {
            Name = name,
            Title = request.Title.Trim(),
            Email = email,
            Role = role,
            Initials = UserFactory.MakeInitials(name),
            Color = UserFactory.PickColor(count),
        };
        user.PasswordHash = UserFactory.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), null, user.ToDto());
    }

    /// <summary>تعديل حساب (لمدير النظام فقط). كلمة المرور والدور اختياريان.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Super)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null) return Problem(statusCode: StatusCodes.Status404NotFound, title: "الحساب غير موجود.");

        var email = UserFactory.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Id != id && u.Email == email && !u.IsDeleted, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "هذا البريد الإلكتروني مسجل بالفعل.");

        if (request.Role is { } newRole && newRole != user.Role)
        {
            if (user.Role == UserRole.Super || newRole == UserRole.Super)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "لا يمكن تغيير دور مدير النظام.");

            // لو الموظف اتحوّل لمدير، نشيل إسناد مهامه لأن المهام تُسند للموظفين فقط
            if (user.Role == UserRole.Employee)
                await RemoveAssignmentsAsync(user, "تغيير دوره إلى مدير", ct);

            user.Role = newRole;
        }

        user.Name = request.Name.Trim();
        user.Title = request.Title.Trim();
        user.Email = email;
        user.Initials = UserFactory.MakeInitials(user.Name);
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = UserFactory.HashPassword(user, request.Password);

        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    /// <summary>حذف حساب مدير أو موظف وإلغاء إسناد مهامه (حذف منطقي للحفاظ على السجل).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Super)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == User.GetUserId())
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "لا يمكنك حذف حسابك الحالي.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null) return Problem(statusCode: StatusCodes.Status404NotFound, title: "الحساب غير موجود.");
        if (user.Role == UserRole.Super)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "لا يمكن حذف حساب مدير النظام.");

        await RemoveAssignmentsAsync(user, "حذف الحساب", ct);
        user.IsDeleted = true;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task RemoveAssignmentsAsync(User user, string reason, CancellationToken ct)
    {
        var assignments = await db.TaskAssignments.Where(a => a.UserId == user.Id).ToListAsync(ct);
        if (assignments.Count == 0) return;

        db.TaskAssignments.RemoveRange(assignments);
        var me = User.GetUserId();
        foreach (var a in assignments)
        {
            db.TaskActivities.Add(new TaskActivity
            {
                TaskId = a.TaskId,
                UserId = me,
                Text = $"ألغى إسناد المهمة عن {user.Name} ({reason})",
                Type = "update",
            });
        }
    }
}
