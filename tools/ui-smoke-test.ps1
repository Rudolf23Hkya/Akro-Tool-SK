<#
  UI smoke test for Akro Tool SK (Windows only, PowerShell 5.1, no extra modules).
  Drives the real app through Windows UI Automation:
    startup dialogs -> athlete name -> fills every Okno (team rows first, then Inv) -> Tlačiť -> saves the PDF.
  Screenshots and the PDF go to -OutDir. The host must be logged in and unlocked.

  Usage:
    .\tools\ui-smoke-test.ps1 -OutDir C:\temp\smoke [-Exe <path>] [-PdfName out.pdf] [-NoPrint]
                              [-AgeGroup DO_8_ROK] [-Category MG]
  -AgeGroup is the Rulebook enum name (DO_8_ROK, DO_10_ROK, DO_14_ROK, OD_8_14_ROK, INDIVIDUAL),
  -Category the radio button text (WP, MP, MxP, WG, MG).
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\Akro Tool SK.exe'),
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$PdfName = 'smoke.pdf',
    [switch]$NoPrint,
    [string]$AgeGroup,
    [string]$Category
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
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
function Click($el)           { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function SetText($el, $value) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value) }
function MainTexts { FindAllOfType $script:main $CT::Text | ForEach-Object { $_.Current.Name } }

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

    # Pick counts from the counter ("WP: 0/6   Inv: 0/6" or "Inv: 0/8")
    $counter = MainTexts | Where-Object { $_ -match '\d+/\d+' } | Select-Object -First 1
    $totals = @([regex]::Matches($counter, '\d+/(\d+)') | ForEach-Object { [int]$_.Groups[1].Value })
    $teamPicks = if ($totals.Count -ge 2) { $totals[0] } else { 0 }
    $slotCount = @(MainTexts | Where-Object { $_ -match '^Okno \d+$' }).Count
    "Okno count: $slotCount, counter: '$counter'"

    # Fill every Okno: the app forces the Inv table once the team table is full,
    # so Okno 1..T get team rows 1..T and the rest get Inv rows 1..I.
    # In each row the tiles are used in turn (empty cells have no tile).
    for ($i = 1; $i -le $slotCount; $i++) {
        $isInv = $i -gt $teamPicks
        $row = if ($isInv) { $i - $teamPicks } else { $i }
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
    Shot '2_main_filled'

    if (-not $NoPrint) {
        $pdf = Join-Path $OutDir $PdfName
        if (Test-Path $pdf) { Remove-Item $pdf }
        Click (FindButton $main 'Tlačiť')
        $save = WaitFor { FindWindowByTitle 'Uložiť vyplnený PDF plán' } 'save dialog'
        # The common file dialog's name box is not reachable through UI Automation from here,
        # but it has keyboard focus when the dialog opens: type the full path and press Enter.
        Start-Sleep -Milliseconds 1500
        [System.Windows.Forms.SendKeys]::SendWait(($pdf -replace '([+^%~(){}\[\]])', '{$1}') + '{ENTER}')
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
