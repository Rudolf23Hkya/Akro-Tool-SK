# Phase 0 — Ship what is already done (#26, #28 for Do 10)

**Goal:** get the finished work on `feature/issues-26-28` into `master` before the bigger refactor starts, so
Phase 1 branches from a clean, released base.

## Parts

### 0.1 Push and open the PR
- Push `feature/issues-26-28` (5 commits: CLAUDE.md, App namespace fix, #26, #28, age-check note).
- PR into `master` with a short test report.

### 0.2 Owner acceptance on Windows
Checklist:
- [ ] Disciplína radios show *Zmiešaná zostava / Dynamická zostava / Balančná zostava*; the same text is on the PDF.
- [ ] Each athlete card has a **year** ComboBox (empty item first); only the year is printed under *Rok narodenia*.
- [ ] *Do 10 rokov* + an athlete born ≥ 10 years before the current year → printing shows the warning dialog:
      **OK** = nothing is printed, **Tlačiť aj tak** = save dialog opens and the PDF is created.
- [ ] *Do 14 rokov* → no warning (other groups come in Phase 5).
- [ ] `wpf-binding-errors.log` is created next to the exe (App namespace fix).

After the merge: close **#26**. Keep **#28** open (or close it and track the remaining part here) until Phase 5.

### 0.3 UI smoke-test script (proposal)
Add `tools/ui-smoke-test.ps1`: the Windows UI Automation script already used in the remote session (fills the
startup dialogs, enters an athlete, opens Okno 1, picks a tile, checks counters and takes screenshots).
Every later phase extends it, so each PR can be checked the same way on the Windows host.

- PowerShell 5.1, no extra modules, no `-ExecutionPolicy Bypass` needed when run from the PowerShell tool / console.
- Not shipped with the app (not in the csproj).

## Done when
- PR merged into `master`, #26 closed, smoke test script in the repo (if accepted).
