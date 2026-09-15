<#
  تجهيز نسخة «إنجاز» للرفع على سيرفر Windows / IIS (مثل site4now / SmarterASP.NET)

  الاستخدام (من جذر المشروع):
      powershell -ExecutionPolicy Bypass -File .\publish.ps1

  الناتج:
      deploy\publish\          الملفات الجاهزة للرفع
      deploy\injaz-release.zip نفس الملفات مضغوطة

  -SelfContained:$false  لو السيرفر عليه ASP.NET Core 10 Hosting Bundle (حجم أصغر)
#>
param(
    [string]$Runtime = 'win-x64',
    [bool]$SelfContained = $true
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$api     = Join-Path $root 'backend\Injaz.Api'
$deploy  = Join-Path $root 'deploy'
$out     = Join-Path $deploy 'publish'
$zip     = Join-Path $deploy 'injaz-release.zip'

foreach ($tool in 'dotnet', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "الأداة $tool مش متسطّبة." }
}

# 1) مفتاح JWT للإنتاج (بيتعمل مرة واحدة ويفضل ثابت عشان المستخدمين ما يتطلعوش بره مع كل نشر)
$prod = Join-Path $api 'appsettings.Production.json'
if (-not (Test-Path $prod)) {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $settings = @{ Jwt = @{ Key = [Convert]::ToBase64String($bytes) } }
    $settings | ConvertTo-Json -Depth 5 | Set-Content -Path $prod -Encoding UTF8
    Write-Host "تم إنشاء $prod بمفتاح JWT جديد." -ForegroundColor Yellow
}

# 2) تنظيف الناتج القديم
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Path $out -Force | Out-Null

# 3) النشر (بيبني الواجهة وينسخها لـ wwwroot تلقائياً)
Write-Host "جاري النشر ($Runtime, SelfContained=$SelfContained)..." -ForegroundColor Cyan
$sc = if ($SelfContained) { 'true' } else { 'false' }
dotnet publish $api -c Release -r $Runtime --self-contained $sc -o $out
if ($LASTEXITCODE -ne 0) { throw 'فشل dotnet publish.' }

# 4) فولدرات لازم تكون موجودة على السيرفر
New-Item -ItemType Directory -Path (Join-Path $out 'App_Data\uploads') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $out 'logs') -Force | Out-Null
foreach ($d in 'App_Data\uploads', 'logs') { Set-Content -Path (Join-Path $out "$d\.keep") -Value '' }

if (-not (Test-Path (Join-Path $out 'wwwroot\index.html'))) { throw 'الواجهة ما اتنسختش لـ wwwroot.' }

# 5) الضغط
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force

Write-Host ''
Write-Host 'النسخة جاهزة:' -ForegroundColor Green
Write-Host "  الفولدر: $out"
Write-Host "  الملف:   $zip"
Write-Host ''
Write-Host 'على السيرفر: ارفع محتويات الفولدر لجذر الموقع، واعمل للـ App_Pool صلاحية كتابة على App_Data و logs.'
