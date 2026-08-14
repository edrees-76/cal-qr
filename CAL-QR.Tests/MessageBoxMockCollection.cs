using Xunit;

namespace CAL_QR.Tests
{
    // تسلسل الأصناف التي تلمس حقول MessageBoxShowMock الـstatic، لمنع
    // تشارُك الحالة بين اختبارات متوازية (سباق على حقل static مشترك).
    [CollectionDefinition("MessageBoxMock", DisableParallelization = true)]
    public class MessageBoxMockCollection { }
}
