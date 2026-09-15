namespace Injaz.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Injaz";
    public string Audience { get; set; } = "Injaz.Client";
    /// <summary>مفتاح التوقيع — 32 حرفاً على الأقل. لا تضعه في Git في بيئة الإنتاج.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;
}

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string UploadsPath { get; set; } = "App_Data/uploads";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] BlockedExtensions { get; set; } = [".exe", ".bat", ".cmd", ".com", ".msi", ".ps1", ".sh", ".dll", ".js", ".vbs"];
}

public class InitialAdminOptions
{
    public const string SectionName = "InitialAdmin";

    /// <summary>بريد حساب مدير النظام الذي يُنشأ تلقائياً لو مفيش مدير نظام.</summary>
    public string Email { get; set; } = "super@injaz.local";
    public string Name { get; set; } = "مدير النظام";
    /// <summary>كلمة المرور الأولى — تُستخدم مرة واحدة عند الإنشاء فقط.</summary>
    public string? Password { get; set; }
}

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>عدد الأيام قبل الاستحقاق التي يبدأ عندها التنبيه.</summary>
    public int DueSoonDays { get; set; } = 3;
}
