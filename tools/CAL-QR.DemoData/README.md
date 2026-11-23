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

> **أيّ مؤشّر يُقرأ؟** المنظومة تقرأ `C:\ProgramData\CAL-QR\db_path.txt` **أوّلاً**
> (`AppPaths.FindPointerFile`)، ولا تقرأ الملفّ الذي بجوار `CAL-QR.exe` إلّا إذا غاب مؤشّر ProgramData.
> لذلك كتابة مؤشّر بجوار exe وحدها **لا تكفي** عند وجود مؤشّر في ProgramData، وستفتح المنظومة قاعدتك الحيّة.
> التوجيه الصحيح يكون على مؤشّر ProgramData نفسه.

```powershell
$f = "C:\ProgramData\CAL-QR\db_path.txt"

# 1) نسخة احتياطيّة من المؤشّر الحاليّ (أو علامة تدلّ أنّه لم يكن موجوداً)
if (Test-Path $f) { Copy-Item $f "$f.bak" -Force } else { Set-Content "$f.none" "none" }

# 2) وجّه المؤشّر إلى نسخة من القاعدة التجريبيّة (لا تستعمل الأصل مباشرة)
Copy-Item "D:\cal-qr-demo\cal-qr-DEMO.db" "D:\cal-qr-demo\cal-qr-DEMO-run.db" -Force
New-Item -ItemType Directory -Force "C:\ProgramData\CAL-QR" | Out-Null
Set-Content -Path $f -Value "D:\cal-qr-demo\cal-qr-DEMO-run.db" -NoNewline

# 3) شغّل المنظومة
& "D:\cal-qr\CAL-QR\bin\Debug\net8.0-windows\CAL-QR.exe"
```

**تأكّد قبل أيّ عمل:** اسم القاعدة المعروض في الشريط العلويّ يجب أن يكون `cal-qr-DEMO-run.db`.
إن ظهر اسم قاعدتك الحيّة فأغلق المنظومة فوراً وراجع المؤشّر. ولا تُصدر شهادات على القاعدة التجريبيّة.

## العودة إلى بياناتك (بعد إغلاق المنظومة)
```powershell
$f = "C:\ProgramData\CAL-QR\db_path.txt"
if (Test-Path "$f.bak") { Move-Item "$f.bak" $f -Force }
elseif (Test-Path "$f.none") { Remove-Item $f, "$f.none" -Force }
Get-Content $f   # تحقّق: يجب أن يُظهر مسار قاعدتك الحيّة
```
إن كان عندك مؤشّر قديم بجوار exe فهو لم يُمسّ في الخطوات أعلاه.
