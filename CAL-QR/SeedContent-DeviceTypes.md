# CAL-QR — محتوى بذر قوالب `DeviceType` — الأنواع الخمسة (مدموج)

> **الحالة:** 🟢 **المرجع الوحيد للبذر.** يحلّ محلّ النسختين السابقتين (كلتاهما محذوفة):
> `CAL-QR/SeedContent-DeviceTypes.md` (١٤٣ سطراً) و `d:\cal-qr\SeedContent-DeviceTypes.md` (٢٣٠ سطراً).
> **كلتاهما تُحذف بعد اعتماد هذا الملف.**
> **تاريخ الدمج:** 30 يوليو 2026

## مصادر الدمج
| المكوّن | المصدر |
|---|---|
| **النصوص** (منهجية · أحكام · تتبعية · ملاحظات · فحوص) | ✅ قوالب رضا النهائية (`B101` · `Beta_Scintillation_Probe` · `G501` · `PED` · `Dose_Rate_Meter`) |
| **البنية** (مرادفات · قاعدة إعادة التسمية · `DefaultResult` · الحقول القائمة) | ✅ المرجع الأقدم — أدقّ من v2 الملغاة |
| ❌ **قيم من مراجع أقدم** | **مُزالة بالكامل.** المصدر الوحيد هو القوالب الخمسة المرفوعة؛ ما لا يظهر فيها **لا يُبذَر** |

⚠ **قاعدة مُلزِمة:** بند «This certificate shall not be reproduced except in full» **محذوف من الأنواع الخمسة** بقرار إدريس.
⚠ `RadiationSource` **ليس قالباً** — إدخال يدوي على الشهادة (يتغيّر بكل معايرة حسب المصادر المتوفّرة).

---

## ⚠ أسماء الأنواع — الاسم المعتمد ومرادفاته

الاسم يُنسخ إلى `CertificateTemplateType` ويُطبع ويدخل التوقيع، فيجب أن يطابق عنوان النموذج.

| # | الاسم المعتمد | المرادفات القديمة (في القاعدة حالياً) |
|---|---|---|
| 1 | `Pancake Probe` | — |
| 2 | `Beta Scintillation Probe` | `Beta Scintillator Probe` |
| 3 | `Gamma Scintillation Probe` | `Gamma Probe` |
| 4 | `Personal Electronic Dosimeter (PED)` | `PED` |
| 5 | `Dose Rate Meter` | — |

**قاعدة المطابقة عند البذر:** يُبحث بالاسم المعتمد، فإن لم يوجد يُبحث بالمرادفات.
**عند إصابة مرادف: يُعاد تسمية الصفّ القائم، ولا يُنشأ صفّ جديد** — حفاظاً على `DeviceTypeId` وكل الأجهزة المرتبطة به.

إعادة التسمية لا تمسّ شهادة صادرة: `CertificateTemplateType` منسوخ نصاً وقت الإصدار.

---

## ثابت تطبيقي مشترك (ليس حقل قالب — لا يُبذَر في `DeviceType`)

`ComplianceStatement` — ثابت في الكود، ثنائي اللغة، يُطبع في الأنواع الخمسة، **خارج `SIG1`** (v3 §٤):
```
The calibration was performed in accordance with the technical procedures approved by the International Atomic Energy Agency (IAEA) and in compliance with the relevant technical principles and requirements of ISO/IEC 17025:2017

أُجريت المعايرة وفقًا لإجراءات العمل الفنية المعتمدة من الوكالة الدولية للطاقة الذرية (IAEA)، وبما يتوافق مع المبادئ والمتطلبات الفنية ذات الصلة للمواصفة الدولية ISO/IEC 17025:2017
```

---

## ١. Pancake Probe

| الحقل | القيمة | المصدر |
|---|---|---|
| `ProcedureNo` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `CalibrationLocation` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `CalibrationStandard` | `Certified Sr-90/Y-90 Reference Beta Sources` | ✅ قالب رضا |
| `ReferenceGeometry` | `Direct Contact Geometry` | ✅ قالب رضا |
| `CalibrationMode` | `Direct Contact Geometry` | ✅ قالب رضا — **حقل جديد (v3 §٤ بند ١)** |
| `CountingTime` | `60 Sec` | ✅ قالب رضا |
| `CountingUnit` | `kCPM` | ✅ قالب رضا |
| `Instrumentation` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL USE` | ✅ قالب رضا |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CFavg` | ✅ قالب رضا |
| `TraceabilityReference` | `Measurement traceability is established through certified Sr-90/Y-90 reference Beta sources maintained by the Secondary Standard Dosimetry Laboratory (SSDL).` | ✅ قالب رضا |
| `UncertaintyEnabled` | `true` | |
| `MethodologyEnabled` | `true` | |

**`Notes`** (بعد حذف بند «عدم النسخ»):
```
The reported expanded uncertainty is based on a standard uncertainty multiplied by a coverage factor k = 2, providing a coverage probability of approximately 95%.
The calibration results relate strictly and exclusively to the specific physical instrument identified by the serial number above.
```

**الفحوصات الوظيفية** — `DefaultResult = Yes`
| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio/Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Detector Window Inspection | Window clean and free from damage |

**مكوّنات عدم اليقين** — `StandardUncertainty` و`ContributionPercent` تُتركان فارغتين (تتغيّران بكل معايرة)
| # | ComponentName | EvaluationType | Distribution |
|---|---|---|---|
| 1 | Source (calibration certificate) | Type B | Normal |
| 2 | Radioactive decay | Type B | Normal |
| 3 | Counting statistics | Type A | Poisson |
| 4 | Repeatability (measurement) | Type A | Normal |
| 5 | Geometry and positioning | Type B | Rectangular |

---

## ٢. Beta Scintillation Probe

> ✅ **البند المعلّق مُغلق:** OCR كان تالفاً في المرجع الأقدم؛ القالب الفعلي وصل كاملاً. النصوص أدناه حقيقية، لا `[يحتاج تأكيد]`.

| الحقل | القيمة | المصدر |
|---|---|---|
| `ProcedureNo` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `CalibrationLocation` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `CalibrationStandard` | `Certified Sr-90/Y-90 Reference Beta Sources` | ✅ قالب رضا |
| `ReferenceGeometry` | `Direct Contact Geometry` | ✅ قالب رضا |
| `CalibrationMode` | `Direct Contact Geometry` | ✅ قالب رضا |
| `CountingTime` | `60 Sec` | ✅ قالب رضا |
| `CountingUnit` | `kCPM` | ✅ قالب رضا |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL USE` | ✅ قالب رضا |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CFavg` | ✅ قالب رضا |
| `TraceabilityReference` | `Measurement traceability is established through certified Sr-90/Y-90 reference Beta sources maintained by the Secondary Standard Dosimetry Laboratory (SSDL).` | ✅ قالب رضا |
| `UncertaintyEnabled` | `true` | |
| `MethodologyEnabled` | `true` | |

**`Notes`:** مطابق لـ`Pancake` أعلاه (بندان، بلا «عدم النسخ»). **يُزرع نصاً مستقلاً لا مشتركاً.**

**الفحوصات الوظيفية** — `DefaultResult = Yes`
الصفوف ١–٤ مطابقة لـ`Pancake`. **الصفّ ٥ يختلف:**
| # | CheckName | Requirement |
|---|---|---|
| 5 | Probe Window Inspection | Window clean and free from damage |

**مكوّنات عدم اليقين:** الخمسة، مطابقة لـ`Pancake` حرفياً (تُزرع مستقلة).

---

## ٣. Gamma Scintillation Probe

| الحقل | القيمة | المصدر |
|---|---|---|
| `ReferenceGeometry` | `Distance = 1.0 meter (Axis configuration)` | ✅ قالب رضا |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` | ✅ قالب رضا |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CF` | ✅ قالب رضا |
| `TraceabilityReference` | `Measurement traceability is established through a calibrated Farmer ionization chamber referenced to the International Atomic Energy Agency (IAEA).` | ✅ قالب رضا |
| `UncertaintyEnabled` | `false` | |
| `MethodologyEnabled` | `true` | |

