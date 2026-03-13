using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Supervertaler.Trados.Settings
{
    /// <summary>
    /// Per-project settings overlay. Stored in a separate JSON file per Trados project
    /// at %LocalAppData%\Supervertaler.Trados\projects\{key}.json.
    /// Only contains settings that vary between projects (termbase path, enabled/disabled
    /// termbases, write targets, etc.). Global settings (API keys, UI prefs) stay in
    /// the main settings.json.
    /// </summary>
    [DataContract]
    public class ProjectSettings
    {
        private static readonly string ProjectsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Supervertaler.Trados", "projects");

        // ─── Human-readable metadata ────────────────────────────────

        /// <summary>
        /// Full path to the .sdlproj file. Stored for human readability only —
        /// the file is looked up by hash key, not by this path.
        /// </summary>
        [DataMember(Name = "projectPath")]
        public string ProjectPath { get; set; } = "";

        /// <summary>
        /// Trados project name. Stored for human readability only.
        /// </summary>
        [DataMember(Name = "projectName")]
        public string ProjectName { get; set; } = "";

        // ─── Per-project termbase settings ──────────────────────────

        /// <summary>
        /// Path to the Supervertaler SQLite database for this project.
        /// </summary>
        [DataMember(Name = "termbasePath")]
        public string TermbasePath { get; set; } = "";

        /// <summary>
        /// IDs of termbases marked as write targets for this project.
        /// </summary>
        [DataMember(Name = "writeTermbaseIds")]
        public List<long> WriteTermbaseIds { get; set; } = new List<long>();

        /// <summary>
        /// ID of the termbase marked as "Project" (pink highlighting) for this project.
        /// -1 means not set.
        /// </summary>
        [DataMember(Name = "projectTermbaseId")]
        public long ProjectTermbaseId { get; set; } = -1;

        /// <summary>
        /// IDs of Supervertaler termbases the user has disabled for this project.
        /// </summary>
        [DataMember(Name = "disabledTermbaseIds")]
        public List<long> DisabledTermbaseIds { get; set; } = new List<long>();

        /// <summary>
        /// Synthetic IDs of MultiTerm termbases disabled for this project.
        /// </summary>
        [DataMember(Name = "disabledMultiTermIds")]
        public List<long> DisabledMultiTermIds { get; set; } = new List<long>();

        /// <summary>
        /// IDs of termbases excluded from AI context for this project.
        /// </summary>
        [DataMember(Name = "disabledAiTermbaseIds")]
        public List<long> DisabledAiTermbaseIds { get; set; } = new List<long>();

        // ─── Static helpers ─────────────────────────────────────────

        /// <summary>
        /// Computes a stable, filesystem-safe key from the .sdlproj path.
        /// Uses SHA256 truncated to 12 hex characters.
        /// </summary>
        public static string GetProjectKey(string projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath))
                return null;

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(projectFilePath.Trim().ToLowerInvariant()));
                var sb = new StringBuilder(12);
                for (int i = 0; i < 6; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Returns the full path to the project settings file for the given project.
        /// </summary>
        private static string GetProjectSettingsPath(string projectFilePath)
        {
            var key = GetProjectKey(projectFilePath);
            if (key == null) return null;
            return Path.Combine(ProjectsDir, key + ".json");
        }

        /// <summary>
        /// Checks whether project-specific settings exist for the given project.
        /// </summary>
        public static bool HasProjectSettings(string projectFilePath)
        {
            var path = GetProjectSettingsPath(projectFilePath);
            return path != null && File.Exists(path);
        }

        /// <summary>
        /// Loads project-specific settings for the given .sdlproj path.
        /// Returns null if no project settings file exists.
        /// </summary>
        public static ProjectSettings Load(string projectFilePath)
        {
            try
            {
                var path = GetProjectSettingsPath(projectFilePath);
                if (path == null || !File.Exists(path))
                    return null;

                var json = File.ReadAllText(path, Encoding.UTF8);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ProjectSettings));
                    var ps = (ProjectSettings)serializer.ReadObject(stream);

                    // Null-safety for lists
                    if (ps.WriteTermbaseIds == null) ps.WriteTermbaseIds = new List<long>();
                    if (ps.DisabledTermbaseIds == null) ps.DisabledTermbaseIds = new List<long>();
                    if (ps.DisabledMultiTermIds == null) ps.DisabledMultiTermIds = new List<long>();
                    if (ps.DisabledAiTermbaseIds == null) ps.DisabledAiTermbaseIds = new List<long>();

                    return ps;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves project-specific settings for the given .sdlproj path.
        /// </summary>
        public static void Save(string projectFilePath, ProjectSettings ps)
        {
            try
            {
                var path = GetProjectSettingsPath(projectFilePath);
                if (path == null) return;

                Directory.CreateDirectory(ProjectsDir);

                using (var stream = new MemoryStream())
                {
                    var serializerSettings = new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true
                    };
                    var serializer = new DataContractJsonSerializer(typeof(ProjectSettings), serializerSettings);
                    serializer.WriteObject(stream, ps);

                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
            }
            catch
            {
                // Silently ignore save failures
            }
        }
    }
}
