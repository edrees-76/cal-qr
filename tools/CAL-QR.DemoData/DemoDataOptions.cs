namespace CAL_QR.DemoData;

public sealed class DemoDataOptions
{
    public int Owners { get; init; } = 30;
    public int Devices { get; init; } = 50;
    public int Certificates { get; init; } = 200;

    /// <summary>عدد الشهادات (من الإجماليّ) التي نتيجة معايرتها «راسب» (مرفوضة).</summary>
    public int FailedCertificates { get; init; } = 10;

    public DateTime Today { get; init; } = DateTime.Today;
    public int Seed { get; init; } = 20260930;
    public string AdminUsername { get; init; } = "admin";
    public string AdminPassword { get; init; } = "Demo@12345";

    /// <summary>false (الافتراضيّ) ⇒ يرفض التوليد إن كان الملفّ موجوداً، فلا يُكتب فوق قاعدة قائمة.</summary>
    public bool OverwriteExisting { get; init; }
}

public sealed record DemoDataSummary(
    int Owners, int Devices, int Certificates, int Failed,
    string FirstNumber, string LastNumber, string AdminUsername, string AdminPassword);
