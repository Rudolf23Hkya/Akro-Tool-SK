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
    [ValidateSet('PrintAnyway', 'Ok')][string]$AgeWarning = 'PrintAnyway'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
if (-not ('SmokeWin32' -as [type])) {
    Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class SmokeWin32 {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
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
        if (Test-Path $pdf) { Remove-Item $pdf }
        Click (FindButton $main 'Tlačiť')
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
                "SMOKE TEST PASSED"
                return
            }
            Click (FindButton $save 'Tlačiť aj tak')
            $save = WaitFor { FindWindowByTitle 'Uložiť vyplnený PDF plán' } 'save dialog'
        }
        else { "no age warning" }
        # The common file dialog is not fully visible under the WPF window in UI Automation, but it is when
        # opened by its window handle: 1001 = file name box, 1 = Save. Fallback: type the path + Enter.
        Start-Sleep -Milliseconds 1500
        $hwnd = [SmokeWin32]::FindWindow([NullString]::Value, 'Uložiť vyplnený PDF plán')
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
            if ($hwnd -ne [IntPtr]::Zero) { [SmokeWin32]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 300 }
            [System.Windows.Forms.SendKeys]::SendWait(($pdf -replace '([+^%~(){}\[\]])', '{$1}') + '{ENTER}')
        }
        $done = WaitFor { FindWindowByTitle 'Export hotový|Chyba pri exporte' } 'export result' 30
        "export result: $($done.Current.Name)"
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        if (-not (Test-Path $pdf)) { throw "PDF not created: $pdf" }
        "PDF: $pdf"
    }
    "SMOKE TEST PASSED"
}
catch { "FAILED: $_"; Shot 'failure' }
finally { Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue }
