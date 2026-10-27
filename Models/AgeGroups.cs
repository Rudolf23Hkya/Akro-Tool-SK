using System;
using System.Collections.Generic;
using System.Linq;

namespace Atletika_SutaznyPlan_Generator.Models
{
    // === Age groups and their exercise tables ===
    // Reference data: docs/specification/01-age-group-tables.md
    // Rows and columns are 1-based everywhere (X = column, Y = row).

    /// <summary>One exercise picture grid from the rulebook.</summary>
    public sealed class ExerciseTable
    {
        private readonly decimal[] _columnValues;
        private readonly HashSet<(int Row, int Col)> _emptyCells;

        public ExerciseTable(string id, string displayName, int rows, IEnumerable<decimal> columnValues,
                             IEnumerable<(int Row, int Col)>? emptyCells = null)
        {
            Id = id;
            DisplayName = displayName;
            Rows = rows;
            _columnValues = columnValues.ToArray();
            _emptyCells = emptyCells?.ToHashSet() ?? new HashSet<(int Row, int Col)>();
        }

        // DB folder name inside the age-group folder, e.g. "wp", "inv"
        public string Id { get; }

        // Shown in the picker title, e.g. "WP", "Pár"
        public string DisplayName { get; }

        public int Rows { get; }
        public int Cols => _columnValues.Length;

        /// <summary>Difficulty of every exercise in the given column (1-based).</summary>
        public decimal GetDifficulty(int col) => _columnValues[col - 1];

        /// <summary>True for positions that have no exercise in the rulebook.</summary>
        public bool IsEmpty(int row, int col) => _emptyCells.Contains((row, col));

        public bool HasExercise(int row, int col)
            => row >= 1 && row <= Rows && col >= 1 && col <= Cols && !IsEmpty(row, col);
    }

    public sealed class AgeGroupDefinition
    {
        public Rulebook Rulebook { get; init; }
        public string Label { get; init; } = "";
        public string DbFolder { get; init; } = "";
        public string FilePrefix { get; init; } = "";

        // Which table each team category uses (several categories may share one table)
        public IReadOnlyDictionary<Category, ExerciseTable> TeamTables { get; init; } = new Dictionary<Category, ExerciseTable>();

        // null = the age group has no Inv table
        public ExerciseTable? InvTable { get; init; }

        public int TeamPicks { get; init; }
        public int InvPicks { get; init; }

        // Print-time age warning (issue #28): highest allowed age in the competition year; null = no check
        public int? MaxAge { get; init; }

        public int SlotCount => TeamPicks + InvPicks;

        public ExerciseTable GetTable(Category category)
        {
            if (category == Category.Inv)
                return InvTable ?? throw new InvalidOperationException($"{Label} has no Inv table.");

            return TeamTables.TryGetValue(category, out var table)
                ? table
                : throw new InvalidOperationException($"{Label} has no table for {category}.");
        }

        public IEnumerable<ExerciseTable> AllTables
            => TeamTables.Values.Concat(InvTable is null ? Array.Empty<ExerciseTable>() : new[] { InvTable }).Distinct();
    }

    public static class AgeGroups
    {
        // Do 10 / Do 14: difficulty = column × 0.01 (confirmed by the owner)
        private static readonly decimal[] ColumnTimesHundredth = { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m };

        public static IReadOnlyList<AgeGroupDefinition> All { get; } = new[]
        {
            SixBySix(Rulebook.DO_10_ROK, "Do 10 rokov", "do10_db", "10r", maxAge: 9),
            // TODO (Phase 5): age limit for Do 14 rokov
            SixBySix(Rulebook.DO_14_ROK, "Do 14 rokov", "do14_db", "14r", maxAge: null),
        };

        public static AgeGroupDefinition Get(Rulebook rulebook) => All.First(g => g.Rulebook == rulebook);

        // One 6×5 table per category + a 6×5 Inv table, 6 + 6 picks
        private static AgeGroupDefinition SixBySix(Rulebook rulebook, string label, string dbFolder, string filePrefix, int? maxAge)
        {
            static ExerciseTable Table(string id, string name) => new(id, name, rows: 6, ColumnTimesHundredth);

            return new AgeGroupDefinition
            {
                Rulebook = rulebook,
                Label = label,
                DbFolder = dbFolder,
                FilePrefix = filePrefix,
                TeamTables = new Dictionary<Category, ExerciseTable>
                {
                    [Category.WP] = Table("wp", "WP"),
                    [Category.MP] = Table("mp", "MP"),
                    [Category.MxP] = Table("m_x_p", "MxP"),
                    [Category.WG] = Table("wg", "WG"),
                    [Category.MG] = Table("mg", "MG"),
                },
                InvTable = Table("inv", "Inv"),
                TeamPicks = 6,
                InvPicks = 6,
                MaxAge = maxAge
            };
        }
    }
}
