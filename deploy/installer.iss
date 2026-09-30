; ============================================================================
; نظام معايرة أجهزة المسح الإشعاعي - CAL-QR — سكربت Inno Setup 6
; ============================================================================
; يُبنى بواسطة deploy\build-installer.ps1 الذي يمرر {#PublishDir} (مجلد نشر dotnet publish
; الذاتي الاكتفاء win-x64) عبر /DPublishDir=... — بنفس نمط مثبِّت منظومة مصادر.
;
; فصل البيانات عن البرنامج (قرار معماري ثابت):
;   • {app} (Program Files\CAL-QR) للقراءة فقط: ملفات البرنامج والشعارات والخطوط.
;   • بيانات المنظومة (القاعدة، المرفقات، مجلد QR، المؤشر db_path.txt) في
;     %ProgramData%\CAL-QR. يُنشئه المثبِّت ويمنح المستخدمين صلاحية التعديل عليه،
;     ويُدار كله بواسطة Helpers\AppPaths داخل البرنامج نفسه.
;   • لا [UninstallDelete] عمداً، ومجلد البيانات بعلامة uninsneveruninstall: إلغاء التثبيت
;     وإعادة التثبيت لا يمسّان بيانات المستخدم أبداً.
;   • لا توقيع كود (لا SignTool) — يظهر تحذير SmartScreen؛ تُطابَق بصمة SHA256 المرفقة.

#ifndef PublishDir
  #define PublishDir "..\CAL-QR\bin\Release\net8.0-windows\win-x64\publish"
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; AppId ثابت للأبد كي يتعرّف Inno Setup على التثبيت الحالي كترقية لا كتثبيت منفصل.
AppId={{55A64762-9F97-4A4A-932D-4304A858D0BB}
; مطابق حرفياً لاسم الـMutex في CAL-QR\App.xaml.cs — يمنع التثبيت والبرنامج يعمل.
AppMutex=CAL-QR-SingleInstance-Mutex
AppName=نظام معايرة أجهزة المسح الإشعاعي - CAL-QR
AppVersion={#AppVersion}
AppPublisher=مركز البحوث النووية
DefaultDirName={autopf}\CAL-QR
DefaultGroupName=CAL-QR
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
WizardImageFile=assets\wizard_large.bmp
WizardSmallImageFile=assets\wizard_small.bmp
OutputBaseFilename=CAL-QRSetup
OutputDir=output
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\CAL-QR.exe
SetupIconFile=..\CAL-QR\Assets\Logo\cal-qr-icon.ico

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Messages]
; إصلاح ظهور الأرقام بالهندية في شريط العنوان وصفحات المعالج: ويندوز يطبّق «استبدال الأرقام
; حسب السياق» فالرقم بعد كلمة عربية يُرسَم هندياً. علامة LRM غير مرئية (U+200E) قبل كل رقم متغيّر
; تجعله لاتينياً. نفس معالجة مثبِّت مصادر؛ باقي النص مطابق لملف Arabic.isl الرسمي.
AboutSetupMessage=%1 الإصدار ‎%2%n%3%n%n%1 صفحة الأنترنت:%n%4
WinVersionTooLowError=هذا البرنامج يتطلب %1 الإصدار ‎%2 أو أعلى.
WinVersionTooHighError=لا يمكن تثبيت هذا البرنامج على %1 الإصدار ‎%2 أو أعلى.
DiskSpaceGBLabel=تحتاج على الأقل ‎[gb] GB من المساحة لتثبيت البرنامج.
DiskSpaceMBLabel=تحتاج على الأقل ‎[mb] MB من المساحة لتثبيت البرنامج.
DiskSpaceWarning=يتطلب الإعداد على الأقل ‎%1 KB من المساحة الفارغة للتثبيت، ولكن محرك الأقراص المحدد فيه فقط ‎%2 KB متوفرة.%n%nهل تريد المتابعة على أية حال؟
InstallingLabel=يرجى الانتظار ريثما يقوم برنامج الإعداد بتثبيت [name] على جهازك.
FinishedLabelNoIcons=اكتمل معالج التثبيت من تثبيت [name] على جهازك.
ClickFinish=اضغط على إنهاء للخروج من معالج التثبيت.
SelectTasksLabel2=حدد المهام الإضافية التي ترغب في أن يقوم الإعداد بتنفيذها أثناء تثبيت [name]، ثم اضغط على التالي.
StatusCreateIcons=يجري إنشاء الاختصارات...
UninstallStatusLabel=يرجى الانتظار ريثما يتم إزالة تثبيت %1 من جهازك.

[CustomMessages]
; NameAndVersion مصدر عنوان شريط العنوان وصفحة الترحيب («الإصدار ‎1.0.0»).
NameAndVersion=%1 الإصدار ‎%2

[Tasks]
Name: "desktopicon"; Description: "إنشاء أيقونة على سطح المكتب"; GroupDescription: "أيقونات إضافية:"

[Dirs]
; مجلد بيانات المنظومة المشترك بين حسابات ويندوز (فلا ينقسم ترقيم الشهادات). صلاحية التعديل
; للمستخدمين ضرورية لأن Program Files للقراءة فقط وProgramData لا يسمح افتراضياً بالكتابة لغير المنشئ.
; uninsneveruninstall: لا يُحذف عند إلغاء التثبيت حتى لو كان فارغاً.
Name: "{commonappdata}\CAL-QR"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
; يُنسخ محتوى مجلد النشر (self-contained win-x64) بالكامل إلى {app} فقط. لا مرجع هنا لأي مسار بيانات.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CAL-QR"; Filename: "{app}\CAL-QR.exe"
Name: "{group}\{cm:UninstallProgram,CAL-QR}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CAL-QR"; Filename: "{app}\CAL-QR.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CAL-QR.exe"; Description: "{cm:LaunchProgram,CAL-QR}"; Flags: nowait postinstall skipifsilent
