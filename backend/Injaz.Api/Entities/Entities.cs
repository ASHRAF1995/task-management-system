namespace Injaz.Api.Entities;

/// <summary>Super: مدير النظام — يدير الحسابات وله كل صلاحيات المدير.</summary>
public enum UserRole { Manager, Employee, Super }

public enum TaskItemStatus { Todo, Progress, Review, Done }

public enum TaskPriority { High, Medium, Low }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Employee;
    public string Title { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Color { get; set; } = "#2563eb";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TaskAssignment> Assignments { get; set; } = [];
}

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<TaskAssignment> Assignments { get; set; } = [];
    public List<WorkLog> Logs { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];
    public List<TaskActivity> Activities { get; set; } = [];
}

public class TaskAssignment
{
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class WorkLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Text { get; set; } = "";
    public decimal Hours { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    /// <summary>اسم الملف المخزَّن على القرص (نسبي لمجلد الرفع).</summary>
    public string StoredName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>سجل نشاط المهمة (إنشاء، تعديل، تغيير حالة، إسناد...).</summary>
public class TaskActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Text { get; set; } = "";
    public string Type { get; set; } = "update";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>يحفظ الإشعارات التي قرأها كل مستخدم.</summary>
public class NotificationRead
{
    public Guid UserId { get; set; }
    public Guid TaskId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
