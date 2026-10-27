# Phase 5 — Age check for every age group (#28, remaining part)

**Goal:** the print-time age warning (implemented for Do 10 rokov in Phase 0) covers **every** age group.
By the owner's request this is the **last** step, after the new groups exist (CLAUDE.md §6).

Depends on: Phase 2.

## Parts

### 5.1 Limits from the definitions
- `GetMaxAgeInYear` is replaced by `MinAge` / `MaxAge` from `AgeGroupDefinition` (Phase 1.1), values from
  [01-age-group-tables.md §4](01-age-group-tables.md#4-age-warning-limits-phase-5):
  Do 8 max 7 · Do 10 max 9 · Do 14 max 13 · Od 8 – 14 min 8, max 13 · Individuálna zostava none.
- Age = competition year − birth year (competition year = current year, unchanged).

### 5.2 Too young (new, Od 8 – 14 only)
- Athletes below `MinAge` are listed too. Message lines distinguish both cases, e.g.
  - `V roku {year} dovŕšia {MaxAge + 1} alebo viac rokov.`
  - `V roku {year} ešte nedovŕšia {MinAge} rokov.`
- Same dialog: **OK** = don't print, **Tlačiť aj tak** = print.

### 5.3 Which athletes are checked
- Only athletes with a birth year (empty year → not checked, as today).
- Proposal: check only the cards that belong to the team size (pair: Top + Base, trio: + 1st Middle,
  quartet: all four; Individuálna zostava: none) so a forgotten year in an unused card cannot trigger a warning.
  *(Today all four cards are checked.)* — confirm in review.

## Acceptance
- For each group: one athlete just inside and one just outside each limit → warning only for the outside one.
- Individuálna zostava → never a warning.
- OK / Tlačiť aj tak behave as in Phase 0.
