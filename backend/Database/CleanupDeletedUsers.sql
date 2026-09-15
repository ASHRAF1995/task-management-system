/* =====================================================================
   إنجاز — حذف نهائي للحسابات المحذوفة من التطبيق (IsDeleted = 1)
   ومعاها كل البيانات المرتبطة بيها:
     - المهام اللي هما أنشأوها (بسجلاتها ومرفقاتها ونشاطها)
     - سجلات العمل والمرفقات والنشاط والإشعارات الخاصة بيهم على أي مهمة

   تحذير: الحذف نهائي. خد نسخة احتياطية قبل التشغيل.
   ملفات المرفقات على القرص (App_Data/uploads) لا تُحذف من هنا.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Ids TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Ids (Id)
SELECT Id FROM dbo.Users WHERE IsDeleted = 1 AND Role <> N'Super';

-- معاينة: الحسابات اللي هتتحذف
SELECT u.Email, u.Name, u.Role FROM dbo.Users u JOIN @Ids i ON i.Id = u.Id;

-- المهام اللي أنشأوها (الجداول التابعة بتتحذف تلقائياً بالـ CASCADE)
DELETE FROM dbo.Tasks            WHERE CreatedById IN (SELECT Id FROM @Ids);

-- بياناتهم على مهام تانية
DELETE FROM dbo.NotificationReads WHERE UserId IN (SELECT Id FROM @Ids);
DELETE FROM dbo.TaskAssignments   WHERE UserId IN (SELECT Id FROM @Ids);
DELETE FROM dbo.WorkLogs          WHERE UserId IN (SELECT Id FROM @Ids);
DELETE FROM dbo.Attachments       WHERE UserId IN (SELECT Id FROM @Ids);
DELETE FROM dbo.TaskActivities    WHERE UserId IN (SELECT Id FROM @Ids);

DELETE FROM dbo.Users             WHERE Id IN (SELECT Id FROM @Ids);
DECLARE @Deleted INT = @@ROWCOUNT;

PRINT CONCAT(N'تم حذف ', @Deleted, N' حساب نهائياً.');

COMMIT TRANSACTION;
