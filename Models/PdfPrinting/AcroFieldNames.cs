using Atletika_SutaznyPlan_Generator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public static class AcroFieldNames
    {
        public const string EventName = "eventName";
        public const string ExerciseDifficulityTotal = "ExerciseDifficulity"; // overall or header field
        public const string ExerciseIdTotal = "ExerciseID";
        public const string CatNum2 = "catNum2";
        public const string CatNum1 = "catNum1";
        public const string Category1 = "category1";
        public const string Category2 = "category2";
        public const string InvidualnaZostava = "invidualnaZostava";
        public const string StalkovaZostava = "stalkovaZostava";
        public const string Discipline = "discipline";
        public const string TeamSizeText = "teamSizeText";
        public const string OverallDifficulity = "overallDifficulity";
        public const string Trainer = "trainer";
        public const string AthleteTop = "athleteTop";
        public const string AthleteMiddle1 = "athleteMiddle[1]";
        public const string AthleteMiddle2 = "athleteMiddle[2]";
        public const string AthleteBase = "athleteBase";
        public const string ClubName = "clubName";

        // Nested
        public const string TrainerName = "trainer.name";
        public const string TrainerContact = "trainer.contact";

        public const string AthleteTopName = "athleteTop.name";
        public const string AthleteTopDob = "athleteTop.dateOfBirth";

        public const string AthleteMiddle1Name = "athleteMiddle[1].name";
        public const string AthleteMiddle1Dob = "athleteMiddle[1].dateOfBirth";

        public const string AthleteMiddle2Name = "athleteMiddle[2].name";
        public const string AthleteMiddle2Dob = "athleteMiddle[2].dateOfBirth";

        public const string AthleteBaseName = "athleteBase.name";
        public const string AthleteBaseDob = "athleteBase.dateOfBirth";

        // Picture boxes on the sheet: index 0..(box count - 1); the template decides how many exist (16, #30)
        public const string ImageFieldsParent = "Image1_af_image";
        public static string ImageField(int box) => $"{ImageFieldsParent}.{box}";
        public static string ExerciseDifficultyField(int box) => $"ExerciseDifficulity.{box}";
        public static string ExerciseIdField(int box) => $"ExerciseID.{box}";

        // Box number text ("1", "2.a", "2.b", ...) — optional; exists only in templates edited for #30
        public static string BoxNumberField(int box) => $"BoxNumber.{box}";
    }


    // One exercise inside an Okno
    public sealed record PickedExercise(
        int X,                  // column (1-based)
        decimal Difficulty,     // from the table's column value
        string ImagePath,
        int MemberIndex,        // -1 = whole team; 0.. = team member in Top -> Base order (Zvlášť, issues #29, #46)
        string? Position        // Zvlášť: "Base", "Middle", "Middle1", "Middle2", "Top" (printed as R1.Top); null = whole team
    );

    // One picture box on the printed sheet (issue #30). Okno in routine order; a Zvlášť Okno n
    // becomes n.a, n.b, ... and only n.a carries the Okno value; n.b, n.c, ... are continuation boxes
    // whose value cell is greyed out (issue #44).
    public sealed record PrintBox(string Label, string ImagePath, decimal? Difficulty, string ExerciseId,
                                  bool IsContinuation = false);

    // One Okno: one exercise for the whole team, or one per team member from the same row (Zvlášť)
    public sealed record ExerciseSlotSelection(
        int SlotIndex,          // 0..SlotCount-1
        Rulebook Rulebook,
        Category Category,
        int Y,                  // row (1-based), the same for all exercises of the Okno
        IReadOnlyList<PickedExercise> Exercises)
    {
        public bool IsSeparate => Exercises.Count > 1;

        // Okno value = average of its exercises, rounded to 3 decimals per Okno (decision D5)
        public decimal Difficulty =>
            Math.Round(Exercises.Average(e => e.Difficulty), 3, MidpointRounding.AwayFromZero);

        // First exercise = the one printed in the Okno's own box
        public int X => Exercises[0].X;
        public string ImagePath => Exercises[0].ImagePath;
    }
}