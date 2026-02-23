using Atletika_SutaznyPlan_Generator.Models.PdfPrinting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        TestPdf(); // <-- run once at startup
    }

    private void TestPdf()
    {
        var baseDir = AppContext.BaseDirectory;

        var templatePath = Path.Combine(baseDir, "Cvicebny-plan_Template.pdf");
        var outputPath = Path.Combine(baseDir, "OUT_training_plan.pdf");

        // Pick ONE image that exists, repeat it 12x for testing
        var img = Path.Combine(baseDir, "DataBase", "do10_db", "inv", "10r_inv_01_01.png");
        var imagePaths = Enumerable.Repeat(img, 12).ToList();

        TrainingPlanPdf.Fill12Images(templatePath, outputPath, imagePaths);

        Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
    }
}