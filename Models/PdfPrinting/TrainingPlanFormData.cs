using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public sealed class TrainingPlanFormData
    {
        // ====== User-entered fields ======
        public string? EventName { get; set; }
        public string? CatNum1 { get; set; }
        public string? CatNum2 { get; set; }
        public string? Category1 { get; set; }
        public string? Category2 { get; set; }
        public string? InvidualnaZostava { get; set; }
        public string? StalkovaZostava { get; set; }
        public string? Discipline { get; set; }
        public string? TeamSizeText { get; set; }
        public string? ClubName { get; set; }

        public string? TrainerName { get; set; }
        public string? TrainerContact { get; set; }

        public string? AthleteTopName { get; set; }
        public string? AthleteTopDob { get; set; }

        public string? AthleteMiddle1Name { get; set; }
        public string? AthleteMiddle1Dob { get; set; }

        public string? AthleteMiddle2Name { get; set; }
        public string? AthleteMiddle2Dob { get; set; }

        public string? AthleteBaseName { get; set; }
        public string? AthleteBaseDob { get; set; }

        // ====== Selected images ======
        // Slot index 0..SlotCount-1 => selection. The template has 12 boxes, so 12 is the maximum.
        public const int MaxSlots = 12;
        private readonly ExerciseSlotSelection?[] _slots = new ExerciseSlotSelection?[MaxSlots];

        // Number of Okno of the selected age group
        public int SlotCount { get; private set; } = MaxSlots;

        public IReadOnlyList<ExerciseSlotSelection?> Slots => new ArraySegment<ExerciseSlotSelection?>(_slots, 0, SlotCount);

        /// <summary>Clears all slots and sets the number of slots (age group changed or table cleared).</summary>
        public void ResetSlots(int slotCount)
        {
            if (slotCount is < 1 or > MaxSlots) throw new ArgumentOutOfRangeException(nameof(slotCount));
            Array.Clear(_slots);
            SlotCount = slotCount;
        }

        // One exercise for the whole team
        public void SetSlot(int slotIndex, Rulebook rulebook, Category category, int x, int y, string imagePath)
            => SetSlot(slotIndex, rulebook, category, y, new (int, string, int, string?)[] { (x, imagePath, -1, null) });

        // One or more exercises from the same row (Zvlášť: one per team member, issue #29)
        public void SetSlot(int slotIndex, Rulebook rulebook, Category category, int y,
                            IReadOnlyList<(int X, string ImagePath, int MemberIndex, string? Position)> picks)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (picks.Count == 0) throw new ArgumentException("At least one exercise is required.", nameof(picks));

            var table = AgeGroups.Get(rulebook).GetTable(category);
            if (y < 1 || y > table.Rows) throw new ArgumentOutOfRangeException(nameof(y));

            var exercises = new List<PickedExercise>(picks.Count);
            foreach (var (x, imagePath, memberIndex, position) in picks)
            {
                if (x < 1 || x > table.Cols) throw new ArgumentOutOfRangeException(nameof(picks), $"Column {x} is outside the table.");
                if (table.IsEmpty(y, x)) throw new ArgumentException($"No exercise at row {y}, column {x}.");
                if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("ImagePath is required.", nameof(picks));

                exercises.Add(new PickedExercise(x, table.GetDifficulty(x), imagePath, memberIndex, position));
            }

            _slots[slotIndex] = new ExerciseSlotSelection(slotIndex, rulebook, category, y, exercises);
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            _slots[slotIndex] = null;
        }

        // ====== Computed per-slot values ======
        public decimal GetSlotDifficulty(int slotIndex)
        {
            var s = _slots[slotIndex] ?? throw new InvalidOperationException($"Slot {slotIndex} not set.");
            return s.Difficulty;
        }

        public string GetSlotExerciseId(int slotIndex)
        {
            var s = _slots[slotIndex] ?? throw new InvalidOperationException($"Slot {slotIndex} not set.");
            // "Inv" only distinguishes the two tables; Individuálna zostava has just one table -> plain R{row}
            var prefix = s.Category == Category.Inv && AgeGroups.Get(s.Rulebook).HasTeamTables ? "Inv" : "";
            return $"{prefix}R{s.Y}";
        }

        public decimal OverallDifficulty =>
            Slots.Where(s => s != null)
                 .Select(s => s!.Difficulty)
                 .Aggregate(0m, (acc, d) => acc + d);

        // ====== Export payloads for PDF fill ======

        /// <summary>
        /// Text fields of the sheet header (everything except the picture boxes). Printed on every page.
        /// </summary>
        public Dictionary<string, string> ToHeaderFieldMap(CultureInfo? culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;

            var map = new Dictionary<string, string>();

            void Add(string field, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    map[field] = value!;
            }

            // Header / general
            Add(AcroFieldNames.EventName, EventName);
            Add(AcroFieldNames.CatNum1, CatNum1);
            Add(AcroFieldNames.CatNum2, CatNum2);
            Add(AcroFieldNames.Category1, Category1);
            Add(AcroFieldNames.Category2, Category2);
            Add(AcroFieldNames.InvidualnaZostava, InvidualnaZostava);
            Add(AcroFieldNames.StalkovaZostava, StalkovaZostava);
            Add(AcroFieldNames.Discipline, Discipline);
            Add(AcroFieldNames.TeamSizeText, TeamSizeText);
            Add(AcroFieldNames.ClubName, ClubName);

            // People
            Add(AcroFieldNames.TrainerName, TrainerName);
            Add(AcroFieldNames.TrainerContact, TrainerContact);

            Add(AcroFieldNames.AthleteTopName, AthleteTopName);
            Add(AcroFieldNames.AthleteTopDob, AthleteTopDob);

            Add(AcroFieldNames.AthleteMiddle1Name, AthleteMiddle1Name);
            Add(AcroFieldNames.AthleteMiddle1Dob, AthleteMiddle1Dob);

            Add(AcroFieldNames.AthleteMiddle2Name, AthleteMiddle2Name);
            Add(AcroFieldNames.AthleteMiddle2Dob, AthleteMiddle2Dob);

            Add(AcroFieldNames.AthleteBaseName, AthleteBaseName);
            Add(AcroFieldNames.AthleteBaseDob, AthleteBaseDob);

            // Overall
            map[AcroFieldNames.OverallDifficulity] = OverallDifficulty.ToString("0.000", culture);

            return map;
        }

        /// <summary>
        /// Picture boxes in routine order (issue #30, Variant B): Okno n -> box "n"; a Zvlášť Okno n -> boxes
        /// "n.a", "n.b", ... (members Base -> Top). Only the first box of a Zvlášť Okno gets the Okno value;
        /// its exercise IDs carry the position, e.g. "R1.Top" / "InvR1.Base".
        /// </summary>
        public IReadOnlyList<PrintBox> ToPrintBoxes()
        {
            var boxes = new List<PrintBox>();
            for (int i = 0; i < SlotCount; i++)
            {
                var s = _slots[i];
                if (s is null) continue;

                var id = GetSlotExerciseId(i);
                var number = (i + 1).ToString(CultureInfo.InvariantCulture);

                if (!s.IsSeparate)
                {
                    boxes.Add(new PrintBox(number, s.ImagePath, s.Difficulty, id));
                    continue;
                }

                for (int k = 0; k < s.Exercises.Count; k++)
                {
                    var e = s.Exercises[k];
                    boxes.Add(new PrintBox(
                        $"{number}.{(char)('a' + k)}",
                        e.ImagePath,
                        k == 0 ? s.Difficulty : null,
                        e.Position is null ? id : $"{id}.{e.Position}"));
                }
            }
            return boxes;
        }

        public void SetSlotFromImagePath(int slotIndex, string imagePath)
        {
            if (!TryParseImageFileName(Path.GetFileName(imagePath), out var rb, out var cat, out var x, out var y))
                throw new ArgumentException($"Invalid image filename format: {imagePath}", nameof(imagePath));

            SetSlot(slotIndex, rb, cat, x, y, imagePath);
        }

        public static bool TryParseImageFileName(string fileName, out Rulebook rulebook, out Category category, out int x, out int y)
        {
            // Expected: 10r_mxp_01_01.png  => prefix_cat_col_row.png
            rulebook = default;
            category = default;
            x = 0; y = 0;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return false;

            var prefix = parts[0]; // 10r / 14r
            var group = AgeGroups.All.FirstOrDefault(g => g.FilePrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase));
            if (group is null) return false;
            rulebook = group.Rulebook;

            // Category token can be "mxp" or "m_x_p" depending on source
            // Everything between prefix and last two numbers is category
            var colToken = parts[^2];
            var rowToken = parts[^1];

            if (!int.TryParse(colToken, out x)) return false; // column
            if (!int.TryParse(rowToken, out y)) return false; // row

            var catToken = string.Join("_", parts.Skip(1).Take(parts.Length - 3)).ToLowerInvariant();

            category = catToken switch
            {
                "wp" => Category.WP,
                "mp" => Category.MP,
                "mxp" => Category.MxP,
                "m_x_p" => Category.MxP,
                "wg" => Category.WG,
                "mg" => Category.MG,
                "inv" => Category.Inv,
                _ => default
            };

            return Enum.IsDefined(category);
        }
    }
}
