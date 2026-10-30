<#
  UI smoke test for Akro Tool SK (Windows only, PowerShell 5.1, no extra modules).
  Drives the real app through Windows UI Automation:
    startup dialogs -> athlete name -> fills every Okno (team rows first, then Inv) -> Tlačiť -> saves the PDF.
  Screenshots and the PDF go to -OutDir. The host must be logged in and unlocked.

  Usage:
    .\tools\ui-smoke-test.ps1 -OutDir C:\temp\smoke [-Exe <path>] [-PdfName out.pdf] [-NoPrint]
                              [-AgeGroup DO_8_ROK] [-Category MG] [-SeparateOkno 1,7]
  -AgeGroup is the Rulebook enum name (DO_8_ROK, DO_10_ROK, DO_14_ROK, OD_8_14_ROK, INDIVIDUAL),
  -Category the radio button text (WP, MP, MxP, WG, MG).
  -SeparateOkno fills these Okno with "Zvlášť" (one Inv exercise per team member, Top -> Base): every member
  takes the first tile of the row, the last member the second tile (MG: 0.010, 0.010, 0.010, 0.020 -> 0.013).
  At most as many as the age group has Inv picks.
  -BirthYears sets the year ComboBoxes, e.g. @{ Top = '2019'; Base = '2012'; Middle1 = '2000' }.
  -AgeWarning: answer to the age warning before printing — PrintAnyway ("Tlačiť aj tak") or Ok (don't print).
  -HistoryRoundTrip (#45): after printing, checks the history entry, clears the table, adds fake entries (older ones
  for the month separator, an unknown age group, a damaged file), removes one, loads the printed plan back from
  História, prints it again as <PdfName>_reloaded.pdf and compares both PDFs page by page (rendered pixels).
  The app always uses <OutDir>\history (AKRO_TOOL_HISTORY_DIR), never the real history; it is emptied at the start.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\Akro Tool SK.exe'),
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$PdfName = 'smoke.pdf',
    [switch]$NoPrint,
    [string]$AgeGroup,
    [string]$Category,
    [int[]]$SeparateOkno = @(),
    [hashtable]$BirthYears = @{},
    [ValidateSet('PrintAnyway', 'Ok')][string]$AgeWarning = 'PrintAnyway',
    [switch]$HistoryRoundTrip
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
if (-not ('SmokeNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class SmokeNative {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
'@
}
$AE   = [System.Windows.Automation.AutomationElement]
$TS   = [System.Windows.Automation.TreeScope]
$CT   = [System.Windows.Automation.ControlType]
$Walk = [System.Windows.Automation.TreeWalker]::ControlViewWalker
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Shot($name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    "  screenshot: $name.png"
}
function AppWindows {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $script:proc.Id)
    @($AE::RootElement.FindAll($TS::Children, $c))
}
function WaitFor([scriptblock]$find, [string]$what, [int]$sec = 15) {
    $end = (Get-Date).AddSeconds($sec)
    while ((Get-Date) -lt $end) { $r = & $find; if ($r) { return $r }; Start-Sleep -Milliseconds 250 }
    throw "Timed out waiting for: $what"
}
function FindAllOfType($scope, $type) {
    @($scope.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $type))))
}
function FindWindowByTitle([string]$pattern) {
    foreach ($w in AppWindows) { foreach ($x in @($w) + (FindAllOfType $w $CT::Window)) { if ($x.Current.Name -match $pattern) { return $x } } }
}
function FindButton($scope, [string]$name) { FindAllOfType $scope $CT::Button | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1 }
function FindButtonByLabel($scope, [string]$pattern) {
    $t = FindAllOfType $scope $CT::Text | Where-Object { $_.Current.Name -match $pattern } | Select-Object -First 1
    while ($t -and $t.Current.ControlType -ne $CT::Button) { $t = $Walk.GetParent($t) }
    $t
}
# Mouse over the element (real cursor, so hover styles show), screenshot, cursor back to the corner
function HoverShot($el, [string]$name) {
    $r = $el.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point ([int]($r.X + $r.Width / 2)), ([int]($r.Y + $r.Height / 3))
    Start-Sleep -Milliseconds 600
    Shot $name
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point 5, 5
}
function Click($el)           { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function SetText($el, $value) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value) }
function MainTexts { FindAllOfType $script:main $CT::Text | ForEach-Object { $_.Current.Name } }
# Zvlášť picker header, e.g. "Ján Novák (Base) – 1/2"
function SubtitleOf($grid) { FindAllOfType $grid $CT::Text | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match '\d+/\d+$' } | Select-Object -First 1 }
function TileButton($text) { $t = $text; while ($t.Current.ControlType -ne $CT::Button) { $t = $Walk.GetParent($t) }; $t }
function SelectItem($el) { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 300 }
# Real mouse click (a menu item opening a modal window would block a UIA Invoke)
function MouseClick($el) {
    $r = $el.Current.BoundingRectangle
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point ([int]($r.X + $r.Width / 2)), ([int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 200
    [SmokeNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); [SmokeNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point 5, 5
}
# MessageBox of the app: answered with Enter = the default button (OK / Yes; the button texts follow the Windows language)
function AnswerMessageBox([string]$title) {
    $box = WaitFor { FindWindowByTitle "^$title$" } "message box '$title'"
    $text = (FindAllOfType $box $CT::Text | ForEach-Object { $_.Current.Name }) -join ' / '
    $hwnd = [SmokeNative]::FindWindow([NullString]::Value, $title)
    if ($hwnd -ne [IntPtr]::Zero) { [SmokeNative]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 300 }
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    WaitFor { -not (FindWindowByTitle "^$title$") } "message box '$title' closed" | Out-Null
    $text
}
# História -> Súťažné plány...
function OpenHistory {
    $menu = FindAllOfType $script:main $CT::MenuItem | Where-Object { $_.Current.Name -eq 'História' } | Select-Object -First 1
    $menu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $item = WaitFor { foreach ($w in AppWindows) { FindAllOfType $w $CT::MenuItem | Where-Object { $_.Current.Name -eq 'Súťažné plány...' } | Select-Object -First 1 } } 'menu item Súťažné plány...'
    MouseClick $item
    WaitFor { FindWindowByTitle '^História' } 'history window'
}
function HistoryItems($win) { @(FindAllOfType $win $CT::ListItem) }
function ItemText($item) { (FindAllOfType $item $CT::Text | ForEach-Object { $_.Current.Name }) -join ' | ' }
function HistoryFiles { @(Get-ChildItem $historyDir -Filter *.json -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'broken.json' }) }
# Rendered pages of two PDFs have the same pixels
function SamePdf([string]$a, [string]$b) {
    $render = Join-Path $OutDir 'render'
    $pa = @(& (Join-Path $PSScriptRoot 'render-pdf.ps1') -Pdf $a -OutDir $render)
    $pb = @(& (Join-Path $PSScriptRoot 'render-pdf.ps1') -Pdf $b -OutDir $render)
    if ($pa.Count -ne $pb.Count) { return $false }
    for ($i = 0; $i -lt $pa.Count; $i++) { if ((PixelHash $pa[$i]) -ne (PixelHash $pb[$i])) { return $false } }
    $true
}
function PixelHash([string]$png) {
    $bmp = New-Object System.Drawing.Bitmap $png
    try {
        $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bytes = New-Object byte[] ($data.Stride * $data.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $bmp.UnlockBits($data)
        "$($bmp.Width)x$($bmp.Height):" + [Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes))
    } finally { $bmp.Dispose() }
}

# Tlačiť -> (age warning) -> save dialog -> PDF; $script:printed = false when the age warning stopped printing
function PrintTo([string]$pdf) {
    $script:printed = $false
    if (Test-Path $pdf) { Remove-Item $pdf }
    Click (FindButton $script:main 'Tlačiť')
    $save = WaitFor { FindWindowByTitle 'Uložiť vyplnený PDF plán|Upozornenie' } 'save dialog or age warning'

    if ($save.Current.Name -match 'Upozornenie') {
        Start-Sleep -Milliseconds 500
        $warning = (FindAllOfType $save $CT::Text | ForEach-Object { $_.Current.Name }) -join ' / '
        "age warning: $($warning -replace '\r?\n', ' | ')"
        Shot '3_age_warning'
        if ($AgeWarning -eq 'Ok') {
            Click (FindButton $save 'OK')
            Start-Sleep -Milliseconds 800
            if (FindWindowByTitle 'Uložiť vyplnený PDF plán') { throw 'Save dialog opened after OK' }
            "OK -> nothing printed"
            return
        }
        Click (FindButton $save 'Tlačiť aj tak')
        $save = WaitFor { FindWindowByTitle 'Uložiť vyplnený PDF plán' } 'save dialog'
    }
    else { "no age warning" }
    # The common file dialog is not fully visible under the WPF window in UI Automation, but it is when
    # opened by its window handle: 1001 = file name box, 1 = Save. Fallback: type the path + Enter.
    Start-Sleep -Milliseconds 1500
    $hwnd = [SmokeNative]::FindWindow([NullString]::Value, 'Uložiť vyplnený PDF plán')
    $nameBox = $null; $saveButton = $null
    if ($hwnd -ne [IntPtr]::Zero) {
        $dlg = $AE::FromHandle($hwnd)
        $byId = { param($id, $type) $dlg.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.AndCondition(
            (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)),
            (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $type))))) }
        # The dialog builds its controls with a delay: wait for them
        try {
            $nameBox = WaitFor { & $byId '1001' $CT::Edit } 'file name box' 10
            $saveButton = WaitFor { & $byId '1' $CT::Button } 'Save button' 10
        } catch { $nameBox = $null; $saveButton = $null }
    }
    if ($nameBox -and $saveButton) {
        SetText $nameBox $pdf
        Click $saveButton
    }
    else {
        if ($hwnd -ne [IntPtr]::Zero) { [SmokeNative]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 300 }
        [System.Windows.Forms.SendKeys]::SendWait(($pdf -replace '([+^%~(){}\[\]])', '{$1}') + '{ENTER}')
    }
    $done = WaitFor { FindWindowByTitle 'Export hotový|Chyba pri exporte' } 'export result' 30
    "export result: $($done.Current.Name)"
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    if (-not (Test-Path $pdf)) { throw "PDF not created: $pdf" }
    "PDF: $pdf"
    $script:printed = $true
}

