using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;

namespace Atletika_SutaznyPlan_Generator.Models
{
    public sealed class ExerciseImageRepository
    {
        private readonly string _dbRoot;

        // Key = (rulebook, table id, row, col) -> absolute file path
        private readonly ConcurrentDictionary<(Rulebook, string tableId, int row, int col), string> _index = new();

        // Things that don't match the table definitions (shown by "--check-database", also traced at startup)
        private readonly List<string> _problems = new();
        public IReadOnlyList<string> Problems => _problems;

        // Groups: prefix(10r), cat(inv), column (01), row (06)
        private static readonly Regex FileRx = new(
            @"^(?<prefix>[a-z0-9]+)_(?<cat>[a-z_]+)_(?<a>\d{2})_(?<b>\d{2})\.png$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ExerciseImageRepository(string dbRoot)
        {
            _dbRoot = dbRoot;
            BuildIndex();
        }

        // Walks up to 8 parents from the exe folder looking for DataBase|Database|database with an age-group folder.
        // Fallback: the exe folder (the index stays empty, all tiles show the placeholder).
        public static string ResolveDbRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                foreach (var folderName in new[] { "DataBase", "Database", "database" })
                {
                    var candidate = Path.Combine(dir.FullName, folderName);

                    if (AgeGroups.All.Any(g => Directory.Exists(Path.Combine(candidate, g.DbFolder))))
                        return candidate;
                }
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void BuildIndex()
        {
            foreach (var group in AgeGroups.All)
            {
                if (group.IndexFile != null)
                    IndexFromCsv(group);
                else
                    IndexFromFileNames(group);
            }
        }

        // Classic layout (Do 10 / Do 14): <DbFolder>/<table id>/<prefix>_<cat>_<COL>_<ROW>.png
        private void IndexFromFileNames(AgeGroupDefinition group)
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

        // Owner's export (#27): <DbFolder>/index.csv with the columns id, row, column, value|difficulty, file
        // (other columns are ignored, column order may differ between folders).
        private void IndexFromCsv(AgeGroupDefinition group)
        {
            var folder = Path.Combine(_dbRoot, group.DbFolder);
            var csvPath = Path.Combine(folder, group.IndexFile!);
            if (!File.Exists(csvPath)) return;

            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length == 0) return;

            var header = ParseCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
            int Col(params string[] names) => names.Select(n => header.IndexOf(n)).FirstOrDefault(i => i >= 0, -1);

            int idCol = Col("id"), rowCol = Col("row"), columnCol = Col("column"), valueCol = Col("value", "difficulty"), fileCol = Col("file");
            if (idCol < 0 || rowCol < 0 || columnCol < 0 || fileCol < 0)
            {
                _problems.Add($"{group.Label}: {group.IndexFile} has no id/row/column/file column.");
                return;
            }

            for (int n = 1; n < lines.Length; n++)
            {
                if (string.IsNullOrWhiteSpace(lines[n])) continue;

                var cells = ParseCsvLine(lines[n]);
                string Cell(int i) => i >= 0 && i < cells.Count ? cells[i].Trim() : "";
                var where = $"{group.Label}, {group.IndexFile} line {n + 1} ({Cell(idCol)})";

                // Table from the id prefix ("DUO-I-1"); without prefixes the folder holds only the Inv table
                ExerciseTable? table;
                if (group.IndexTablesByIdPrefix.Count == 0)
                    table = group.InvTable;
                else
                    group.IndexTablesByIdPrefix.TryGetValue(Cell(idCol).Split('-')[0].ToUpperInvariant(), out table);

                if (table is null) { _problems.Add($"{where}: unknown table."); continue; }

                if (!TryParseRowLabel(Cell(rowCol), out var row)) { _problems.Add($"{where}: unknown row '{Cell(rowCol)}'."); continue; }
                if (!int.TryParse(Cell(columnCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out var col))
                { _problems.Add($"{where}: column '{Cell(columnCol)}' is not a number."); continue; }

                if (!table.HasExercise(row, col)) { _problems.Add($"{where}: row {row}, column {col} is not an exercise of table {table.DisplayName}."); continue; }

                // The value in the CSV is only checked; the table definition is what counts
                var valueText = Cell(valueCol).Replace(',', '.');
                if (valueText.Length > 0 &&
                    decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
                    value != table.GetDifficulty(col))
                {
                    _problems.Add($"{where}: value {value:0.00} in the CSV, {table.GetDifficulty(col):0.00} in the table definition.");
                }

                var file = Path.Combine(folder, Cell(fileCol));
                if (!File.Exists(file)) { _problems.Add($"{where}: file '{Cell(fileCol)}' is missing."); continue; }

                _index[(group.Rulebook, table.Id, row, col)] = Path.GetFullPath(file);
            }
        }

        // "Riadok IV" -> 4; "Pružnosť (2 s)" / "Statika (2 s)" / "Dynamika" -> 1 / 2 / 3; "C" -> 3; "4" -> 4
        private static bool TryParseRowLabel(string label, out int row)
        {
            row = 0;
            label = label.Trim();

            if (label.StartsWith("Riadok", StringComparison.OrdinalIgnoreCase))
                return TryParseRoman(label.Substring("Riadok".Length).Trim(), out row);

            if (label.StartsWith("Pružnosť", StringComparison.OrdinalIgnoreCase)) { row = 1; return true; }
            if (label.StartsWith("Statika", StringComparison.OrdinalIgnoreCase)) { row = 2; return true; }
            if (label.StartsWith("Dynamika", StringComparison.OrdinalIgnoreCase)) { row = 3; return true; }

            if (label.Length == 1 && char.IsLetter(label[0]))
            {
                row = char.ToUpperInvariant(label[0]) - 'A' + 1;
                return true;
            }

            return int.TryParse(label, NumberStyles.Integer, CultureInfo.InvariantCulture, out row) && row > 0;
        }

        private static bool TryParseRoman(string text, out int value)
        {
            var digits = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10 };
            value = 0;
            text = text.ToUpperInvariant();
            if (text.Length == 0 || text.Any(c => !digits.ContainsKey(c))) return false;

            for (int i = 0; i < text.Length; i++)
            {
                var v = digits[text[i]];
                value += i + 1 < text.Length && digits[text[i + 1]] > v ? -v : v;
            }
            return value > 0;
        }

        // Comma-separated, values may be quoted ("0,01"), "" inside quotes = one quote
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else current.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }

            result.Add(current.ToString());
            if (result.Count > 0) result[0] = result[0].TrimStart('﻿');
            return result;
        }

        public string? GetImagePath(Rulebook rb, Category category, int row, int col)
        {
            var group = AgeGroups.Get(rb);
            var table = group.GetTable(category);
            if (_index.TryGetValue((rb, table.Id, row, col), out var path))
                return path;

            // Picture not delivered yet: a real file always wins, the mock only fills the gaps
            if (group.MockImageRelativePath != null && table.HasExercise(row, col))
            {
                var mock = Path.GetFullPath(Path.Combine(_dbRoot, group.MockImageRelativePath));
                if (File.Exists(mock))
                    return mock;
            }

            return null;
        }

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

        // Per table: how many exercises have a picture, and which positions are missing (for "--check-database")
        public IEnumerable<string> DescribeCoverage()
        {
            foreach (var group in AgeGroups.All)
            {
                foreach (var table in group.AllTables)
                {
                    var missing = new List<string>();
                    int expected = 0;
                    for (int r = 1; r <= table.Rows; r++)
                        for (int c = 1; c <= table.Cols; c++)
                        {
                            if (!table.HasExercise(r, c)) continue;
                            expected++;
                            if (!_index.ContainsKey((group.Rulebook, table.Id, r, c)))
                                missing.Add($"R{r}/S{c}");
                        }

                    var line = $"{group.Label,-22} {table.DisplayName,-9} ({table.Id}): {expected - missing.Count}/{expected} pictures";
                    if (missing.Count > 0)
                        line += $"; missing: {string.Join(", ", missing)}" +
                                (group.MockImageRelativePath != null ? " (mock picture shown)" : " (placeholder shown)");
                    yield return line;
                }
            }
        }
    }

    public sealed record TableCell(Rulebook Rulebook, Category Category, int Row, int Col, string? ImagePath,
                                   bool IsEmpty, decimal Difficulty);
}
