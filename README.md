# إنجاز — نظام إدارة المهام

نظام عربي (RTL) لإدارة مهام الفريق، ويتكون من جزأين:

- **الواجهة** (`/src`): JavaScript عادي مع Vite.
- **الخادم** (`/backend`): ASP.NET Core Web API على .NET 10 مع SQL Server (EF Core) ومصادقة JWT.

كل البيانات تُحفظ الآن في قاعدة البيانات. المرفقات تُحفظ على القرص في `backend/Injaz.Api/App_Data/uploads`، وكلمات المرور تُحفظ مشفّرة (hash).

## المتطلبات

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- SQL Server: يكفي LocalDB (يأتي مع Visual Studio) أو SQL Server Express أو Developer
- Node.js 20 أو أحدث

## التشغيل محلياً

### 1) تشغيل الـ API

```powershell
cd backend/Injaz.Api
dotnet restore
dotnet run --launch-profile http
```

- الـ API يعمل على `http://localhost:5080`.
- صفحة تجربة الـ API (Scalar) على `http://localhost:5080/scalar/v1`.
- عند أول تشغيل تُنشأ الجداول تلقائياً لو مش موجودة، ويتعمل حساب مدير النظام (راجع «أول دخول»).

لو لا تستخدم LocalDB، عدّل `ConnectionStrings:Default` في `appsettings.json`. مثال لـ SQL Express:

```json
"Default": "Server=.\\SQLEXPRESS;Database=InjazTasks;Trusted_Connection=True;TrustServerCertificate=True"
```

### 2) تشغيل الواجهة

من جذر المشروع:

```powershell
npm install
npm run dev
```

افتح `http://localhost:5173`. أي طلب يبدأ بـ `/api` يمر تلقائياً عبر الـ proxy المضبوط في `vite.config.js` إلى الـ API.

### أول دخول

مفيش بيانات تجريبية. عند التشغيل، لو مفيش حساب مدير نظام، التطبيق بينشئ حساب **مدير النظام** من إعدادات `InitialAdmin`:

| الإعداد | القيمة الافتراضية |
|---|---|
| `InitialAdmin:Email` | `super@injaz.local` |
| `InitialAdmin:Name` | `مدير النظام` |
| `InitialAdmin:Password` | لازم تتحدد. في Development القيمة `ChangeMe@123` من `appsettings.Development.json` |

كلمة المرور دي بتُستخدم مرة واحدة وقت إنشاء الحساب. ادخل بيها، وغيّرها من «إدارة الحسابات»، وبعدين ضيف المديرين والموظفين.

## قاعدة البيانات و Migrations

في البداية ينشئ التطبيق الجداول مباشرة من النموذج باستخدام `EnsureCreated`. لما يبدأ النموذج يتغير، انتقل إلى Migrations:

```powershell
dotnet tool install --global dotnet-ef
cd backend/Injaz.Api
# احذف قاعدة البيانات التجريبية أول مرة فقط (لأنها أُنشئت بدون Migrations)
dotnet ef database drop
dotnet ef migrations add InitialCreate
dotnet run
```

بعد وجود Migrations في المشروع، يطبّقها التطبيق تلقائياً عند التشغيل (`Migrate`).

**الجداول:** `Users`, `Tasks`, `TaskAssignments`, `WorkLogs`, `Attachments`, `TaskActivities`, `NotificationReads`.

## نقاط الـ API

كل النقاط تتطلب `Authorization: Bearer <token>` ما عدا تسجيل الدخول.

| الطريقة | المسار | الوصف | الصلاحية |
|---|---|---|---|
| POST | `/api/auth/login` | تسجيل الدخول | الكل |
| GET | `/api/auth/me` | المستخدم الحالي | مسجّل |
| GET | `/api/users` | أعضاء الفريق | مسجّل |
| POST / PUT / DELETE | `/api/users[/{id}]` | إدارة الحسابات (مديرين وموظفين، مع الدور) | مدير النظام |
| GET | `/api/tasks?status=&search=` | المهام المتاحة (المدير: الكل، الموظف: المسندة إليه) | مسجّل |
| GET | `/api/tasks/{id}` | تفاصيل المهمة مع السجل والمرفقات والنشاط | مدير / مكلّف |
| POST / PUT / DELETE | `/api/tasks[/{id}]` | إنشاء / تعديل / حذف مهمة | مدير / مدير النظام |
| PATCH | `/api/tasks/{id}/status` | تغيير الحالة | مدير / مكلّف |
| POST | `/api/tasks/{id}/logs` | إضافة سجل عمل بالساعات | مدير / مكلّف |
| POST | `/api/tasks/{id}/attachments` | رفع ملفات (`multipart/form-data`، الحقل `files`) | مدير / مكلّف |
| GET / DELETE | `/api/attachments/{id}` | تنزيل أو حذف مرفق | مدير / مكلّف (الحذف: المدير أو صاحب الملف) |
| GET | `/api/activity?take=10` | آخر النشاطات | مسجّل |
| GET | `/api/notifications` | المهام المتأخرة أو التي يستحق موعدها خلال 3 أيام | مسجّل |
| POST | `/api/notifications/read-all` | تعليم الإشعارات كمقروءة | مسجّل |
| GET | `/api/health` | فحص حالة الخادم | الكل |

القيم النصية: الحالة `todo | progress | review | done`، والأولوية `high | medium | low`، والدور `super | manager | employee`.

## الأدوار

- **مدير النظام (super):** الوحيد اللي بيضيف ويعدّل ويحذف الحسابات (مديرين وموظفين)، وله كل صلاحيات المدير في المهام. حسابه بيتعمل تلقائياً من إعدادات `InitialAdmin` لو مش موجود. ما ينفعش يتحذف أو يتغيّر دوره.
- **مدير (manager):** ينشئ المهام ويعدّلها ويسندها ويشوف كل المهام والفريق.
- **موظف (employee):** يشوف المهام المسندة له بس، ويغيّر حالتها ويسجّل ساعات ويرفع ملفات.

## هيكل الـ Backend

```
backend/
├── Injaz.slnx
└── Injaz.Api/
    ├── Program.cs              # الإعداد: EF Core, JWT, CORS, OpenAPI
    ├── Controllers/            # Auth, Users, Tasks, Attachments, Activity, Notifications
    ├── Data/                   # AppDbContext + DbSeeder
    ├── Entities/               # نماذج قاعدة البيانات
    ├── Dtos/                   # نماذج الطلبات والردود مع رسائل التحقق بالعربي
    ├── Services/               # TokenService, FileStorage, أدوات المستخدم
    └── Infrastructure/         # إضافة مخطط Bearer لوثيقة OpenAPI
```

## قبل النشر للإنتاج

- ضع مفتاح JWT قوياً خارج الكود، مثلاً:
  `dotnet user-secrets set "Jwt:Key" "<مفتاح عشوائي طويل>"`
  أو متغير البيئة `Jwt__Key`. المفتاح الموجود في `appsettings.Development.json` مخصص للتطوير فقط.
- عدّل `Cors:AllowedOrigins` ليطابق دومين الواجهة، واضبط `VITE_API_URL` عند بناء الواجهة (راجع `.env.example`).
- في الإنتاج اضبط `InitialAdmin__Password` كمتغير بيئة عند أول تشغيل، وسيُنشأ حساب مدير النظام بكلمة المرور دي.
- مدة تنبيه «موعد المهمة قريب» تتظبط من `Notifications:DueSoonDays` (الافتراضي 3 أيام).
- يمكن لاحقاً نقل تخزين الملفات إلى Azure Blob أو S3 عبر استبدال `FileStorage`.