✅ **`MethodologyText` — نصّ رضا حرفياً (`Co-60`):**
```
The calibration was performed using an instrument-specific validated method. The calibration was carried out using a Co-60 point gamma source. Reference dose rates were determined using a calibrated Farmer ionization chamber traceable to the International Atomic Energy Agency (IAEA). The inverse square law was applied for distance calculation. Results are expressed as calibration factor (CF) and absolute relative error (AE).
```
> ⚠ **النويدة مثبَّتة في نصّ رضا المعتمد.** إن عُويِر الجهاز بنويدة أخرى أو بأكثر من واحدة، **يُعدّل `MethodologyText` يدوياً على الشهادة قبل الحفظ** (الحقل قابل للتحرير). النويدات المستعملة فعلاً تظهر أيضاً في `RadiationSource` وصفوف جدول النتائج.

**`Notes`** (٣ بنود، بعد حذف «عدم النسخ»):
```
The calibration results relate to the instrument configuration listed above.
The calibration results are valid only for the conditions and geometry specified.
Traceability is maintained to the International Atomic Energy Agency (IAEA).
```

**`AdditionalInformation`** (٤ بنود):
```
Calibration performed at reference distance of 1.0 meter (axis configuration).
The instrument performance is within acceptable limits in accordance with laboratory procedures.
This certificate is valid only for the instrument configuration and conditions specified.
Measurements are traceable to the International Atomic Energy Agency (IAEA).
```

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`
| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio/Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Detector Response Check | Detector response within acceptable range when exposed to different radiation sources |

**مكوّنات عدم اليقين:** لا يوجد.

---

## ٤. Personal Electronic Dosimeter (PED)

> 🔴 **تصحيحان جوهريان على المزروع حالياً:**
> ١. `UncertaintyEnabled` كان `true` مع «`ExpandedUncertainty` و`CoverageFactor` فقط» — هذه **عائلة «ب» الملغاة**. قالب رضا الفعلي **بلا عدم يقين إطلاقاً** ⇒ `false`.
> ٢. `CorrectedReadingFormula` كان `× CFavg` — القالب يقول `× CF`.

| الحقل | القيمة | المصدر |
|---|---|---|
| `DetectorType` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `CalibrationStandard` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `MeasurementType` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `Distance` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة (المسافة داخل نصّ `ReferenceGeometry` فقط) |
| `ReferenceGeometry` | `Distance = 1.0 meter (Axis configuration)` | ✅ قالب رضا |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` | ✅ قالب رضا |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CF` | ✅ قالب رضا — **مصحّح** |
| `TraceabilityReference` | `Measurement traceability is established through a calibrated Farmer ionization chamber referenced to the International Atomic Energy Agency (IAEA).` | ✅ قالب رضا |
| `UncertaintyEnabled` | **`false`** | ✅ **مصحّح** |
| `MethodologyEnabled` | `true` | |

✅ **`MethodologyText` — نصّ رضا حرفياً بعد تصحيحه (`Co-60`):**
```
The calibration was performed using an instrument-specific validated method. The calibration was carried out using a Co-60 point gamma source. Reference dose rates were determined using a calibrated Farmer ionization chamber traceable to the International Atomic Energy Agency (IAEA). The inverse square law was applied for distance calculation. Results are expressed as calibration factor (CF) and absolute relative error (AE).
```
> ✅ **البند المعلّق مُغلق.** التضارب في قالب رضا (خانة المصدر `Co-60` / المنهجية `Cs-137`) كان **خطأ كتابة في المنهجية**. أكّد رضا أن الصحيح **`Co-60` في الموضعين**.
> `RadiationSource` المرجعي (لا يُبذَر، إدخال يدوي): `The calibration was performed using a Co-60 reference gamma source.`
> ⚠ **النويدة مثبَّتة في نصّ رضا المعتمد.** إن عُويِر الجهاز بنويدة أخرى أو بأكثر من واحدة، **يُعدّل `MethodologyText` يدوياً على الشهادة قبل الحفظ** (الحقل قابل للتحرير). النويدات المستعملة فعلاً تظهر أيضاً في `RadiationSource` وصفوف جدول النتائج.

**`Notes`** (بعد حذف «عدم النسخ»): مطابق لـ`Gamma` (٣ بنود).

**`AdditionalInformation`:** مطابق لـ`Gamma` (٤ بنود).

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`
| # | CheckName | Requirement |
|---|---|---|
| 1 | Battery & Display Check | LCD operational, battery level normal |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio/Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Dose Response Check | Response within acceptable range when exposed to radiation |

