using System.Security.Claims;
using Injaz.Api.Dtos;
using Injaz.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Injaz.Api.Services;

public static class Roles
{
    public const string Manager = nameof(UserRole.Manager);
    public const string Employee = nameof(UserRole.Employee);
    public const string Super = nameof(UserRole.Super);

    /// <summary>من يدير المهام: المدير ومدير النظام.</summary>
    public const string TaskManagers = Manager + "," + Super;
}

public static class AppClaimTypes
{
    public const string Name = "name";
    public const string Role = "role";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("رمز الدخول لا يحتوي على هوية المستخدم.");
    }

    /// <summary>مدير أو مدير نظام (صلاحيات إدارة المهام).</summary>
    public static bool IsManager(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.Manager) || principal.IsInRole(Roles.Super);

    public static bool IsSuper(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Super);
}

public static class UserFactory
{
    private static readonly string[] Colors = ["#0f766e", "#7c3aed", "#c2410c", "#be185d", "#2563eb", "#059669"];
    private static readonly PasswordHasher<User> Hasher = new();

    public static string HashPassword(User user, string password) => Hasher.HashPassword(user, password);

    public static bool VerifyPassword(User user, string password) =>
        Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public static string MakeInitials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]));

    public static string PickColor(int index) => Colors[Math.Abs(index) % Colors.Length];

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static UserDto ToDto(this User u) => new(u.Id, u.Name, u.Email, u.Role, u.Title, u.Initials, u.Color);
}
