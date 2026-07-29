# محتوى بذر قوالب `DeviceType` — الأنواع الخمسة

> **المصدر:** النماذج الخمسة الفعلية. كل نص أدناه **منقول حرفياً** من شهادة صادرة.
> **يحلّ التعارض ت-٢** في خطة المرحلة ٢.
> ⚠ ما لم أستطع استخراجه موسوم `[يحتاج تأكيد]` — **لا يُختلق**، يُترك فارغاً.
>
> **هذا الملف هو مرجع الأصل.** النسخة التنفيذية منه في `CAL-QR/Data/DeviceTypeCatalog.cs`،
> وأي تعديل هنا يجب أن يرافقه تعديل هناك (أو العكس)، وإلا تباعد المرجع عن المزروع.

---

## ⚠ أسماء الأنواع — الاسم المعتمد ومرادفاته

**الاسم يُنسخ إلى `CertificateTemplateType` ويُطبع على الشهادة ويدخل التوقيع.** لذا يجب أن يطابق عنوان النموذج.

| # | الاسم المعتمد | المرادفات القديمة (في القاعدة حالياً) |
|---|---|---|
| 1 | `Pancake Probe` | — |
| 2 | `Beta Scintillation Probe` | `Beta Scintillator Probe` |
| 3 | `Gamma Scintillation Probe` | `Gamma Probe` |
| 4 | `Personal Electronic Dosimeter (PED)` | `PED` |
| 5 | `Dose Rate Meter` | — |

**قاعدة المطابقة عند البذر:** يُبحث بالاسم المعتمد، فإن لم يوجد يُبحث بالمرادفات. **عند إصابة مرادف: يُعاد تسمية الصفّ القائم، ولا يُنشأ صفّ جديد** — حفاظاً على `DeviceTypeId` وكل الأجهزة المرتبطة به.

إعادة التسمية لا تمسّ أي شهادة صادرة: `CertificateTemplateType` منسوخ نصاً وقت الإصدار (مبدأ الوثيقة المجمّدة).

---

## ١. Pancake Probe

| الحقل | القيمة |
|---|---|
| `ProcedureNo` | `SSDL-PED-IS-CP-01` |
| `CalibrationLocation` | `SSDL Calibration Laboratory, Tajoura - Libya` |
| `ReferenceGeometry` | `Contact` |
| `CountingTime` | `60 s` |
| `CountingUnit` | `kCPM` |
| `ComplianceVerdict` | `Instrument complies with laboratory acceptance criteria.` |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CFavg` |
| `UncertaintyEnabled` | `true` |
| `MethodologyEnabled` | `true` |

**`MethodologyText`:**
```
• The calibration was performed in accordance with SSDL procedure SSDL-PED-IS-CP-01.
• The Average Correction Factor (CFavg) is the mean of the correction factors obtained from three reference sources.
• The reported expanded uncertainty is based on a coverage factor k = 2 (approximately 95% confidence level).
```

**`AdditionalInformation`:**
```
This certificate is valid only for the instrument and detector identified above.
Recalibration is recommended before the due date to ensure continued measurement accuracy.
```

**الفحوصات الوظيفية** — `DefaultResult = Yes`

| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Detector Window Inspection | Window clean and free from damage |

**مكوّنات عدم اليقين** — `StandardUncertainty` تُترك فارغة (تتغير بكل معايرة)

| # | ComponentName | EvaluationType |
|---|---|---|
| 1 | Source (calibration certificate) | Type B |
| 2 | Radioactive decay | Type B |
| 3 | Counting statistics | Type A |
| 4 | Repeatability (measurement) | Type A |
| 5 | Geometry and positioning | Type B |

---

## ٢. Beta Scintillation Probe

| الحقل | القيمة |
|---|---|
| `CalibrationStandard` | `SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-01 Implemented in accordance with ISO/IEC 17025:2017 requirements.` |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CFavg` |
| `UncertaintyEnabled` | `true` |
| `MethodologyEnabled` | `true` |

⚠ **`MethodologyText` · `ComplianceVerdict` · `CountingTime` · `CountingUnit` · `ReferenceGeometry` — `[يحتاج تأكيد]`**
نص OCR لهذا النموذج تالف، والبنية مؤكدة والنصوص غير مقروءة.

**الفحوصات ومكوّنات عدم اليقين — `[يحتاج تأكيد]`**
مرجَّح أنها مطابقة لـ Pancake، ولا تُزرع بالتخمين. تُترك فارغة حتى تصل النسخة الأصلية.

---

## ٣. Gamma Scintillation Probe

| الحقل | القيمة |
|---|---|
| `ReferenceGeometry` | `Distance = 1.0 meter (Axis configuration)` |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CF` |
| `UncertaintyEnabled` | `false` |
| `MethodologyEnabled` | `true` |

**`MethodologyText`:**
```
Calibration was performed using instrument-specific validated methods. The calibration was carried out using a Co-60, Cs-137 and point gamma source. Reference dose rates were determined using a calibrated Farmer ionization chamber traceable to the International Atomic Energy Agency (IAEA). The inverse square law was applied for distance calculations. Results are expressed as Calibration Factor (CF) and Relative Error (%).
```

**`TraceabilityReference`:**
```
Measurement traceability is established through a calibrated Farmer ionization chamber referenced to the International Atomic Energy Agency (IAEA).
```

**`Notes`:**
```
- The calibration results relate to the instrument configuration listed above.
- This certificate shall not be reproduced except in full, without the express written permission of the SSDL – TNRC laboratory management.
- The calibration results are valid only for the conditions and geometry specified.
- Traceability is maintained to the International Atomic Energy Agency (IAEA).
```

**`AdditionalInformation`:**
```
Calibration performed at reference distance of 1.0 meter (axis configuration). The instrument performance is within acceptable limits in accordance with laboratory procedures. This certificate is valid only for the instrument configuration and conditions specified. Measurements are traceable to the International Atomic Energy Agency (IAEA).
```

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`

| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio / Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Detector Response Check | Detector response within acceptable range when exposed to different radiation sources |

**مكوّنات عدم اليقين:** لا يوجد.

---

## ٤. Personal Electronic Dosimeter (PED)

| الحقل | القيمة |
|---|---|
| `DetectorType` | `Electronic Personal Dosimeter` |
| `CalibrationStandard` | `SSDL-TNRC Internal Calibration Procedure Ref.: SSDL-CP-PED in accordance with ISO/IEC 17025:2017 & IAEA standards.` |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` |
| `CorrectedReadingFormula` | `Corrected Dose = Measured Dose × CFavg` |
| `UncertaintyEnabled` | `true` |
| `MethodologyEnabled` | `true` |

**`MethodologyText`:**
```
Radiation Source: Cs-137 Point Source | Reference Standard: Farmer Ionization Chamber (IAEA Traceable)
Calibration was performed by exposing the Personal Electronic Dosimeter (PED) to gamma radiation fields produced by a calibrated Cs-137 source. Measurements were evaluated in terms of Personal Dose Measurement (µSv or mSv) against reference values. The Calibration Factor (CF) and Absolute Relative Error (AE) were determined. Corrected Reading = Measured Dose × CF.
```

**`AdditionalInformation`:**
```
The reported expanded uncertainty is based on a standard uncertainty multiplied by a coverage factor k = 2, providing a coverage probability of approximately 95%.
Measurements are traceable to the International Atomic Energy Agency (IAEA) standards.
This certificate shall not be reproduced except in full, without the express written permission of the SSDL - TNRC laboratory management.
The calibration results relate strictly and exclusively to the specific physical instrument identified by the serial number above.
```

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`

| # | CheckName | Requirement |
|---|---|---|
| 1 | Battery & Display Check | LCD operational, battery level normal |
| 2 | High Voltage / Detector Check | Operating voltage within specified range |
| 3 | Audio / Alarm Check | Audible and visual alarm operational |
| 4 | Visual Inspection | No physical damage to casing or clip |
| 5 | Dose Response Check | Response within acceptable range when exposed to radiation |

**مكوّنات عدم اليقين:** لا يوجد جدول مكوّنات — `ExpandedUncertainty` و`CoverageFactor` فقط.

---

## ٥. Dose Rate Meter

| الحقل | القيمة |
|---|---|
| `MeasurementType` | `Dose Rate Measurement (µSv/h)` |
| `Distance` | `1.0 meter` |
| `ReferenceGeometry` | `Distance 1.0 meter (Axis configuration)` |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CF` |
| `UncertaintyEnabled` | `false` |
| `MethodologyEnabled` | `true` |

**`MethodologyText`:** مطابق حرفياً لنص `Gamma Scintillation Probe` أعلاه.

**`TraceabilityReference`:** مطابق حرفياً لنص `Gamma Scintillation Probe` أعلاه.

**`Notes`:** مطابق حرفياً لنص `Gamma Scintillation Probe` أعلاه.

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`

| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio / Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument |
| 5 | Dose Rate Response Check | Detector response within acceptable range when exposed to different radiation sources |

**مكوّنات عدم اليقين:** لا يوجد.

---

## ملاحظات تنفيذية

**١. `RadiationSource` ليس قالباً.** يتغير بكل معايرة حسب المصادر المستعملة فعلاً — إدخال يدوي.

**٢. `StandardUncertainty` في القوالب فارغة عمداً.** المكوّن ثابت، والقيمة تتغير بكل معايرة.

**٣. `DefaultResult` يختلف:** `Yes` في Pancake · `Acceptable` في الباقي. يُبقى كما هو في كل نموذج بدل توحيده — النصّ يُطبع كما هو على الشهادة.

**٤. النصوص المكررة بين Gamma و DRM** تُزرع مرتين لا مرة. القالب لكل نوع مستقل، وتوحيدها يمنع تعديل أحدهما دون الآخر.

**٥. `[يحتاج تأكيد]` في Beta** — البنية تُزرع فارغة، والقيم تُملأ لاحقاً من الواجهة أو ببذر `_v2`. لا اختلاق.

**٦. اختلاف الشرطة بين النصين — منقول كما ورد، لا موحَّد:**
- `Gamma` / `Dose Rate Meter` في `Notes`: **شرطة طويلة** `SSDL – TNRC` (U+2013)
- `PED` في `AdditionalInformation`: **شرطة عادية** `SSDL - TNRC` (U+002D)

الفارق مقصود لأن النصين منقولان من نموذجين مختلفين. لا يُوحَّدان بلا تأكيد من م. رضا.
