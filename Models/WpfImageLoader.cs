using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Atletika_SutaznyPlan_Generator.Models
{

    public static class WpfImageLoader
    {
        public static ImageSource? Load(string? absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // avoids locking the file
            bmp.UriSource = new Uri(absolutePath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze(); // safe for background threads + binding
            return bmp;
        }
    }
}