**مكوّنات عدم اليقين:** لا يوجد.

---

## ٥. Dose Rate Meter

| الحقل | القيمة | المصدر |
|---|---|---|
| `MeasurementType` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة |
| `Distance` | — | ❌ **لا يُبذَر** — غير موجود في القوالب الخمسة (المسافة داخل نصّ `ReferenceGeometry` فقط) |
| `ReferenceGeometry` | `Distance = 1.0 meter (Axis configuration)` | ✅ قالب رضا |
| `ComplianceVerdict` | `APPROVED FOR OPERATIONAL RADIATION SAFETY USE` | ✅ قالب رضا |
| `CorrectedReadingFormula` | `Corrected Reading = Measured Reading × CF` | ✅ قالب رضا |
| `TraceabilityReference` | `Measurement traceability is established through a calibrated Farmer ionization chamber referenced to the International Atomic Energy Agency (IAEA).` | ✅ قالب رضا |
| `UncertaintyEnabled` | `false` | |
| `MethodologyEnabled` | `true` | |

✅ **`MethodologyText` — نصّ رضا حرفياً (`Cs-137`):**
```
The calibration was performed using an instrument-specific validated method. The calibration was carried out using a Cs-137 point gamma source. Reference dose rates were determined using a calibrated Farmer ionization chamber traceable to the International Atomic Energy Agency (IAEA). The inverse square law was applied for distance calculation. Results are expressed as calibration factor (CF) and absolute relative error (AE).
```
> ⚠ **النويدة مثبَّتة في نصّ رضا المعتمد.** إن عُويِر الجهاز بنويدة أخرى أو بأكثر من واحدة، **يُعدّل `MethodologyText` يدوياً على الشهادة قبل الحفظ** (الحقل قابل للتحرير). النويدات المستعملة فعلاً تظهر أيضاً في `RadiationSource` وصفوف جدول النتائج.

**`Notes`:** مطابق لـ`Gamma` (٣ بنود، بلا «عدم النسخ»).
**`AdditionalInformation`:** مطابق لـ`Gamma` (٤ بنود).

**الفحوصات الوظيفية** — `DefaultResult = Acceptable`
| # | CheckName | Requirement |
|---|---|---|
| 1 | Background Check | Count rate within normal background limits |
| 2 | High Voltage Check | Operating voltage within specified range |
| 3 | Audio/Alarm Check | Audible alarm operational |
| 4 | Visual Inspection | No physical damage to instrument and probe |
| 5 | Dose Rate Response Check | Detector response within acceptable range when exposed to different radiation sources |

**مكوّنات عدم اليقين:** لا يوجد.

---

## ملاحظات تنفيذية

**١. `RadiationSource` ليس قالباً.** يتغيّر بكل معايرة حسب المصادر المستعملة فعلاً — إدخال يدوي على الشهادة. **لا يُبذَر.**

**١-ب. صياغة رضا هي الأساس — لا تُبدَّل.** نصوص المنهجية تُبذَر **حرفياً** كما اعتمدها، بنويداتها المثبَّتة. صياغة بديلة بلا نويدة كانت مقترحة ثم **رُفضت**: النصّ في شهادة ISO/IEC 17025 معتمد بصياغته لا بمعناه فقط، ولا يُستبدل باستنباط.
⚠ **الأثر التشغيلي:** عند المعايرة بنويدة مختلفة أو بأكثر من واحدة، **يُعدّل `MethodologyText` يدوياً على الشهادة قبل الحفظ**.

**١-ج. قاعدة تحقّق: نصّ القالب مقابل الحقل المتغيّر** (تُنفَّذ في المرحلة ٣).
أي نصّ قالب سرديّ يحوي **قيمة متغيّرة** يجب أن يُقارَن آلياً بالحقل الذي يحملها:

