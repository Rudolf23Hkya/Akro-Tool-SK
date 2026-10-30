using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Atletika_SutaznyPlan_Generator.Models.PlanHistory
{
    // === Súťažný plán history (issue #45): one JSON file per printed plan ===
    // Stored in %LOCALAPPDATA%\Akro Tool SK\history — not next to the exe, because ClickOnce installs every
    // update into a new folder. The smoke test sets AKRO_TOOL_HISTORY_DIR so it never touches a coach's history.
    // Saving and reading never throw: problems are traced and must never block printing.
    public sealed class PlanHistoryStore
    {
        public const string DirectoryEnvironmentVariable = "AKRO_TOOL_HISTORY_DIR";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            // Slovak names stay readable in the file
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public string Directory { get; }

        public PlanHistoryStore(string directory)
        {
            Directory = directory;
        }

        public static string DefaultDirectory()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Akro Tool SK", "history");
        }

        /// <summary>
        /// Adds the plan. If it is identical to the newest entry (printed twice in a row), only that entry's date
        /// is updated (Q9). Returns false (and traces the reason) when it could not be saved.
        /// </summary>
        public bool Save(PlanHistoryEntry entry)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                var newest = LoadAll().FirstOrDefault();
                var path = newest != null && IsSamePlan(newest.Entry, entry)
                    ? newest.FilePath
                    : Path.Combine(Directory,
                        $"{entry.SavedAt.LocalDateTime:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.json");

                // Temp file + move: a crash never leaves a half-written entry
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(entry, JsonOptions));
                File.Move(tempPath, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Plan history: could not save to '{Directory}': {ex.Message}");
                return false;
            }
        }

        /// <summary>All readable entries, newest first. Damaged files are skipped (and traced).</summary>
        public IReadOnlyList<StoredPlan> LoadAll()
        {
            var plans = new List<StoredPlan>();
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    return plans;

                foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<PlanHistoryEntry>(File.ReadAllText(file), JsonOptions);
                        if (entry != null)
                            plans.Add(new StoredPlan(file, entry));
                        else
                            Trace.TraceWarning($"Plan history: empty file '{file}'");
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"Plan history: could not read '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Plan history: could not list '{Directory}': {ex.Message}");
            }

            return plans
                .OrderByDescending(p => p.Entry.SavedAt)
                .ThenByDescending(p => Path.GetFileName(p.FilePath), StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Removes one entry. Returns false when the file could not be deleted.</summary>
        public bool Delete(StoredPlan plan)
        {
            try
            {
                File.Delete(plan.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Plan history: could not delete '{plan.FilePath}': {ex.Message}");
                return false;
            }
        }

        // Same plan = same file content apart from the date
        private static bool IsSamePlan(PlanHistoryEntry a, PlanHistoryEntry b)
            => JsonSerializer.Serialize(a with { SavedAt = default }, JsonOptions)
               == JsonSerializer.Serialize(b with { SavedAt = default }, JsonOptions);
    }
}
