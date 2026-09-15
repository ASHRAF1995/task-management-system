/* =====================================================================
   إنجاز — سكربت إنشاء قاعدة البيانات (SQL Server)
   مطابق لنموذج EF Core في backend/Injaz.Api/Data/AppDbContext.cs

   - السكربت آمن للتشغيل أكثر من مرة: كل جدول وفهرس يُنشأ فقط لو مش موجود.
   - شغّله وأنت متصل بقاعدة البيانات المطلوبة (أو فعّل جزء CREATE DATABASE تحت).
   - حساب المدير الأول لا يُضاف هنا؛ التطبيق ينشئه عند أول تشغيل
     من الإعداد InitialAdmin:Password (راجع الملاحظات في آخر الملف).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- مطلوب للفهرس المفلتر على Users.Email
GO

/* ---------- (اختياري) إنشاء قاعدة البيانات ----------
   كثير من الاستضافات لا تسمح بـ CREATE DATABASE؛ في هذه الحالة أنشئها من لوحة التحكم
   ثم اختَرها قبل تشغيل السكربت.

IF DB_ID(N'InjazTasks') IS NULL
    CREATE DATABASE [InjazTasks];
GO
USE [InjazTasks];
GO
*/

BEGIN TRANSACTION;
GO

/* =========================== Users =========================== */
IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id]           UNIQUEIDENTIFIER NOT NULL,
        [Name]         NVARCHAR(100)    NOT NULL,
        [Email]        NVARCHAR(256)    NOT NULL,
        [PasswordHash] NVARCHAR(512)    NOT NULL,
        [Role]         NVARCHAR(20)     NOT NULL,   -- Super | Manager | Employee
        [Title]        NVARCHAR(100)    NOT NULL,
        [Initials]     NVARCHAR(10)     NOT NULL,
        [Color]        NVARCHAR(20)     NOT NULL,
        [IsDeleted]    BIT              NOT NULL,
        [CreatedAt]    DATETIME2        NOT NULL,   -- UTC
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Email' AND object_id = OBJECT_ID(N'[dbo].[Users]'))
    CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]) WHERE [IsDeleted] = 0;
GO

