using Injaz.Api.Entities;
using Injaz.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Data;

/// <summary>يجهّز قاعدة البيانات ويتأكد من وجود حساب مدير النظام (Super). لا يضيف أي بيانات تجريبية.</summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services, InitialAdminOptions admin, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // إذا وُجدت Migrations في المشروع نطبّقها، وإلا ننشئ الجداول مباشرة من النموذج.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Super && !u.IsDeleted, ct)) return;

        var email = UserFactory.NormalizeEmail(admin.Email);
        if (string.IsNullOrWhiteSpace(admin.Password))
        {
            if (!await db.Users.AnyAsync(ct))
                throw new InvalidOperationException("قاعدة البيانات فارغة: اضبط InitialAdmin:Password لإنشاء حساب مدير النظام الأول.");
            logger.LogWarning("لا يوجد حساب مدير نظام. اضبط InitialAdmin:Password وأعد التشغيل لإنشاء {Email}.", email);
            return;
        }

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
        if (existing is not null)
        {
            // البريد مستخدم بالفعل: نرقّي الحساب لمدير نظام بدل إنشاء حساب مكرر
            existing.Role = UserRole.Super;
            await db.TaskAssignments.Where(a => a.UserId == existing.Id).ExecuteDeleteAsync(ct);
        }
        else
        {
            var user = new User
            {
                Name = admin.Name,
                Title = admin.Name,
                Email = email,
                Role = UserRole.Super,
                Initials = UserFactory.MakeInitials(admin.Name),
                Color = "#0f172a",
            };
            user.PasswordHash = UserFactory.HashPassword(user, admin.Password);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("تم تجهيز حساب مدير النظام {Email}.", email);
    }
}
