using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CAL_QR.Data;
using CAL_QR.Models;
using CAL_QR.Validation;

namespace CAL_QR.Services
{
    public class CertificateNumberService : ICertificateNumberService
    {
        public const string Prefix = "TNRC-SSDL";
        private const int MaxNumberPerYear = 9999;

        private readonly IDbContextFactory<CalQrDbContext> _contextFactory;

        public CertificateNumberService(IDbContextFactory<CalQrDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>يبني الرقم من سنته وتسلسله. الصيغة مرجع واحد لكل المشروع.</summary>
        public static string Format(int year, int number) =>
            string.Format(CultureInfo.InvariantCulture, "{0}-{1:0000}-{2:0000}", Prefix, year, number);

        public async Task<string> AllocateAsync(CalQrDbContext context, DateTime issueDate)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            IDbContextTransaction? transaction = context.Database.CurrentTransaction;
            if (transaction == null)
            {
                throw new InvalidOperationException(
                    "تخصيص رقم الشهادة يجب أن يقع داخل معاملة مفتوحة يملكها المستدعي، " +
                    "وإلا وقع التخصيص وحفظ الشهادة في معاملتين منفصلتين.");
            }

            int year = issueDate.Year;

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // جملة ذرّية واحدة: لا قراءة ثم كتابة، فلا نافذة تسابق أصلاً.
            // ON CONFLICT يغطي أول شهادة في السنة وما بعدها بنفس الجملة،
            // وRETURNING يُعيد القيمة بعد الزيادة في نفس الرحلة.
            using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText =
                @"INSERT INTO CertificateSequence (Year, LastNumber) VALUES ($year, 1)
                  ON CONFLICT(Year) DO UPDATE SET LastNumber = LastNumber + 1
                  RETURNING LastNumber;";

            var yearParameter = command.CreateParameter();
            yearParameter.ParameterName = "$year";
            yearParameter.Value = year;
            command.Parameters.Add(yearParameter);

            object? scalar = await command.ExecuteScalarAsync();
            if (scalar == null || scalar == DBNull.Value)
            {
                throw new InvalidOperationException(
                    $"تعذّر تخصيص رقم شهادة للسنة {year}: لم يُعِد عدّاد الأرقام أي قيمة.");
            }

            int allocated = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);

            if (allocated > MaxNumberPerYear)
            {
                // الصيغة أربع خانات. تجاوزها يُنتج رقماً بخمس خانات يكسر كل
                // ما يحلّل الرقم — يُرفض صراحةً بدل أن يمر بصمت.
                throw new InvalidOperationException(
                    $"تجاوز عدّاد سنة {year} الحدَّ الأقصى ({MaxNumberPerYear}) لصيغة الأربع خانات.");
            }

            return Format(year, allocated);
        }

        /// <summary>
        /// المزامنة تُنفَّذ في C# لا في SQL عمداً.
        ///
        /// النسخة السابقة كانت جملة SQL تقرأ السنة والتسلسل بـ substr بمواضع
        /// محارف مكتوبة يدوياً — وانزاحت بخانة واحدة، فقرأت "026-" سنةً وأدرجت
        /// صفّاً بـ Year = 26 وتركت Year = 2026 كما هو. والأسوأ أن الخطأ صامت:
        /// مسار ON CONFLICT لم يُنفَّذ قط، فبدت قاعدة «لا تُنقص العدّاد» محروسة
        /// وهي غير مفحوصة إطلاقاً.
        ///
        /// العلة الجذرية ليست الأرقام بل ازدواج مصدر الحقيقة: صيغة الرقم موصوفة
        /// مرة في Format() ومرة بمواضع محارف داخل نص SQL، بلا رابط بينهما. أي
        /// تغيير في البادئة كان سيكسر المزامنة بصمت ويُنتج أرقاماً مكررة.
        ///
        /// هنا تُقرأ الصيغة من ExtractYear/ExtractSequence، ثم يُتحقَّق بالدوران
        /// الكامل: الرقم يُقبل فقط إن ساوى Format(year, sequence) حرفياً — فيُفحص
        /// بذلك البادئة والتصفير والطول دفعةً واحدة، بمصدر حقيقة واحد.
        ///
        /// عملية إدارية لمرة واحدة على جدول صغير، فلا اعتبار للأداء.
        /// </summary>
        public async Task<int> SyncCounterWithExistingAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // ⚠ بلا مرشّح IsDeleted — عن قصد، ولا يُضاف.
            // رقم شهادة أُبطلت يجب أن يُحسب في المزامنة، وإلا عاد العدّاد دونه
            // فأُعيد استخدام رقم صدر فعلاً — وهو ما يمنعه ISO/IEC 17025 صراحةً.
            // إضافة `.Where(c => !c.IsDeleted)` هنا «تنظيفاً» أو اتساقاً مع بقية
            // الاستعلامات تفتح ثغرة إعادة استخدام رقم بصمت.
            var numbers = await context.Certificates
                .AsNoTracking()
                .Select(c => c.CertificateNumber)
                .ToListAsync();

            var highestByYear = new Dictionary<int, int>();

            foreach (string number in numbers)
            {
                int? year = CertificateDateRules.ExtractYear(number);
                int? sequence = CertificateDateRules.ExtractSequence(number);

                if (year == null || sequence == null)
                {
                    continue;
                }

                // الدوران الكامل: أي رقم لا يُعيد بناؤه Format حرفياً ليس من
                // هذه الصيغة (رقم ورقي مرحَّل مثلاً) ولا يدخل المزامنة.
                if (!string.Equals(number?.Trim(), Format(year.Value, sequence.Value), StringComparison.Ordinal))
                {
                    continue;
                }

                if (!highestByYear.TryGetValue(year.Value, out int current) || sequence.Value > current)
                {
                    highestByYear[year.Value] = sequence.Value;
                }
            }

            if (highestByYear.Count == 0)
            {
                return 0;
            }

            var existingRows = await context.CertificateSequence.ToListAsync();
            int changed = 0;

            foreach (var pair in highestByYear)
            {
                var row = existingRows.FirstOrDefault(s => s.Year == pair.Key);

                if (row == null)
                {
                    context.CertificateSequence.Add(new CertificateSequence
                    {
                        Year = pair.Key,
                        LastNumber = pair.Value
                    });
                    changed++;
                }
                else if (pair.Value > row.LastNumber)
                {
                    // شرط الرفع فقط: المزامنة لا تُنقص العدّاد أبداً، وإلا أعادت
                    // إحياء رقم أُلغيت شهادته — وهو ما يمنعه ISO/IEC 17025.
                    row.LastNumber = pair.Value;
                    changed++;
                }
            }

            if (changed > 0)
            {
                await context.SaveChangesAsync();
            }

            return changed;
        }
    }
}
