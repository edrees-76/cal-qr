# CAL-QR.DemoData — مولّد قاعدة بيانات تجريبيّة

يُنشئ **قاعدة SQLite منفصلة** (`cal-qr-DEMO.db`) بها 30 جهة و50 جهازاً و200 شهادة معايرة، منها 10 شهادات
نتيجتها «راسب» (معايرة مرفوضة). كلّ شهادة تصدر عبر `CertificateRepository.AddAsync` الحقيقيّ
(ترقيم `TNRC-SSDL-YYYY-XXXX` + توقيع SIG1 + حمولة QR) فتتحقّق في المنظومة كأيّ شهادة فعليّة.

**لا يمسّ أيّ قاعدة قائمة**: يرفض الكتابة فوق ملفّ موجود إلا بـ `--force`. كلّ القيم القياسيّة والأسماء خياليّة،
والجهات والأجهزة والسجلّات موسومة `IsSeedTestData`.

## التوليد
```powershell
cd D:\cal-qr
dotnet run --project tools\CAL-QR.DemoData -- D:\cal-qr-demo
```
الدخول: `admin` / `Demo@12345` — سؤال الاسترداد: الجواب `demo`.

## تشغيل المنظومة على البيانات التجريبيّة
```powershell
$dir = "D:\cal-qr\CAL-QR\bin\Debug\net8.0-windows"
$f = "$dir\db_path.txt"
if (Test-Path $f) { Copy-Item $f "$f.bak" -Force }
Set-Content -Path $f -Value "D:\cal-qr-demo\cal-qr-DEMO.db" -NoNewline
& "$dir\CAL-QR.exe"
```

## العودة إلى بياناتك
```powershell
$f = "D:\cal-qr\CAL-QR\bin\Debug\net8.0-windows\db_path.txt"
if (Test-Path "$f.bak") { Move-Item "$f.bak" $f -Force } else { Remove-Item $f }
```
