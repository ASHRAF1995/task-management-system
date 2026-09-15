using Injaz.Api.Data;
using Injaz.Api.Dtos;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    /// <summary>تسجيل الدخول والحصول على رمز JWT.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = UserFactory.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);

        if (user is null || !UserFactory.VerifyPassword(user, request.Password))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "البريد الإلكتروني أو كلمة المرور غير صحيحة.");

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    /// <summary>بيانات المستخدم الحالي.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        return user is null ? Unauthorized() : user.ToDto();
    }
}
