using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atletika_SutaznyPlan_Generator.Models
{
    public static class CategoryMap
    {
        // folder names in your structure
        public static readonly IReadOnlyDictionary<Category, string> FolderName = new Dictionary<Category, string>
        {
            [Category.WP] = "wp",
            [Category.MP] = "mp",
            [Category.MxP] = "m_x_p",
            [Category.WG] = "wg",
            [Category.MG] = "mg",
            [Category.Inv] = "inv",
        };
    }
}
