using Injaz.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // كل التواريخ تُخزَّن بتوقيت UTC وتُقرأ بعلامة Utc حتى تظهر صحيحة في المتصفح.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.Initials).HasMaxLength(10);
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            // البريد فريد بين الحسابات الفعّالة فقط (الحسابات المحذوفة لا تمنع إعادة استخدامه)
            // ملاحظة: لا نستخدم Global Query Filter حتى تبقى سجلات الحسابات المحذوفة ظاهرة في تاريخ المهام.
            e.HasIndex(x => x.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskAssignment>(e =>
        {
            e.HasKey(x => new { x.TaskId, x.UserId });
            e.HasOne(x => x.Task).WithMany(t => t.Assignments).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.Assignments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WorkLog>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Hours).HasPrecision(6, 2);
            e.HasOne(x => x.Task).WithMany(t => t.Logs).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Attachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.StoredName).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Task).WithMany(t => t.Attachments).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskActivity>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Type).HasMaxLength(30);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Task).WithMany(t => t.Activities).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<NotificationRead>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TaskId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
