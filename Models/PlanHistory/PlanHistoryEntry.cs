using System;
using System.Collections.Generic;

namespace Atletika_SutaznyPlan_Generator.Models.PlanHistory
{
    // === One printed Súťažný plán (issue #45), stored as one JSON file ===
    // Enums are stored by name, so new enum values can't shift them. Pictures and difficulties are not stored:
    // on load the picture is looked up again by (age group, table, row, column) and the value comes from the table.
    // Everything read from a file may be missing or null — the loader checks every value.
    public sealed record PlanHistoryEntry
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; init; } = CurrentFormatVersion;
        public DateTimeOffset SavedAt { get; init; }

        public string? EventName { get; init; }
        public string? TrainerName { get; init; }
        public string? TrainerContact { get; init; }
        public string? ClubName { get; init; }

        public string? Rulebook { get; init; }  // Rulebook enum name, e.g. "DO_10_ROK"
        public string? Category { get; init; }  // Category enum name, e.g. "MxP"
        public string? Routine { get; init; }   // RoutineType enum name, e.g. "Combi"

        public PlanHistoryAthletes? Athletes { get; init; }
        public List<PlanHistorySlot>? Slots { get; init; }
    }

    public sealed record PlanHistoryAthletes
    {
        public PlanHistoryAthlete? Top { get; init; }
        public PlanHistoryAthlete? Middle1 { get; init; }
        public PlanHistoryAthlete? Middle2 { get; init; }
        public PlanHistoryAthlete? Base { get; init; }
    }

    public sealed record PlanHistoryAthlete
    {
        public string? Name { get; init; }
        public string? BirthYear { get; init; }
    }

    // One Okno: the table (category name or "Inv") and row, one exercise or one per team member (Zvlášť)
    public sealed record PlanHistorySlot
    {
        public int Okno { get; init; }          // 1-based
        public string? Table { get; init; }
        public int Row { get; init; }           // 1-based
        public List<PlanHistoryExercise>? Exercises { get; init; }
    }

    public sealed record PlanHistoryExercise
    {
        public int Col { get; init; }           // 1-based
        public int? Member { get; init; }       // Zvlášť: team member, 0 = Top ... (issue #46); null = whole team
        public string? Position { get; init; }  // Zvlášť: "Top", "Middle1", ... (for reading the file only)
    }

    // An entry together with the file it was read from (needed to remove it)
    public sealed record StoredPlan(string FilePath, PlanHistoryEntry Entry);
}
