using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.IO;

namespace Atletika_SutaznyPlan_Generator.Models
{
    public sealed class ExerciseImageRepository
    {
        private readonly string _dbRoot;

        // Key = (rulebook, table id, row, col) -> absolute file path
        private readonly ConcurrentDictionary<(Rulebook, string tableId, int row, int col), string> _index = new();

        // Groups: prefix(10r), cat(inv), column (01), row (06)
        private static readonly Regex FileRx = new(
            @"^(?<prefix>[a-z0-9]+)_(?<cat>[a-z_]+)_(?<a>\d{2})_(?<b>\d{2})\.png$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ExerciseImageRepository(string dbRoot)
        {
            _dbRoot = dbRoot;
            BuildIndex();
        }

        private void BuildIndex()
        {
            foreach (var group in AgeGroups.All)
            {
                foreach (var table in group.AllTables)
                {
                    // Only files directly inside the table folder (input_4_crop etc. are ignored)
                    var tableDir = Path.Combine(_dbRoot, group.DbFolder, table.Id);
                    if (!Directory.Exists(tableDir)) continue;

                    foreach (var file in Directory.EnumerateFiles(tableDir, "*.png"))
                    {
                        var name = Path.GetFileName(file);
                        var m = FileRx.Match(name);
                        if (!m.Success) continue;

                        // Enforce the age group's file prefix
                        var prefix = m.Groups["prefix"].Value;
                        if (!prefix.Equals(group.FilePrefix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // a/b from filename (01..)
                        var a = int.Parse(m.Groups["a"].Value);
                        var b = int.Parse(m.Groups["b"].Value);

                        // IMPORTANT:
                        // Example filename: 10r_inv_01_02 - rulebook_category_colNum_rowNum
                        if (!table.HasExercise(row: b, col: a)) continue;

                        _index[(group.Rulebook, table.Id, row: b, col: a)] = Path.GetFullPath(file);
                    }
                }
            }
        }

        public string? GetImagePath(Rulebook rb, Category category, int row, int col)
        {
            var table = AgeGroups.Get(rb).GetTable(category);
            return _index.TryGetValue((rb, table.Id, row, col), out var path) ? path : null;
        }

        // UI-friendly overload: x = column, y = row (matches your filename scheme _<col>_<row>.png)
        public string? GetImagePathXY(Rulebook rb, Category category, int x, int y)
            => GetImagePath(rb, category, row: y, col: x);

        public IReadOnlyList<TableCell> GetTable(Rulebook rb, Category category)
        {
            var table = AgeGroups.Get(rb).GetTable(category);
            var list = new List<TableCell>(table.Rows * table.Cols);
            for (int r = 1; r <= table.Rows; r++)
                for (int c = 1; c <= table.Cols; c++)
                {
                    var isEmpty = table.IsEmpty(r, c);
                    list.Add(new TableCell(rb, category, r, c,
                        isEmpty ? null : GetImagePath(rb, category, r, c),
                        isEmpty,
                        table.GetDifficulty(c)));
                }
            return list;
        }
    }

    public sealed record TableCell(Rulebook Rulebook, Category Category, int Row, int Col, string? ImagePath,
                                   bool IsEmpty, decimal Difficulty);
}