# Plan history (#45): a folder of its own, never the coach's real history
$historyDir = Join-Path $OutDir 'history'
if (Test-Path $historyDir) { Remove-Item -Recurse -Force $historyDir }
$env:AKRO_TOOL_HISTORY_DIR = $historyDir

$script:proc = Start-Process $Exe -PassThru
"started pid $($proc.Id)"
try {
    # Startup dialogs
    $dlg = WaitFor { FindWindowByTitle 'Názov podujatia' } 'event name dialog'
    SetText (FindAllOfType $dlg $CT::Edit)[0] 'Test Pohár 2026'
    Click (FindButton $dlg 'OK')
    $dlg2 = WaitFor { FindWindowByTitle 'Tréner, kontakt a klub' } 'trainer dialog'
    $edits = FindAllOfType $dlg2 $CT::Edit
    SetText $edits[0] 'Ján Čierny'; SetText $edits[1] '0900 123 456'; SetText $edits[2] 'Klub Akro Test'
    Click (FindButton $dlg2 'OK')
    "startup dialogs OK"

    Start-Sleep -Milliseconds 800
    $script:main = (AppWindows)[0]
    SetText (FindAllOfType $main $CT::Edit)[0] 'Testovací Atlét'

    # Age group / category (no Okno filled yet, so no confirmation box)
    if ($AgeGroup) {
        $combo = (FindAllOfType $main $CT::ComboBox)[0]
        $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep -Milliseconds 400
        $item = WaitFor { FindAllOfType $combo $CT::ListItem | Where-Object { $_.Current.Name -eq $AgeGroup } | Select-Object -First 1 } "age group $AgeGroup"
        $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 800
    }
    if ($Category) {
        $radio = FindAllOfType $main $CT::RadioButton | Where-Object { $_.Current.Name -eq $Category } | Select-Object -First 1
        $radio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 500
    }
    "enabled categories: $((FindAllOfType $main $CT::RadioButton | Where-Object { $_.Current.IsEnabled } | ForEach-Object { $_.Current.Name }) -join ', ')"

    # Birth years: ComboBox 0 is the age group, then the cards Top, Middle (1st), Middle (2nd), Base
    $cardCombos = @(FindAllOfType $main $CT::ComboBox | Select-Object -Skip 1)
    $cardIndex = @{ Top = 0; Middle1 = 1; Middle2 = 2; Base = 3 }
    foreach ($card in $BirthYears.Keys) {
        $combo = $cardCombos[$cardIndex[$card]]
        $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep -Milliseconds 300
        # The year list is virtualized: find the item through ItemContainerPattern and realize it first
        $item = $combo.GetCurrentPattern([System.Windows.Automation.ItemContainerPattern]::Pattern).FindItemByProperty(
            $null, $AE::NameProperty, [string]$BirthYears[$card])
        try { $item.GetCurrentPattern([System.Windows.Automation.VirtualizedItemPattern]::Pattern).Realize() } catch { }
        $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 300
        "birth year $card = $($BirthYears[$card])"
    }

    # Pick counts from the counter ("WP: 0/6   Inv: 0/6" or "Inv: 0/8")
    $counter = MainTexts | Where-Object { $_ -match '\d+/\d+' } | Select-Object -First 1
    $totals = @([regex]::Matches($counter, '\d+/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value })
    $teamPicks = if ($totals.Count -ge 2) { $totals[0] } else { 0 }
    $invPicks = $totals[$totals.Count - 1]
    $slotCount = @(MainTexts | Where-Object { $_ -match '^Okno \d+$' }).Count
    "Okno count: $slotCount, counter: '$counter'"
    HoverShot (FindButtonByLabel $main '^Okno 1$') '0_hover_empty'

    # Fill every Okno: Zvlášť Okno always use the Inv table (#43); the other Okno use the team table until it is
    # full (the app then forces the Inv table). Team and Inv rows are used in turn, 1, 2, ...
    # In each row the tiles are used in turn (empty cells have no tile).
    $teamUsed = 0; $invUsed = 0
    for ($i = 1; $i -le $slotCount; $i++) {
        $isSeparate = $SeparateOkno -contains $i
        $isInv = $isSeparate -or $teamUsed -ge $teamPicks
        if ($isInv) { $invUsed++; $row = $invUsed } else { $teamUsed++; $row = $teamUsed }

        # The "Zvlášť" button is a sibling of the Okno tile; shown only while an Inv pick is left (#43)
        $sep = $Walk.GetNextSibling((FindButtonByLabel $main "^Okno $i$"))
        while ($sep -and $sep.Current.Name -ne 'Zvlášť') { $sep = $Walk.GetNextSibling($sep) }
        $expectSep = $teamPicks -gt 0 -and ($invUsed - [int]$isInv) -lt $invPicks
        if ([bool]$sep -ne $expectSep) { throw "Okno ${i}: Zvlášť button shown=$([bool]$sep), expected $expectSep" }

        if ($isSeparate) {
            if (-not $sep) { throw "Okno ${i}: no Zvlášť button" }
            Click $sep
            $prevSub = ''
            while ($true) {
                $grid = WaitFor { $g = FindWindowByTitle '^Cvi'; if ($g) { $s = SubtitleOf $g; if ($s -and $s -ne $prevSub) { $g } } } "Zvlášť picker for Okno $i"
                $sub = SubtitleOf $grid
                $null = $sub -match '(\d+)/(\d+)$'; $k = [int]$Matches[1]; $n = [int]$Matches[2]
                if ($grid.Current.Name -notmatch 'Inv') { throw "Okno ${i}: Zvlášť did not open the Inv table (#43)" }
                $toggle = FindAllOfType $grid $CT::Button | Where-Object { $_.Current.Name -match 'individu' } | Select-Object -First 1
                if ($toggle -and $toggle.Current.IsEnabled) { throw "Okno ${i}: table toggle is not locked in Zvlášť" }
                # Members Top -> Base (#46)
                if ($k -eq 1 -and $sub -notmatch '\(Top\)') { throw "Okno ${i}: first member is not Top ($sub)" }
                if ($k -eq $n -and $sub -notmatch '\(Base\)') { throw "Okno ${i}: last member is not Base ($sub)" }
                $all = @(FindAllOfType $grid $CT::Text | Where-Object { $_.Current.Name -match '^R\d+ - ' })
                $enabledRows = @($all | Where-Object { (TileButton $_).Current.IsEnabled } | ForEach-Object { ($_.Current.Name -split ' ')[0] } | Sort-Object -Unique)
                $labels = @($all | Where-Object { $_.Current.Name -match "^R$row - " })
                $pick = if ($k -eq $n) { $labels[1 % $labels.Count] } else { $labels[0] }
                "  Okno ${i} Zvlášť [$sub] selectable rows: $($enabledRows -join ',') -> $($pick.Current.Name)"
                if ($k -gt 1 -and ($enabledRows.Count -ne 1 -or $enabledRows[0] -ne "R$row")) { throw "Okno ${i}: member $k may choose from other rows ($($enabledRows -join ','))" }
                if ($i -eq $SeparateOkno[0] -and $k -eq 2) { Shot '1_picker_zvlast_member2' }
                Click (TileButton $pick)
                $prevSub = $sub
                if ($k -eq $n) { break }
            }
            WaitFor { -not (FindWindowByTitle '^Cvi') } 'picker closed' | Out-Null
            continue
        }

        Click (FindButtonByLabel $main "^Okno $i$")
        $grid = WaitFor { FindWindowByTitle '^Cvi' } "picker for Okno $i"
        if ($isInv -and $grid.Current.Name -notmatch 'Inv') { throw "Okno ${i}: Inv table was not forced (title '$($grid.Current.Name)')" }
        if ($i -eq 1) { Start-Sleep -Milliseconds 800; Shot '1_picker' }
        if ($i -eq $teamPicks + 1 -and $teamPicks -gt 0) { Start-Sleep -Milliseconds 800; Shot '1_picker_inv' }
        $labels = WaitFor { $t = @(FindAllOfType $grid $CT::Text | Where-Object { $_.Current.Name -match "^R$row - " }); if ($t.Count) { , $t } } "tiles of row $row"
        $tile = $labels[($i - 1) % $labels.Count]
        "  Okno ${i}: $($grid.Current.Name) -> $($tile.Current.Name)"
        while ($tile.Current.ControlType -ne $CT::Button) { $tile = $Walk.GetParent($tile) }
        Click $tile
        WaitFor { -not (FindWindowByTitle '^Cvi') } 'picker closed' | Out-Null
    }
    Start-Sleep -Milliseconds 500
    "summary:"; MainTexts | Where-Object { $_ -match 'Obť|/\d' } | ForEach-Object { "   $_" }
    "Okno labels:"; MainTexts | Where-Object { $_ -match ' - R\d - ' } | ForEach-Object { "   $_" }
    $overlays = @(MainTexts | Where-Object { $_ -match '\((Base|Middle|Top)\)$' })
    if ($overlays.Count) { "Zvlášť overlays: $($overlays -join ' | ')" }
    Shot '2_main_filled'
    # Hover a filled Okno (the Zvlášť one if any, to see the overlays on the tile)
    HoverShot (FindButtonByLabel $main $(if ($SeparateOkno.Count) { ' - Zvlášť$' } else { ' - R\d+ - ' })) '2_hover_filled'

    # Filled Okno -> "Jedno cvičenie / Zvlášť / Zrušiť": with all Inv picks used, Zvlášť is allowed only on an
    # Inv Okno, which frees its own pick (#43). Closed with "Zrušiť", nothing changes.
    if ($teamPicks -gt 0) {
        foreach ($check in @(@{ Label = ' - R\d+ - (WP|MP|MxP|WG|MG)$'; Expect = $false }, @{ Label = ' - R\d+ - Inv'; Expect = $true })) {
            $okno = FindButtonByLabel $main $check.Label
            if (-not $okno) { continue }
            Click $okno
            $change = WaitFor { FindWindowByTitle 'Zmeniť cvičenie' } 'Okno change dialog'
            $sepChoice = FindButton $change 'Zvlášť'
            "change dialog on '$($check.Label)': Zvlášť enabled = $($sepChoice.Current.IsEnabled)"
            if ($sepChoice.Current.IsEnabled -ne $check.Expect) { throw "Change dialog: Zvlášť enabled = $($sepChoice.Current.IsEnabled), expected $($check.Expect)" }
            if (-not $check.Expect) { Shot '2_change_dialog_no_zvlast' }
            Click (FindButton $change 'Zrušiť')
            WaitFor { -not (FindWindowByTitle 'Zmeniť cvičenie') } 'change dialog closed' | Out-Null
        }
    }

    if (-not $NoPrint) {
        $pdf = Join-Path $OutDir $PdfName
        PrintTo $pdf
        if (-not $script:printed) {
            if ((HistoryFiles).Count) { throw 'History: a plan was saved although nothing was printed' }
            "SMOKE TEST PASSED"
            return
        }
    }

    # === Plan history (#45): saved on print -> clear -> load it back from História -> print again -> same PDF ===
    if ($HistoryRoundTrip -and -not $NoPrint) {
        $files = HistoryFiles
        if ($files.Count -ne 1) { throw "History: expected 1 entry after printing, found $($files.Count)" }
        $realName = $files[0].Name
        "history entry: $realName"
        $saved = Get-Content $files[0].FullName -Raw -Encoding UTF8
        $before = @(MainTexts)
        $topName = (FindAllOfType $main $CT::Edit)[0]
        $topBefore = $topName.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value

        Click (FindButton $main 'Vyčistiť tabuľku')
        AnswerMessageBox 'Vyčistiť tabuľku' | Out-Null
        Start-Sleep -Milliseconds 500
        if (@(MainTexts | Where-Object { $_ -match ' - R\d+ - ' }).Count) { throw 'Okno not cleared' }
        SetText $topName 'Iné meno'

        # Fake entries: 10 days / 2 months + 10 days older (month separator between them), an unknown age group
        # (1 day older, shown greyed, removed below), a damaged file (skipped)
        $utf8 = New-Object System.Text.UTF8Encoding $false
        foreach ($fake in @(@{ Name = 'old-10d.json'; Days = 10; Months = 0 }, @{ Name = 'old-2m.json'; Days = 10; Months = 2 },
                            @{ Name = 'unknown.json'; Days = 1; Months = 0; Unknown = $true })) {
            # Local time first, so the offset is the one of that date (summer / winter time)
            $savedAt = ([DateTimeOffset][DateTime]::Now.AddDays(-$fake.Days).AddMonths(-$fake.Months)).ToString('yyyy-MM-ddTHH:mm:ss.fffffffzzz')
            $json = $saved -replace '"savedAt":\s*"[^"]*"', "`"savedAt`": `"$savedAt`""
            if ($fake.Unknown) { $json = $json -replace '"rulebook":\s*"[^"]*"', '"rulebook": "DO_99_ROK"' }
            [System.IO.File]::WriteAllText((Join-Path $historyDir $fake.Name), $json, $utf8)
        }
        [System.IO.File]::WriteAllText((Join-Path $historyDir 'broken.json'), '{ "formatVersion": 1, "slots": [', $utf8)

        $win = OpenHistory
        Start-Sleep -Milliseconds 800
        $items = HistoryItems $win
        "history list:"; $items | ForEach-Object { "   " + (ItemText $_) }
        if ($items.Count -ne 4) { throw "History: expected 4 items (damaged file skipped), found $($items.Count)" }
        if ((ItemText $items[1]) -notmatch 'nedá sa načítať') { throw 'History: entry with an unknown age group not marked' }
        Shot '4_history'
        $load = FindButton $win 'Načítať'; $remove = FindButton $win 'Odstrániť'
        if ($load.Current.IsEnabled -or $remove.Current.IsEnabled) { throw 'History: buttons enabled without a selection' }

        SelectItem $items[1]
        if ($load.Current.IsEnabled -or -not $remove.Current.IsEnabled) { throw 'History: unknown entry must only be removable' }
        Click $remove
        AnswerMessageBox 'Odstrániť plán' | Out-Null
        Start-Sleep -Milliseconds 500
        $items = HistoryItems $win
        if ($items.Count -ne 3 -or (Test-Path (Join-Path $historyDir 'unknown.json'))) { throw 'History: removing an entry failed' }
        "removed the unknown entry"

        SelectItem $items[0]
        if (-not $load.Current.IsEnabled) { throw 'History: Načítať disabled on a valid entry' }
        Click $load
        # An athlete name is filled -> "Načítať plán? Aktuálne údaje sa nahradia."
        "load question: $(AnswerMessageBox 'Načítať plán')"
        WaitFor { -not (FindWindowByTitle '^História') } 'history window closed' | Out-Null
        Start-Sleep -Milliseconds 800
        if (FindWindowByTitle 'Načítanie plánu') { Shot 'failure_load_problems'; throw 'History: some Okno could not be loaded' }
        $after = @(MainTexts)
        $topAfter = $topName.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
        if (($before -join '|') -ne ($after -join '|') -or $topAfter -ne $topBefore) {
            "before: $($before -join ' | ')"; "after:  $($after -join ' | ')"
            throw 'History: the loaded plan differs from the printed one'
        }
        "loaded plan matches the printed one"
        Shot '5_main_loaded'

        $pdf2 = Join-Path $OutDir ([System.IO.Path]::GetFileNameWithoutExtension($PdfName) + '_reloaded.pdf')
        PrintTo $pdf2
        # Same plan printed again -> the newest entry only gets a new date (Q9)
        $files = HistoryFiles
        if ($files.Count -ne 3 -or -not ($files | Where-Object { $_.Name -eq $realName })) { throw "History: expected 3 entries after re-printing, found $($files.Name -join ', ')" }
        if (-not (SamePdf $pdf $pdf2)) { throw 'History: the re-printed PDF differs from the first one' }
        "re-printed PDF is identical (rendered pixels)"

        $win = OpenHistory
        Start-Sleep -Milliseconds 800
        "history list:"; HistoryItems $win | ForEach-Object { "   " + (ItemText $_) }
        Shot '6_history_month_separator'
        Click (FindButton $win 'Zavrieť')
        WaitFor { -not (FindWindowByTitle '^História') } 'history window closed' | Out-Null
    }
    "SMOKE TEST PASSED"
}
catch { "FAILED: $_"; Shot 'failure' }
finally { Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue }
