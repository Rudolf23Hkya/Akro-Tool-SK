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

        // Key = (rulebook, category, row, col) -> absolute file path
        private readonly ConcurrentDictionary<(Rulebook, Category, int row, int col), string> _index = new();

        // Groups: prefix(10r), cat(inv), column (01), row (06)
        private static readonly Regex FileRx = new(
            @"^(?<prefix>\d+r)_(?<cat>[a-z_]+)_(?<a>\d{2})_(?<b>\d{2})\.png$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ExerciseImageRepository(string dbRoot)
        {
            _dbRoot = dbRoot;
            BuildIndex();
        }

        private static string RulebookFolder(Rulebook rb) =>
            rb switch
            {
                Rulebook.DO_10_ROK => "do10_db",
                Rulebook.DO_14_ROK => "do14_db",
                _ => throw new ArgumentOutOfRangeException(nameof(rb))
            };

        private static string RulebookPrefix(Rulebook rb) =>
            rb switch
            {
                Rulebook.DO_10_ROK => "10r",
                Rulebook.DO_14_ROK => "14r",
                _ => throw new ArgumentOutOfRangeException(nameof(rb))
            };

        private static bool TryParseCategoryFolder(string folderName, out Category cat)
        {
            folderName = folderName.ToLowerInvariant();
            foreach (var kv in CategoryMap.FolderName)
            {
                if (kv.Value.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    cat = kv.Key;
                    return true;
                }
            }
            cat = default;
            return false;
        }

        private void BuildIndex()
        {
            foreach (Rulebook rb in Enum.GetValues(typeof(Rulebook)))
            {
                var rbDir = Path.Combine(_dbRoot, RulebookFolder(rb));
                if (!Directory.Exists(rbDir)) continue;

                foreach (var catDir in Directory.EnumerateDirectories(rbDir))
                {
                    var folder = Path.GetFileName(catDir);
                    if (!TryParseCategoryFolder(folder, out var category))
                        continue;

                    foreach (var file in Directory.EnumerateFiles(catDir, "*.png"))
                    {
                        var name = Path.GetFileName(file);
                        var m = FileRx.Match(name);
                        if (!m.Success) continue;

                        // Optional: enforce correct rulebook prefix match
                        var prefix = m.Groups["prefix"].Value.ToLowerInvariant();
                        if (!prefix.Equals(RulebookPrefix(rb), StringComparison.OrdinalIgnoreCase))
                            continue;

                        // a/b from filename (01..)
                        var a = int.Parse(m.Groups["a"].Value);
                        var b = int.Parse(m.Groups["b"].Value);

                        // IMPORTANT:
                        // Example filename: 10r_inv_01_02 - rulebook_category_colNum_rowNum
                        _index[(rb, category, row: b, col: a)] = Path.GetFullPath(file);
                    }
                }
            }
        }

        public string? GetImagePath(Rulebook rb, Category category, int row, int col)
            => _index.TryGetValue((rb, category, row, col), out var path) ? path : null;

        // UI-friendly overload: x = column, y = row (matches your filename scheme _<col>_<row>.png)
        public string? GetImagePathXY(Rulebook rb, Category category, int x, int y)
            => GetImagePath(rb, category, row: y, col: x);

        public IReadOnlyList<TableCell> GetTable(Rulebook rb, Category category, int rows, int cols)
        {
            var list = new List<TableCell>(rows * cols);
            for (int r = 1; r <= rows; r++)
                for (int c = 1; c <= cols; c++)
                {
                    list.Add(new TableCell(rb, category, r, c, GetImagePath(rb, category, r, c)));
                }
            return list;
        }
    }

    public sealed record TableCell(Rulebook Rulebook, Category Category, int Row, int Col, string? ImagePath);
}
