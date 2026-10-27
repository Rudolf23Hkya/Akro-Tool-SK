namespace Atletika_SutaznyPlan_Generator.Models
{

        public enum Rulebook
        {
            DO_10_ROK,
            DO_14_ROK
        }

        public enum Category
        {
            WP,   // ženský pár
            MP,   // mužský pár
            MxP,  // kombinovaný pár
            WG,   // ženská trojica
            MG,   // mužská štvorica
            Inv   // individuálna zostava
        }
        public static class RulebookExtensions
        {
            // Labels live in the age-group definitions (AgeGroups.cs)
            public static string ToSlovakLabel(this Rulebook rb) => AgeGroups.Get(rb).Label;
        }
}