/* =========================== Tasks =========================== */
IF OBJECT_ID(N'[dbo].[Tasks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Tasks] (
        [Id]          UNIQUEIDENTIFIER NOT NULL,
        [Title]       NVARCHAR(200)    NOT NULL,
        [Description] NVARCHAR(4000)   NOT NULL,
        [Priority]    NVARCHAR(20)     NOT NULL,   -- High | Medium | Low
        [Status]      NVARCHAR(20)     NOT NULL,   -- Todo | Progress | Review | Done
        [StartDate]   DATE             NOT NULL,
        [DueDate]     DATE             NOT NULL,
        [CreatedById] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAt]   DATETIME2        NOT NULL,
        [UpdatedAt]   DATETIME2        NOT NULL,
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tasks_Users_CreatedById] FOREIGN KEY ([CreatedById])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_CreatedById' AND object_id = OBJECT_ID(N'[dbo].[Tasks]'))
    CREATE INDEX [IX_Tasks_CreatedById] ON [dbo].[Tasks] ([CreatedById]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_DueDate' AND object_id = OBJECT_ID(N'[dbo].[Tasks]'))
    CREATE INDEX [IX_Tasks_DueDate] ON [dbo].[Tasks] ([DueDate]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_Status' AND object_id = OBJECT_ID(N'[dbo].[Tasks]'))
    CREATE INDEX [IX_Tasks_Status] ON [dbo].[Tasks] ([Status]);
GO

/* ====================== TaskAssignments ====================== */
IF OBJECT_ID(N'[dbo].[TaskAssignments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TaskAssignments] (
        [TaskId]     UNIQUEIDENTIFIER NOT NULL,
        [UserId]     UNIQUEIDENTIFIER NOT NULL,
        [AssignedAt] DATETIME2        NOT NULL,
        CONSTRAINT [PK_TaskAssignments] PRIMARY KEY ([TaskId], [UserId]),
        CONSTRAINT [FK_TaskAssignments_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskAssignments_UserId' AND object_id = OBJECT_ID(N'[dbo].[TaskAssignments]'))
    CREATE INDEX [IX_TaskAssignments_UserId] ON [dbo].[TaskAssignments] ([UserId]);
GO

/* ========================== WorkLogs ========================= */
IF OBJECT_ID(N'[dbo].[WorkLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkLogs] (
        [Id]        UNIQUEIDENTIFIER NOT NULL,
        [TaskId]    UNIQUEIDENTIFIER NOT NULL,
        [UserId]    UNIQUEIDENTIFIER NOT NULL,
        [Text]      NVARCHAR(1000)   NOT NULL,
        [Hours]     DECIMAL(6, 2)    NOT NULL,
        [CreatedAt] DATETIME2        NOT NULL,
        CONSTRAINT [PK_WorkLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkLogs_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WorkLogs_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkLogs_TaskId' AND object_id = OBJECT_ID(N'[dbo].[WorkLogs]'))
    CREATE INDEX [IX_WorkLogs_TaskId] ON [dbo].[WorkLogs] ([TaskId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkLogs_UserId' AND object_id = OBJECT_ID(N'[dbo].[WorkLogs]'))
    CREATE INDEX [IX_WorkLogs_UserId] ON [dbo].[WorkLogs] ([UserId]);
GO

/* ========================= Attachments ======================= */
IF OBJECT_ID(N'[dbo].[Attachments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Attachments] (
        [Id]          UNIQUEIDENTIFIER NOT NULL,
        [TaskId]      UNIQUEIDENTIFIER NOT NULL,
        [UserId]      UNIQUEIDENTIFIER NOT NULL,
        [FileName]    NVARCHAR(260)    NOT NULL,
        [ContentType] NVARCHAR(200)    NOT NULL,
        [SizeBytes]   BIGINT           NOT NULL,
        [StoredName]  NVARCHAR(100)    NOT NULL,   -- اسم الملف على القرص (App_Data/uploads)
        [CreatedAt]   DATETIME2        NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attachments_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Attachments_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Attachments_TaskId' AND object_id = OBJECT_ID(N'[dbo].[Attachments]'))
    CREATE INDEX [IX_Attachments_TaskId] ON [dbo].[Attachments] ([TaskId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Attachments_UserId' AND object_id = OBJECT_ID(N'[dbo].[Attachments]'))
    CREATE INDEX [IX_Attachments_UserId] ON [dbo].[Attachments] ([UserId]);
GO

/* ======================== TaskActivities ===================== */
IF OBJECT_ID(N'[dbo].[TaskActivities]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TaskActivities] (
        [Id]        UNIQUEIDENTIFIER NOT NULL,
        [TaskId]    UNIQUEIDENTIFIER NOT NULL,
        [UserId]    UNIQUEIDENTIFIER NOT NULL,
        [Text]      NVARCHAR(1000)   NOT NULL,
        [Type]      NVARCHAR(30)     NOT NULL,     -- create | update | status | log | attachment
        [CreatedAt] DATETIME2        NOT NULL,
        CONSTRAINT [PK_TaskActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskActivities_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskActivities_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskActivities_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[TaskActivities]'))
    CREATE INDEX [IX_TaskActivities_CreatedAt] ON [dbo].[TaskActivities] ([CreatedAt]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskActivities_TaskId' AND object_id = OBJECT_ID(N'[dbo].[TaskActivities]'))
    CREATE INDEX [IX_TaskActivities_TaskId] ON [dbo].[TaskActivities] ([TaskId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TaskActivities_UserId' AND object_id = OBJECT_ID(N'[dbo].[TaskActivities]'))
    CREATE INDEX [IX_TaskActivities_UserId] ON [dbo].[TaskActivities] ([UserId]);
GO

/* ====================== NotificationReads ==================== */
IF OBJECT_ID(N'[dbo].[NotificationReads]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NotificationReads] (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [TaskId] UNIQUEIDENTIFIER NOT NULL,
        [ReadAt] DATETIME2        NOT NULL,
        CONSTRAINT [PK_NotificationReads] PRIMARY KEY ([UserId], [TaskId]),
        CONSTRAINT [FK_NotificationReads_Tasks_TaskId] FOREIGN KEY ([TaskId])
            REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NotificationReads_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificationReads_TaskId' AND object_id = OBJECT_ID(N'[dbo].[NotificationReads]'))
    CREATE INDEX [IX_NotificationReads_TaskId] ON [dbo].[NotificationReads] ([TaskId]);
GO

COMMIT TRANSACTION;
GO

PRINT N'تم إنشاء جداول إنجاز بنجاح.';
GO

/* =====================================================================
   ملاحظات التشغيل
   ---------------------------------------------------------------------
   1) Connection string للسيرفر الأونلاين (ضعه في متغير بيئة، لا في Git):
        ConnectionStrings__Default =
        Server=<host>,1433;Database=<db>;User Id=<user>;Password=<pass>;Encrypt=True;TrustServerCertificate=True

   2) حساب المدير الأول: عند أول تشغيل على قاعدة فارغة اضبط
        InitialAdmin__Password = <كلمة مرور قوية>
      سيُنشأ حساب مدير النظام super@injaz.local بهذه الكلمة (كلمة المرور تُخزَّن hash من التطبيق،
      لذلك لا تُدخَل من SQL مباشرة).

   3) التطبيق لا يضيف أي بيانات تجريبية. لحذف الحسابات المحذوفة (IsDeleted = 1) نهائياً
      استخدم CleanupDeletedUsers.sql.

   4) لو أضفت EF Migrations لاحقاً، سجّل أول Migration كمنفّذة بدل ما يحاول ينشئ الجداول تاني:
        CREATE TABLE [__EFMigrationsHistory] (
            [MigrationId] NVARCHAR(150) NOT NULL PRIMARY KEY,
            [ProductVersion] NVARCHAR(32) NOT NULL);
        INSERT INTO [__EFMigrationsHistory] VALUES (N'<اسم_ملف_الـMigration>', N'10.0.12');
   ===================================================================== */