| نصّ القالب | يُثبّت | يُقارَن بـ |
|---|---|---|
| `MethodologyText` | النويدة (`Co-60` …) | `Radionuclide` في صفوف النتائج · `RadiationSource` |
| `ReferenceGeometry` | المسافة (`1.0 meter`) | `Distance` |

**السلوك:** تطابق ⇒ صمت تام. اختلاف ⇒ **شريط تحذير أصفر داخل حوار الشهادة** (لا نافذة منبثقة، لا تذكير دائم — التنبيه المتكرّر يُغلَق آلياً فيفقد معناه). **غير حاجب للحفظ**: المختصّ صاحب القرار، لكن التحذير يبقى مرئياً حتى يُحلّ.

**زرّ إصلاح بضغطة — بحدّ صارم:**
- ✅ **قيمة واحدة:** استبدال آمن داخل جملة رضا نفسها (يتغيّر الرمز فقط، الصياغة محفوظة).
- ❌ **أكثر من واحدة:** لا زرّ. الجمع يغيّر النحو («a Co-60 point gamma source» ⇒ «Co-60 and Cs-137 point gamma sources») وهو **تأليف نصّ غير معتمد**. تحرير يدوي فقط.

**٢. `StandardUncertainty` و`ContributionPercent` فارغتان عمداً.** المكوّن ثابت، والقيمة تتغيّر بكل معايرة.

**٣. `DefaultResult` يختلف:** `Yes` في Pancake وBeta · `Acceptable` في الثلاثة الباقية. **لا يُوحَّد** — النصّ يُطبع كما هو.

**٤. النصوص المتشابهة تُزرع مستقلة لا مشتركة.** القالب لكل نوع مستقلّ؛ التوحيد يمنع تعديل أحدها دون الآخر.

**٥. بند «عدم النسخ إلا كاملة» محذوف من الأنواع الخمسة** بقرار إدريس. إن وُجد في `DeviceTypeCatalog.cs` المزروع، **يُزال**.

**٦. تصحيحات على المزروع حالياً في `DeviceTypeCatalog.cs`:**
| # | الخطأ المزروع | التصحيح |
|---|---|---|
| 1 | Gamma: «Co-60, Cs-137 and point gamma source» (وصف عام) | **`Co-60`** محدّداً — نصّ رضا |
| 2 | Gamma: «Relative Error» · DRM: «مطابق لـGamma» | «absolute relative error (AE)» · DRM نصّ مستقلّ بـ**`Cs-137`** |
| 3 | Beta: `[يحتاج تأكيد]` فارغ | النصوص الحقيقية أعلاه |
| 4 | PED: `UncertaintyEnabled = true` | **`false`** — لا عدم يقين |
| 5 | PED: `× CFavg` | **`× CF`** |
| 6 | Pancake: «Instrument complies with laboratory acceptance criteria.» | **APPROVED FOR OPERATIONAL USE** |
| 7 | بند «shall not be reproduced except in full» | **محذوف** |

**٧. الشرطة في `SSDL – TNRC`:** بعد حذف بند «عدم النسخ» (موضعها الوحيد)، **المسألة زالت**. لا توحيد مطلوب.

---

## ملخّص المعلّقات
✅ **لا معلّقات — صفر.**

المصدر الوحيد هو القوالب الخمسة المرفوعة. الحقول التي لا تظهر فيها (`ProcedureNo` · `CalibrationLocation` · `Instrumentation` · `DetectorType` · `MeasurementType` · `Distance`) **لا تُبذَر**، وتبقى `NULL` — وهو المعنى المقصود في `DeviceType`: «لا قالب لهذا الحقل في هذا النوع». لا سؤال معلّق لرضا.

⚠ **`Manufacturer` لا يُبذَر أيضاً.** ظهر في القوالب البسيطة بقيم أجهزة بعينها (`Ludlum` · `Mirion/Thermo` · `Thermo Scientific`) — وهي **بيانات جهاز لا قالب نوع**. تُدخل مع الجهاز.

### النويدات المزروعة
| النوع | النويدة |
|---|---|
| Gamma Scintillation Probe | `Co-60` |
| Personal Electronic Dosimeter (PED) | `Co-60` |
| Dose Rate Meter | `Cs-137` |
