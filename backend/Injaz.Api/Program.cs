using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Injaz.Api.Data;
using Injaz.Api.Infrastructure;
using Injaz.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- الإعدادات ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("يجب ضبط Jwt:Key بمفتاح طوله 32 بايت على الأقل (appsettings أو user-secrets أو متغير بيئة).");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));

// ---------- قاعدة البيانات ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ---------- المصادقة والصلاحيات ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = AppClaimTypes.Name,
            RoleClaimType = AppClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            // نرفض الرمز إذا حُذف الحساب أو تغيّر دوره بعد إصدار الرمز
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var id = principal.GetUserId();
                var role = principal.FindFirst(AppClaimTypes.Role)?.Value;
                var user = await db.Users.AsNoTracking()
                    .Where(u => u.Id == id && !u.IsDeleted)
                    .Select(u => new { u.Role })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                if (user is null || user.Role.ToString() != role)
                    context.Fail("الحساب غير فعّال.");
            },
        };
    });
builder.Services.AddAuthorization();

// ---------- الخدمات ----------
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<FileStorage>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // رسالة أوضح عند أخطاء التحقق
        o.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                // نعرض أول رسالة عربية (رسائلنا)، وإلا رسالة عامة بدل رسائل الـ JSON الإنجليزية
                Title = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                            .FirstOrDefault(m => m.Any(c => c is >= '\u0600' and <= '\u06FF')) ?? "البيانات المرسلة غير صحيحة.",
            };
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

// ---------- تهيئة قاعدة البيانات ----------
try
{
    var initialAdmin = app.Configuration.GetSection(InitialAdminOptions.SectionName).Get<InitialAdminOptions>() ?? new();
    await DbSeeder.InitializeAsync(app.Services, initialAdmin);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex,
        "تعذّر تهيئة قاعدة البيانات. تأكد إن SQL Server شغّال وإن ConnectionStrings:Default في appsettings.json صحيح.");
    throw;
}

// ---------- خط معالجة الطلبات ----------
if (app.Environment.IsDevelopment())
{
    // في التطوير تظهر تفاصيل الخطأ كاملة (Developer Exception Page)
    app.MapOpenApi();
    app.MapScalarApiReference(); // واجهة تجربة الـ API على /scalar/v1
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseStatusCodePages();

// الواجهة (نسخة Vite المبنية) بتتقدّم من wwwroot على نفس الموقع
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow })).AllowAnonymous();

app.Run();
