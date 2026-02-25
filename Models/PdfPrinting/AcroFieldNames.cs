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

        public static IReadOnlyList<string> ImageFields { get; } =
            Enumerable.Range(0, 12).Select(i => $"Image1_af_image.{i}").ToList();

        public static IReadOnlyList<string> ExerciseDifficultyFields { get; } =
            Enumerable.Range(0, 12).Select(i => $"ExerciseDifficulity.{i}").ToList();

        public static IReadOnlyList<string> ExerciseIdFields { get; } =
            Enumerable.Range(0, 12).Select(i => $"ExerciseID.{i}").ToList();
    }


    public sealed record ExerciseSlotSelection(
        int SlotIndex,          // 0..11
        Rulebook Rulebook,
        Category Category,
        int X,                  // 1..5 (column)
        int Y,                  // 1..6 (row)
        string ImagePath
);
}