# Runs inside Windows Sandbox only, on the fictional demo database. Takes every screenshot of the CAL-QR guide.
$ErrorActionPreference = "Continue"
$out = "C:\gw\shots\final"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Start-Transcript -Path "$out\log.txt" -Force | Out-Null
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$ae = [System.Windows.Automation.AutomationElement]
$tree = [System.Windows.Automation.TreeScope]
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$CT = [System.Windows.Automation.ControlType]

function ShotEl($el, $name) {
    $r = $el.Current.BoundingRectangle
    $x = [Math]::Max(0, [int]$r.X); $y = [Math]::Max(0, [int]$r.Y)
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $w = [Math]::Min([int]$r.Right, $b.Width) - $x; $h = [Math]::Min([int]$r.Bottom - 9, $b.Height) - $y
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
    $bmp.Save("$out\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "shot $name ${w}x${h}"
}
function FindId($id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($ae::AutomationIdProperty, $id)
    return $ae::RootElement.FindFirst($tree::Descendants, $c)
}
function ByName($root, $name) {
    $c = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $name)
    return $root.FindFirst($tree::Descendants, $c)
}
function Click-El($el) {
    $e = $el
    for ($i = 0; $i -lt 3 -and $e; $i++) {
        $ok = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$ok)) { $ok.Invoke(); return $true }
        if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$ok)) { $ok.Select(); return $true }
        $e = $walker.GetParent($e)
    }
    return $false
}
function Click-Name($root, $name, $wait = 3) {
    $t = ByName $root $name
    if (-not $t) { "MISSING: $name"; return $false }
    $r = Click-El $t
    Start-Sleep $wait
    return $r
}
function Tops($procId) {
    $c = New-Object System.Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $procId)
    return @($ae::RootElement.FindAll($tree::Children, $c))
}
# A modal form is a child window of the main one in this tree, or another top window.
function Dialog-Of($main, $procId) {
    $c = New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $CT::Window)
    foreach ($d in @($main.FindAll($tree::Descendants, $c))) { if ($d.Current.BoundingRectangle.Height -gt 200) { return $d } }
    foreach ($t in (Tops $procId)) { if ($t.Current.NativeWindowHandle -ne $main.Current.NativeWindowHandle -and $t.Current.BoundingRectangle.Height -gt 200) { return $t } }
    return $null
}
function Close-Dialog($d) {
    try { $d.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch { "close failed: $_" }
    Start-Sleep 2
}
# Further shots of the largest scrollable pane: top is the normal shot, then 50 and 100 percent.
function Scroll-Shots($root, $name) {
    $sc = New-Object System.Windows.Automation.PropertyCondition($ae::IsScrollPatternAvailableProperty, $true)
    $best = $null; $area = 0
    foreach ($e in @($root.FindAll($tree::Descendants, $sc))) {
        $pat = $e.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        if (-not $pat.Current.VerticallyScrollable) { continue }
        $r = $e.Current.BoundingRectangle
        if ($r.Width * $r.Height -gt $area) { $area = $r.Width * $r.Height; $best = $pat; $bestEl = $e }
    }
    if (-not $best) { "no scroll in $name"; return }
    $view = $best.Current.VerticalViewSize
    "scroll $name view=$view"
    if ($view -ge 100) { return }
    # steps overlap by 30 percent of the pane, so the shots can be joined by matching rows
    $step = [Math]::Max(1.0, 70.0 * $view / (100.0 - $view))
    $k = 0; $v = 0.0
    while ($v -lt 100) {
        $v = [Math]::Min(100.0, $v + $step); $k++
        $best.SetScrollPercent(-1, $v); Start-Sleep 2
        ShotEl $root ("{0}_p{1:00}" -f $name, $k)
    }
    $best.SetScrollPercent(-1, 0); Start-Sleep 1
    $pr = $bestEl.Current.BoundingRectangle; $rr = $root.Current.BoundingRectangle
    $ry = [Math]::Max(0, [int]$rr.Y); $rx = [Math]::Max(0, [int]$rr.X)
    Set-Content "$out\$name.pane.txt" ("{0} {1} {2} {3} {4}" -f ([int]$pr.X - $rx), ([int]$pr.Y - $ry), [int]$pr.Width, [int]$pr.Height, $k)
}
function Dialog-Shot($main, $procId, $name) {
    $d = Dialog-Of $main $procId
    if (-not $d) { "NO DIALOG for $name"; return }
    ShotEl $d $name
    Scroll-Shots $d $name
    Close-Dialog $d
    $again = Dialog-Of $main $procId
    if ($again) { "still open after $name : " + $again.Current.Name; ShotEl $again "${name}_extra"; Close-Dialog $again }
}
function Nav($main, $name) {
    $t = ByName $main $name
    if (-not $t) { "nav missing: $name"; return }
    [void](Click-El $t)
    Start-Sleep 4
}

Start-Sleep 10
"screen: " + [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
New-Item -ItemType Directory -Force -Path "C:\ProgramData\CAL-QR", "C:\demo" | Out-Null

# ---- 1. first-run wizard, on an empty machine; nothing is saved: the process is stopped before the last step
$p = Start-Process "C:\gw\app\CAL-QR.exe" -PassThru
$next = $null
for ($i = 0; $i -lt 60 -and -not $next; $i++) { Start-Sleep 2; $next = FindId "BtnNext" }
if ($next) {
    $wiz = (Tops $p.Id)[0]
    ShotEl $wiz "wizard1"
    foreach ($s in 2, 3) { [void](Click-El (FindId "BtnNext")); Start-Sleep 2; ShotEl $wiz "wizard$s" }
} else { "wizard not shown" }
Stop-Process -Id $p.Id -Force
Start-Sleep 3
Get-ChildItem "C:\ProgramData\CAL-QR" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# ---- 2. the demo database
Copy-Item "C:\gw\demo\cal-qr-DEMO.db" "C:\demo\cal-qr-DEMO.db" -Force
[System.IO.File]::WriteAllText("C:\ProgramData\CAL-QR\db_path.txt", "C:\demo\cal-qr-DEMO.db")
$p = Start-Process "C:\gw\app\CAL-QR.exe" -PassThru
$login = $null
for ($i = 0; $i -lt 90 -and -not $login; $i++) { Start-Sleep 2; $login = FindId "BtnLogin" }
"login found: $([bool]$login)"
ShotEl (Tops $p.Id)[0] "login"
(FindId "TxtUsername").SetFocus(); Start-Sleep 1
[System.Windows.Forms.SendKeys]::SendWait("admin"); Start-Sleep 1
(FindId "TxtPassword").SetFocus(); Start-Sleep 1
[System.Windows.Forms.SendKeys]::SendWait("Demo@12345"); Start-Sleep 1
[void](Click-El $login)
Start-Sleep 12

$main = (Tops $p.Id)[0]
"main name: " + $main.Current.Name
$alert = Dialog-Of $main $p.Id
if ($alert) { ShotEl $alert "alert"; [void](Click-Name $alert "إغلاق" 2) }
Start-Sleep 2
$main = (Tops $p.Id)[0]
try { $main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState("Maximized"); Start-Sleep 3 } catch { "maximize: $_" }
"main rect: " + $main.Current.BoundingRectangle

Nav $main "الرئيسية"; ShotEl $main "dashboard"

Nav $main "السجلات"; ShotEl $main "records"
[void](Click-Name $main "بحث متقدم"); ShotEl $main "records_search"; [void](Click-Name $main "بحث متقدم")
[void](Click-Name $main "تغيير العرض"); ShotEl $main "records_cards"; [void](Click-Name $main "تغيير العرض")
[void](Click-Name $main "سجل جديد" 4); Dialog-Shot $main $p.Id "record_new"
# the first row of the table: details, edit, certificate ... in the order of the row's buttons
$row = $main.FindFirst($tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $CT::DataItem)))
if ($row) {
    $btns = @($row.FindAll($tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $CT::Button))))
    "row buttons: " + $btns.Count
    $k = 0
    foreach ($b in $btns) {
        $k++
        [void](Click-El $b); Start-Sleep 4
        Dialog-Shot $main $p.Id ("record_btn{0}" -f $k)
        $row = $main.FindFirst($tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $CT::DataItem)))
        if (-not $row) { break }
        $btns2 = @($row.FindAll($tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $CT::Button))))
        if ($k -ge $btns2.Count) { break }
    }
}

