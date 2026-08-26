using Xunit;
using CAL_QR.Models;
using CAL_QR.Services;

namespace CAL_QR.Tests
{
    /// <summary>
    /// الباني يبذر هيكل كتابة لا قيمة: ± والوحدة جاهزتان، والقياس يكتبه المعايِر.
    /// النصّ هنا ليس تجميليًّا — حارس SaveAsync يرفض أيّ حقل ما زال يحمل __،
    /// والوحدات منقولة من قوالب م. رضا وتُطبع حرفيًّا في الشهادة.
    /// </summary>
    public class CertificateDraftBuilderTests
    {
        [Fact]
        public void Build_SeedsEnvironmentalConditionsWithAWritingTemplateNotAValue()
        {
            var builder = new CertificateDraftBuilder();

            var result = builder.Build(
                new DeviceType { Id = 1, Name = "Pancake Probe" },
                new CalibrationRecord { Id = 7 },
                new Device { Id = 3, Model = "M-1", SerialNumber = "S-1" },
                new Owner { Id = 5, Name = "Test Client" });

            Assert.Equal("__ ± __ °C", result.Certificate.Temperature);
            Assert.Equal("__ ± __ % RH", result.Certificate.RelativeHumidity);
            Assert.Equal("__ ± __ kPa", result.Certificate.AtmosphericPressure);
        }
    }
}
