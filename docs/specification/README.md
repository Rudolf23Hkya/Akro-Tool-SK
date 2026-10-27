# Akro Tool SK — Upgrade specification

Status: **draft for owner review** · created 2026-10-27
Source: GitHub issues #26–#30 in `Rudolf23Hkya/Akro-Tool-SK` + the owner's answers from 2026-10-27
(recorded in [00-decisions-and-open-questions.md](00-decisions-and-open-questions.md)).

Technical background of the current app: [`CLAUDE.md`](../../CLAUDE.md) in the repo root.

## 1. Issues covered

| Issue | Title | Current state | Phase |
|---|---|---|---|
| #26 | Slovak discipline names | **Implemented** on branch `feature/issues-26-28` (not pushed/merged yet) | 0 |
| #27 | Add new age groups (Do 8, Od 8 – 14, Individuálna zostava) | Not started | 1 + 2 |
| #28 | Birth year only + age warning | **Implemented for Do 10 rokov** on `feature/issues-26-28`; other groups pending | 0 + 5 |
| #29 | One Okno, one exercise per team member ("Zvlášť") | Not started | 3 |
| #30 | Printing several exercises per Okno | Not started; needs template edit by the owner | 4 |

## 2. Roadmap

```
Phase 0  Ship #26 + #28 (Do 10)          ──┐
                                            ▼
Phase 1  Data-driven foundation (refactor, no visible change)
            │                         │
            ▼                         ▼
Phase 2  New age groups (#27)     Phase 3  Zvlášť exercises (#29)  ◄──►  Phase 4  Print Zvlášť (#30)
            │                                                            ▲
            ▼                                          owner: template edit in Acrobat
Phase 5  Age check for every group (#28 TODO) — always the LAST step       (can start any time)
```

- Phase 1 is the base for everything else: today the app assumes **12 Okno, 6 + 6, 6×5 tables and
  difficulty = column × 0.01** in many places. The new age groups break all four assumptions.
- Phases 2 and 3 are independent of each other once Phase 1 is merged.
- Phase 3 and Phase 4 should be released **together** — a Zvlášť Okno that cannot be printed is useless.
- Phase 5 is last by the owner's request (see `CLAUDE.md` §6).

| Phase | Document | Size | Status |
|---|---|---|---|
| 0 | [phase-0-ship-26-28.md](phase-0-ship-26-28.md) | S | Merged 2026-10-27 (6c9fb70); #26 closed; smoke test script added with Phase 1 |
| 1 | [phase-1-data-driven-foundation.md](phase-1-data-driven-foundation.md) | L | Merged (PR #31) |
| 2 | [phase-2-new-age-groups.md](phase-2-new-age-groups.md) | M | Merged (PR #32); real pictures (O3) pending, mocks until then |
| 3 | [phase-3-sep-exercises.md](phase-3-sep-exercises.md) | L | PR open (`feature/phase-3-zvlast`); prints only the first picture until Phase 4 |
| 4 | [phase-4-print-sep-exercises.md](phase-4-print-sep-exercises.md) | M | |
| 5 | [phase-5-age-check-all-groups.md](phase-5-age-check-all-groups.md) | S | |

Reference data (table sizes, column values, empty cells, age limits) lives in **one** place:
[01-age-group-tables.md](01-age-group-tables.md).

## 3. Owner tasks (outside the code)

| # | Task | Needed by |
|---|---|---|
| O1 | Test the `feature/issues-26-28` build on Windows and approve the PR | Phase 0 |
| O2 | Upload the rulebook PDFs to `docs/` (planned by the owner) | Phase 2 (reference) |
| O3 | Prepare the exercise pictures for Do 8, Od 8 – 14 and Individuálna zostava | End of Phase 2 (mocks until then) |
| O4 | Edit `Cvicebny-plan_Template.pdf` in Acrobat: add 4 more boxes (→ 16) and turn the box numbers into text fields `BoxNumber.0–15` (see Phase 4, part 4.1) | Phase 4 |
| O5 | Confirm or correct the assumptions A1–A13 in [00-decisions-and-open-questions.md](00-decisions-and-open-questions.md) | before the phase that needs them |

## 4. Ground rules (from CLAUDE.md, apply to every phase)

- **Stability on Windows first.** Small, conservative changes. No new frameworks or NuGet packages without asking.
- UI strings in **Slovak**; code, comments and docs in English.
- Keep `FormData` (what is printed) and `ExerciseSlots` (what is shown) in sync.
- Keep **X = column, Y = row** everywhere.
- Do not rename PDF field constants or DB file/folder names unless the template/DB changes too.
- `dotnet build` must stay at **0 warnings**.

## 5. How each phase is delivered

1. Branch from `master`: `feature/phase-N-<short-name>`.
2. Implement part by part (each part is a reviewable commit).
3. Checks before the PR:
   - `dotnet build "Akro Tool SK.csproj"` → 0 warnings, 0 errors.
   - UI smoke test on the Windows host (UI Automation script + screenshots), see Phase 0, part 0.3.
   - For PDF changes: export a PDF, render it to PNG and inspect it.
4. PR into `master` with a short test report; the owner tests on Windows and merges.
5. Close or update the GitHub issue.

## 6. Glossary

| Term | Meaning |
|---|---|
| **Okno** | One of the slots on the main screen (today 12) = one numbered box on the printed sheet. |
| **Age group** | Code name `Rulebook` (enum). Shown in the *Kategória* ComboBox: Do 10 rokov, Do 14 rokov, … |
| **Category** | Team type radio: WP, MP, MxP, WG, MG. |
| **Table** | One exercise picture grid (rows × columns) from the rulebook, e.g. *Pár*, *Trojica*, *Inv*. |
| **Team table / Inv table** | The category-specific table and the individual-elements table of an age group. |
| **Row rule** | Max. one Okno per table row (issue #15). |
| **Empty cell** | A table position that has no exercise in the rulebook (new in #27). Not selectable. |
| **Zvlášť** | The "one exercise per team member" mode and its button (#29, D15). |
| **Box** | One picture box on the printed sheet (12 today, 16 after the template edit). Filled in routine order with numbers `1`, `2.a`, `2.b`, …; overflow goes to an extra page (#30). |
