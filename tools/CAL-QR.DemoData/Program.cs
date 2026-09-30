using CAL_QR.DemoData;

// الاستعمال:
//   dotnet run --project tools\CAL-QR.DemoData -- <مجلّد الإخراج> [--force]
// يُنشئ <مجلّد الإخراج>\cal-qr-DEMO.db قاعدة بيانات تجريبيّة كاملة. لا يمسّ أيّ قاعدة قائمة:
// يرفض الكتابة فوق ملفّ موجود إلا بـ --force.
string? folder = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
bool force = args.Contains("--force");

if (string.IsNullOrWhiteSpace(folder))
{
    Console.WriteLine("الاستعمال: CAL-QR.DemoData <مجلّد الإخراج> [--force]");
    return 1;
}

Directory.CreateDirectory(folder);
string dbPath = Path.GetFullPath(Path.Combine(folder, "cal-qr-DEMO.db"));

try
{
    var summary = await DemoDataGenerator.GenerateAsync(dbPath, new DemoDataOptions { OverwriteExisting = force });

    Console.WriteLine();
    Console.WriteLine("تمّ توليد القاعدة التجريبيّة:");
    Console.WriteLine($"  الملفّ:        {dbPath}");
    Console.WriteLine($"  الجهات:        {summary.Owners}");
    Console.WriteLine($"  الأجهزة:       {summary.Devices}");
    Console.WriteLine($"  الشهادات:      {summary.Certificates} (منها {summary.Failed} معايرة مرفوضة/راسبة)");
    Console.WriteLine($"  الأرقام:       {summary.FirstNumber} … {summary.LastNumber}");
    Console.WriteLine($"  الدخول:        {summary.AdminUsername} / {summary.AdminPassword}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("فشل التوليد: " + ex.Message);
    return 2;
}
