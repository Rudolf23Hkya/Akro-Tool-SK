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
            public static string ToSlovakLabel(this Rulebook rb) => rb switch
            {
                Rulebook.DO_10_ROK => "Do 10 rokov",
                Rulebook.DO_14_ROK => "Do 14 rokov",
                _ => rb.ToString()
            };
        }
}
