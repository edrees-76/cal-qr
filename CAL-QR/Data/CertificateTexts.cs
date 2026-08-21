namespace CAL_QR.Data
{
    /// <summary>
    /// ╔════════════════════════════════════════════════════════════════════════╗
    /// ║  نصوص ثابتة تُطبع في كل شهادة — مُدمجة في الكود، لا في قاعدة البيانات. ║
    /// ╚════════════════════════════════════════════════════════════════════════╝
    ///
    /// ⚠ **خارج SIG1 عمداً** (v3 §٤). الشرطان اللذان يُجيزان ذلك متحققان معاً:
    /// النصّ متطابق حرفياً في كل شهادة، ولا عمود له في قاعدة البيانات. نصّ لا
    /// يتغيّر من سجلّ إلى سجلّ لا يميّز أحدهما عن الآخر، فلا يضيف حماية توقيعية؛
    /// وحمايته الحقيقية أنه لا يُعدَّل إلا بإصدار برنامج جديد — لا عمود يُحرَّر
    /// في القاعدة. وإدخاله SIG1 كان سيُبطل الاختبارات الذهبية لبانٍ مستقرّ.
    ///
    /// ⚠ أي نصّ يفشل في أحد الشرطين ⇒ يدخل SIG1 وجوباً ويُنسخ إلى الشهادة.
    /// هذه الحالة الوحيدة القائمة.
    ///
    /// ❌ ليس حقلاً على Certificate ولا على DeviceType. ❌ لا يُبذَر.
    /// </summary>
    public static class CertificateTexts
    {
        /// <summary>
        /// بيان المطابقة — الإنجليزية. يُطبع في الأنواع الخمسة، أعلى الصندوق.
        ///
        /// ⚠ منفصل عن النظير العربي عمداً، لا سلسلة واحدة بفاصل: خلط RTL و LTR
        /// في سلسلة واحدة يُجبر الطابع على تخمين الاتجاه. الفصل يتيح ضبط
        /// FlowDirection لكل كتلة على حدة في قالب QuestPDF (المرحلة ٥).
        ///
        /// ⚠ ComplianceStatement (بأي **معيار** أُجريت المعايرة) ≠
        /// ComplianceVerdict (**حكم** الجهاز، قالب على DeviceType). لا يُخلطان.
        /// </summary>
        public const string ComplianceStatementEn =
            "The calibration was performed in accordance with the technical procedures approved by " +
            "the International Atomic Energy Agency (IAEA) and in compliance with the relevant " +
            "technical principles and requirements of ISO/IEC 17025:2017";

        /// <summary>
        /// بيان المطابقة — العربية. يُطبع أسفل النظير الإنجليزي في الصندوق نفسه.
        /// </summary>
        public const string ComplianceStatementAr =
            "أجريت المعايرة وفقا لإجراءات العمل الفنية المعتمدة من الوكالة الدولية للطاقة الذرية (IAEA)، " +
            "وبما يتوافق مع المبادئ والمتطلبات الفنية ذات الصلة للمواصفة الدولية ISO/IEC 17025:2017";

        // ─── نصوص تقرير الحالة (النوع السادس) — إنجليزية فقط، خارج SIG1 بنفس مبرّر بيان المطابقة ───

        /// <summary>قيمة ثابتة تحلّ محلّ تاريخ الاستحقاق في تقرير الحالة.</summary>
        public const string StatusReportRecalibrationValue =
            "To be determined after successful calibration";

        /// <summary>عنوان البيان الذي يحلّ محلّ جدول النتائج في تقرير الحالة.</summary>
        public const string StatusReportNoResultsTitle =
            "CALIBRATION COULD NOT BE PERFORMED";

        /// <summary>نصّ البيان الذي يحلّ محلّ جدول النتائج في تقرير الحالة.</summary>
        public const string StatusReportNoResultsBody =
            "No calibration results are reported because the instrument failed the pre-calibration functional inspection.";

        /// <summary>صندوق ختام تقرير الحالة — يحلّ محلّ صندوق بيان المطابقة. إنجليزية فقط، خارج SIG1.</summary>
        public const string StatusReportNotPerformedTitle =
            "CALIBRATION WAS NOT PERFORMED";

        public const string StatusReportNotPerformedLine1 =
            "This report documents the results of the pre-calibration functional inspection only.";

        public const string StatusReportNotPerformedLine2 =
            "The instrument failed the functional checks; therefore, calibration could not be completed.";

        public const string StatusReportNotPerformedLine3 =
            "The instrument shall be repaired and successfully pass the functional inspection before being submitted for recalibration.";

        /// <summary>
        /// سطر جهة الاتصال المؤسّسي في تذييل كل شهادة. ثابت لا يتغيّر بين الشهادات،
        /// ولا عمود له في القاعدة ⇒ هنا، خارج SIG1 — بنفس مبرّر بيان المطابقة.
        /// </summary>
        public const string FooterContact =
            "Tel: +218 21 3705824   Tel: +218 21 3690962   Fax: +218 21 3690961   Email: gdoffice@tnrc.ly   P.O.Box: 30878";
    }
}
