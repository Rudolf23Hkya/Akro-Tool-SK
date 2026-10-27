using System;
using System.Windows;

namespace Atletika_SutaznyPlan_Generator
{
    // Laptops often run 1920×1080 at 125–150 % scaling, which leaves only ~690–830 px (WPF units) of height.
    // The windows are designed for more, so shrink size and minimum size to the screen's work area
    // (screen minus taskbar) and center the window; the content scrolls where needed.
    public static class WindowSizing
    {
        public static void FitToWorkArea(Window window)
        {
            var area = SystemParameters.WorkArea;

            window.MinWidth = Math.Min(window.MinWidth, area.Width);
            window.MinHeight = Math.Min(window.MinHeight, area.Height);

            if (!double.IsNaN(window.Width))
                window.Width = Math.Min(window.Width, area.Width);
            if (!double.IsNaN(window.Height))
                window.Height = Math.Min(window.Height, area.Height);

            window.WindowStartupLocation = window.Owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen;
        }
    }
}