Nav $main "التحقق"; ShotEl $main "verify"

Nav $main "الجهات"; ShotEl $main "owners"
[void](Click-Name $main "إضافة جهة" 4); Dialog-Shot $main $p.Id "owner_new"

Nav $main "الأنواع"; ShotEl $main "types"
[void](Click-Name $main "إضافة نوع جهاز" 4); Dialog-Shot $main $p.Id "type_new"

Nav $main "التقارير"; ShotEl $main "reports"
[void](Click-Name $main "تقرير أداء وحدة المعايرة"); ShotEl $main "reports_perf"

Nav $main "المستخدمين"; ShotEl $main "users"
[void](Click-Name $main "إضافة مستخدم جديد" 4); Dialog-Shot $main $p.Id "user_new"
[void](Click-Name $main "سجل العمليات والأحداث"); ShotEl $main "audit"

Nav $main "الإعدادات"
foreach ($c in "CardSecurity", "CardPaths", "CardPrint", "CardBackup", "CardGeneral", "CardReset") {
    $el = FindId $c
    if ($el) { [void](Click-El $el); Start-Sleep 3; ShotEl $main ("settings_" + $c); Scroll-Shots $main ("settings_" + $c) } else { "missing $c" }
}
Nav $main "المساعدة"; ShotEl $main "help"
Nav $main "عن البرنامج"; ShotEl $main "about"
Stop-Transcript | Out-Null
Set-Content "C:\gw\shots\done2.txt" "done"
