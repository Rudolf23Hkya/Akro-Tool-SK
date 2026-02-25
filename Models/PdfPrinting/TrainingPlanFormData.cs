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
        // Slot index 0..11 => selection
        private readonly ExerciseSlotSelection?[] _slots = new ExerciseSlotSelection?[12];

        // Difficulty rule can be swapped if needed
        public Func<int /*rowY*/, int /*colX*/, decimal> DifficultyRule { get; set; } = DefaultDifficulty;

        public IReadOnlyList<ExerciseSlotSelection?> Slots => _slots;

        public void SetSlot(int slotIndex, Rulebook rulebook, Category category, int x, int y, string imagePath)
        {
            if (slotIndex is < 0 or > 11) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (x is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(x));
            if (y is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(y));
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("ImagePath is required.", nameof(imagePath));

            _slots[slotIndex] = new ExerciseSlotSelection(slotIndex, rulebook, category, x, y, imagePath);
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex is < 0 or > 11) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            _slots[slotIndex] = null;
        }

        // ====== Computed per-slot values ======
        public decimal GetSlotDifficulty(int slotIndex)
        {
            var s = _slots[slotIndex] ?? throw new InvalidOperationException($"Slot {slotIndex} not set.");
            return DifficultyRule(s.Y, s.X);
        }

        public string GetSlotExerciseId(int slotIndex)
        {
            var s = _slots[slotIndex] ?? throw new InvalidOperationException($"Slot {slotIndex} not set.");
            var prefix = s.Category == Category.Inv ? "Inv" : "";
            return $"{prefix}R{s.Y}";
        }

        public decimal OverallDifficulty =>
            _slots.Where(s => s != null)
                  .Select(s => DifficultyRule(s!.Y, s!.X))
                  .Aggregate(0m, (acc, d) => acc + d);

        // ====== Export payloads for PDF fill ======

        /// <summary>Text fields to fill in the PDF.</summary>
        public Dictionary<string, string> ToTextFieldMap(CultureInfo? culture = null)
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

            // Per-slot computed fields
            for (int i = 0; i < 12; i++)
            {
                if (_slots[i] is null) continue;

                var diff = GetSlotDifficulty(i).ToString("0.0", culture);
                var id = GetSlotExerciseId(i);

                map[AcroFieldNames.ExerciseDifficultyFields[i]] = diff;
                map[AcroFieldNames.ExerciseIdFields[i]] = id;
            }

            // Overall
            map[AcroFieldNames.OverallDifficulity] = OverallDifficulty.ToString("0.0", culture);

            return map;
        }

        /// <summary>Image fields (Image1_af_image.N) -> path</summary>
        public List<(string fieldName, string imagePath)> ToImageFieldList()
        {
            var list = new List<(string, string)>(12);
            for (int i = 0; i < 12; i++)
            {
                var s = _slots[i];
                if (s is null) continue;

                list.Add((AcroFieldNames.ImageFields[i], s.ImagePath));
            }
            return list;
        }

        private static decimal DefaultDifficulty(int rowY, int colX)
        {
            // difficulty depends ONLY on column: 1..5 => 0.1..0.5
            return colX * 0.1m;
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

            var prefix = parts[0].ToLowerInvariant(); // 10r / 14r
            rulebook = prefix switch
            {
                "10r" => Rulebook.DO_10_ROK,
                "14r" => Rulebook.DO_14_ROK,
                _ => default
            };
            if (!Enum.IsDefined(rulebook)) return false;

            // Category token can be "mxp" or "m_x_p" depending on source
            // Everything between prefix and last two numbers is category
            var colToken = parts[^2];
            var rowToken = parts[^1];

            if (!int.TryParse(colToken, out x)) return false; // 01..05
            if (!int.TryParse(rowToken, out y)) return false; // 01..06

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
