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

        // Print-time age warning (issue #28): allowed age in the competition year (competition year − birth year);
        // null = no limit on that side
        public int? MinAge { get; init; }
        public int? MaxAge { get; init; }

        // Until the owner's pictures exist, every exercise without a picture shows this one
        // (relative to the DataBase folder); null = show the SGF placeholder instead.
        public string? MockImageRelativePath { get; init; }

        // Pictures described by a CSV in DbFolder (owner's export for #27: id, row, column, value/difficulty, file).
        // null = classic layout: <DbFolder>/<table id>/<prefix>_<table>_<COL>_<ROW>.png
        public string? IndexFile { get; init; }

        // index.csv: id prefix ("DUO" in "DUO-I-1") -> table; empty = every row belongs to the Inv table
        public IReadOnlyDictionary<string, ExerciseTable> IndexTablesByIdPrefix { get; init; } = new Dictionary<string, ExerciseTable>();

        public int SlotCount => TeamPicks + InvPicks;

        // Individuálna zostava: one competitor, only the Inv table, no category choice
        public bool HasTeamTables => TeamTables.Count > 0;

        public bool IsCategoryAllowed(Category category) => TeamTables.ContainsKey(category);

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

        // Inv table of Do 8 and Od 8 – 14: columns 1..7 = 0.01..0.07, column 8 = 0.07 too
        private static readonly decimal[] InvColumns3x8 = { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m, 0.06m, 0.07m, 0.07m };

        // List order = order in the age-group ComboBox
        public static IReadOnlyList<AgeGroupDefinition> All { get; } = new[]
        {
            Do8(),
            // Turning 10 (14) during the year already means the higher age group
            SixBySix(Rulebook.DO_10_ROK, "Do 10 rokov", "do10_db", "10r", maxAge: 9),
            SixBySix(Rulebook.DO_14_ROK, "Do 14 rokov", "do14_db", "14r", maxAge: 13),
            Od8To14(),
            Individual(),
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

        // Do 8 rokov: pairs only (WP / MP / MxP share the Duo table), 5 + 3 picks
        private static AgeGroupDefinition Do8()
        {
            var duo = new ExerciseTable("duo", "Duo", rows: 5,
                new[] { 0.01m, 0.01m, 0.01m, 0.02m, 0.02m, 0.03m, 0.03m },
                emptyCells: new[] { (5, 7) });
            var inv = new ExerciseTable("inv", "Inv", rows: 3, InvColumns3x8,
                emptyCells: new[] { (1, 7), (1, 8), (2, 7), (2, 8) });

            return new AgeGroupDefinition
            {
                Rulebook = Rulebook.DO_8_ROK,
                Label = "Do 8 rokov",
                DbFolder = "new_do8",
                IndexFile = "index.csv",
                IndexTablesByIdPrefix = new Dictionary<string, ExerciseTable> { ["DUO"] = duo, ["IND"] = inv },
                TeamTables = new Dictionary<Category, ExerciseTable>
                {
                    [Category.WP] = duo,
                    [Category.MP] = duo,
                    [Category.MxP] = duo,
                },
                InvTable = inv,
                TeamPicks = 5,
                InvPicks = 3,
                MaxAge = 7
            };
        }

        // Od 8 – 14 rokov: Pár (WP / MP / MxP), Trojica (WG), Štvorica (MG), 5 + 3 picks
        private static AgeGroupDefinition Od8To14()
        {
            var par = new ExerciseTable("par", "Pár", rows: 5,
                new[] { 0.01m, 0.01m, 0.01m, 0.02m, 0.02m, 0.03m, 0.03m, 0.03m },
                emptyCells: new[] { (4, 6), (5, 6) });
            var groupColumns = new[] { 0.01m, 0.01m, 0.02m, 0.02m, 0.03m, 0.03m };
            var trojica = new ExerciseTable("trojica", "Trojica", rows: 5, groupColumns);
            var stvorica = new ExerciseTable("stvorica", "Štvorica", rows: 5, groupColumns);
            var inv = new ExerciseTable("inv", "Inv", rows: 3, InvColumns3x8,
                emptyCells: new[] { (1, 8), (2, 8) });

            return new AgeGroupDefinition
            {
                Rulebook = Rulebook.OD_8_14_ROK,
                Label = "Od 8 – 14 rokov",
                // "zat" = začiatočníci (beginners)
                DbFolder = "new_zat_do14",
                IndexFile = "index.csv",
                IndexTablesByIdPrefix = new Dictionary<string, ExerciseTable>
                {
                    ["PAR"] = par, ["TRO"] = trojica, ["STV"] = stvorica, ["IND"] = inv
                },
                TeamTables = new Dictionary<Category, ExerciseTable>
                {
                    [Category.WP] = par,
                    [Category.MP] = par,
                    [Category.MxP] = par,
                    [Category.WG] = trojica,
                    [Category.MG] = stvorica,
                },
                InvTable = inv,
                TeamPicks = 5,
                InvPicks = 3,
                MinAge = 8,
                MaxAge = 13
            };
        }

        // Individuálna zostava: one competitor, one 8×6 table (handled as the Inv table), 8 picks, no age check
        private static AgeGroupDefinition Individual()
        {
            return new AgeGroupDefinition
            {
                Rulebook = Rulebook.INDIVIDUAL,
                Label = "Individuálna zostava",
                // index.csv rows A..H = table rows 1..8, ids without a table prefix
                DbFolder = "new_inv",
                IndexFile = "index.csv",
                InvTable = new ExerciseTable("individual", "Inv", rows: 8,
                    new[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m, 0.06m }),
                TeamPicks = 0,
                InvPicks = 8
            };
        }
    }
}
