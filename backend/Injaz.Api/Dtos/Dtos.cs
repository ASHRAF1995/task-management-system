using System.ComponentModel.DataAnnotations;
using Injaz.Api.Entities;

namespace Injaz.Api.Dtos;

// ---------- المصادقة ----------
public record LoginRequest(
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب."), EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")] string Email,
    [Required(ErrorMessage = "كلمة المرور مطلوبة.")] string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------- المستخدمون ----------
public record UserDto(Guid Id, string Name, string Email, UserRole Role, string Title, string Initials, string Color);

public record CreateUserRequest(
    [Required(ErrorMessage = "الاسم مطلوب."), MaxLength(100)] string Name,
    [Required(ErrorMessage = "المسمى الوظيفي مطلوب."), MaxLength(100)] string Title,
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب."), EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة."), MaxLength(256)] string Email,
    [Required(ErrorMessage = "كلمة المرور مطلوبة."), MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.")] string Password,
    UserRole? Role = UserRole.Employee);

public record UpdateUserRequest(
    [Required(ErrorMessage = "الاسم مطلوب."), MaxLength(100)] string Name,
    [Required(ErrorMessage = "المسمى الوظيفي مطلوب."), MaxLength(100)] string Title,
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب."), EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة."), MaxLength(256)] string Email,
    [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.")] string? Password,
    UserRole? Role = null);

// ---------- المهام ----------
public record TaskSummaryDto(
    Guid Id,
    string Title,
    string Description,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateOnly StartDate,
    DateOnly DueDate,
    List<Guid> Assignees,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal LoggedHours,
    int AttachmentsCount);

public record TaskDetailsDto(
    Guid Id,
    string Title,
    string Description,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateOnly StartDate,
    DateOnly DueDate,
    List<Guid> Assignees,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<WorkLogDto> Logs,
    List<AttachmentDto> Attachments,
    List<ActivityDto> History);

public record TaskRequest(
    [Required(ErrorMessage = "عنوان المهمة مطلوب."), MaxLength(200, ErrorMessage = "العنوان طويل جداً.")] string Title,
    [Required(ErrorMessage = "وصف المهمة مطلوب."), MaxLength(4000, ErrorMessage = "الوصف طويل جداً.")] string Description,
    [Required(ErrorMessage = "الأولوية مطلوبة.")] TaskPriority? Priority,
    [Required(ErrorMessage = "الحالة مطلوبة.")] TaskItemStatus? Status,
    [Required(ErrorMessage = "تاريخ البدء مطلوب.")] DateOnly? StartDate,
    [Required(ErrorMessage = "تاريخ الاستحقاق مطلوب.")] DateOnly? DueDate,
    [Required(ErrorMessage = "اختر موظفاً واحداً على الأقل.")] List<Guid> AssigneeIds);

public record UpdateStatusRequest(
    [Required(ErrorMessage = "الحالة مطلوبة.")] TaskItemStatus? Status);

// ---------- سجل العمل والمرفقات والنشاط ----------
public record WorkLogDto(Guid Id, Guid UserId, string UserName, string Text, decimal Hours, DateTime CreatedAt);

public record CreateWorkLogRequest(
    [Range(0.25, 24, ErrorMessage = "عدد الساعات يجب أن يكون بين 0.25 و24.")] decimal Hours,
    [Required(ErrorMessage = "اكتب وصفاً لما أنجزته."), MaxLength(1000)] string Text);

public record AttachmentDto(Guid Id, Guid UserId, string UserName, string Name, string Type, long Size, DateTime CreatedAt, string Url);

public record ActivityDto(Guid Id, Guid TaskId, string TaskTitle, Guid UserId, string UserName, string Text, string Type, DateTime CreatedAt);

// ---------- الإشعارات ----------
public record NotificationDto(Guid TaskId, string Title, DateOnly DueDate, TaskItemStatus Status, bool Overdue, bool IsRead);
