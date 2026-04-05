using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Trados Studio query tools that the AI Assistant can invoke via tool use.
    /// These read local Trados data (projects.xml, TM metadata, etc.) and return
    /// JSON results that the LLM can use to answer user questions.
    /// </summary>
    public static class TradosTools
    {
        // ─── Tool Definitions (JSON for Claude API) ──────────────────

        /// <summary>
        /// Returns the tool definitions array as a JSON string for the Claude API.
        /// </summary>
        public static string GetToolDefinitionsJson()
        {
            return @"[
  {
    ""name"": ""studio_list_projects"",
    ""description"": ""Lists all projects registered in Trados Studio with their name, status, creation date, and file path. Use this when the user asks about their projects, project status, or wants an overview of their work."",
    ""input_schema"": {
      ""type"": ""object"",
      ""properties"": {
        ""status_filter"": {
          ""type"": ""string"",
          ""description"": ""Optional filter: 'InProgress', 'Completed', or 'Archived'. Omit to list all."",
          ""enum"": [""InProgress"", ""Completed"", ""Archived""]
        }
      },
      ""required"": []
    }
  },
  {
    ""name"": ""studio_get_project"",
    ""description"": ""Gets detailed information about a specific Trados Studio project by name, including source/target languages, files, and status. Use when the user asks about a specific project."",
    ""input_schema"": {
      ""type"": ""object"",
      ""properties"": {
        ""project_name"": {
          ""type"": ""string"",
          ""description"": ""The name (or partial name) of the project to look up.""
        }
      },
      ""required"": [""project_name""]
    }
  },
  {
    ""name"": ""studio_list_tms"",
    ""description"": ""Lists all translation memories (TMs) registered in Trados Studio with their name, file path, and language direction. Use when the user asks about their translation memories or TM setup."",
    ""input_schema"": {
      ""type"": ""object"",
      ""properties"": {},
      ""required"": []
    }
  },
  {
    ""name"": ""studio_list_project_templates"",
    ""description"": ""Lists all project templates available in Trados Studio. Use when the user asks about their templates or wants to know which templates are available."",
    ""input_schema"": {
      ""type"": ""object"",
      ""properties"": {},
      ""required"": []
    }
  }
]";
        }

        // ─── Tool Dispatch ──────────────────────────────────────────

        /// <summary>
        /// Executes a tool by name with the given JSON input arguments.
        /// Returns a JSON string with the result.
        /// </summary>
        public static string ExecuteTool(string toolName, string inputJson)
        {
            try
            {
                switch (toolName)
                {
                    case "studio_list_projects":
                        return ListProjects(ExtractJsonField(inputJson, "status_filter"));
                    case "studio_get_project":
                        return GetProject(ExtractJsonField(inputJson, "project_name"));
                    case "studio_list_tms":
                        return ListTranslationMemories();
                    case "studio_list_project_templates":
                        return ListProjectTemplates();
                    default:
                        return JsonError($"Unknown tool: {toolName}");
                }
            }
            catch (Exception ex)
            {
                return JsonError($"Tool execution error: {ex.Message}");
            }
        }

        // ─── Tool Implementations ───────────────────────────────────

        private static string ListProjects(string statusFilter)
        {
            var xmlPath = GetProjectsXmlPath();
            if (xmlPath == null || !File.Exists(xmlPath))
                return JsonError("Could not find Trados Studio projects.xml. Is Trados Studio installed?");

            var doc = XDocument.Load(xmlPath);
            var items = doc.Descendants("ProjectListItem").ToList();

            var sb = new StringBuilder();
            sb.Append("{\"projects\":[");
            int count = 0;

            foreach (var item in items)
            {
                var info = item.Element("ProjectInfo");
                if (info == null) continue;

                var name = info.Attribute("Name")?.Value ?? "";
                var status = info.Attribute("Status")?.Value ?? "";
                var createdAt = info.Attribute("CreatedAt")?.Value ?? "";
                var projectFilePath = item.Attribute("ProjectFilePath")?.Value ?? "";

                // Apply status filter if specified
                if (!string.IsNullOrEmpty(statusFilter) &&
                    !status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Map status to friendly name
                var friendlyStatus = MapStatus(status);

                // Format creation date
                var dateStr = FormatDate(createdAt);

                if (count > 0) sb.Append(",");
                sb.Append("{");
                sb.Append("\"name\":").Append(JsonStr(name));
                sb.Append(",\"status\":").Append(JsonStr(friendlyStatus));
                sb.Append(",\"created\":").Append(JsonStr(dateStr));
                if (!string.IsNullOrEmpty(projectFilePath))
                {
                    var folder = Path.GetDirectoryName(ResolveProjectPath(projectFilePath, xmlPath));
                    sb.Append(",\"path\":").Append(JsonStr(folder ?? ""));
                }
                sb.Append("}");
                count++;
            }

            sb.Append("],\"total\":").Append(count).Append("}");
            return sb.ToString();
        }

        private static string GetProject(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return JsonError("Project name is required.");

            var xmlPath = GetProjectsXmlPath();
            if (xmlPath == null || !File.Exists(xmlPath))
                return JsonError("Could not find Trados Studio projects.xml.");

            var doc = XDocument.Load(xmlPath);
            var items = doc.Descendants("ProjectListItem").ToList();

            // Find project by name (case-insensitive, partial match)
            var searchLower = projectName.ToLowerInvariant();
            var match = items.FirstOrDefault(i =>
            {
                var name = i.Element("ProjectInfo")?.Attribute("Name")?.Value;
                return name != null && name.ToLowerInvariant().Contains(searchLower);
            });

            if (match == null)
                return JsonError($"No project found matching '{projectName}'.");

            var info = match.Element("ProjectInfo");
            var name2 = info?.Attribute("Name")?.Value ?? "";
            var status = info?.Attribute("Status")?.Value ?? "";
            var createdAt = info?.Attribute("CreatedAt")?.Value ?? "";
            var projectFilePath = match.Attribute("ProjectFilePath")?.Value ?? "";

            // Try to read the .sdlproj file for more details
            var projPath = ResolveProjectPath(projectFilePath, xmlPath);
            string sourceLang = null;
            var targetLangs = new List<string>();
            var files = new List<string>();

            if (projPath != null && File.Exists(projPath))
            {
                try
                {
                    var projDoc = XDocument.Load(projPath);
                    var ns = projDoc.Root?.GetDefaultNamespace();

                    // Source language
                    var slElem = projDoc.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "SourceLanguageCode");
                    sourceLang = slElem?.Value;

                    // Target languages
                    var tlElems = projDoc.Descendants()
                        .Where(e => e.Name.LocalName == "TargetLanguageCode");
                    foreach (var tl in tlElems)
                    {
                        if (!string.IsNullOrEmpty(tl.Value) && !targetLangs.Contains(tl.Value))
                            targetLangs.Add(tl.Value);
                    }

                    // Language directions (fallback)
                    if (targetLangs.Count == 0)
                    {
                        var langDirs = projDoc.Descendants()
                            .Where(e => e.Name.LocalName == "LanguageDirection");
                        foreach (var ld in langDirs)
                        {
                            var tc = ld.Attribute("TargetLanguageCode")?.Value;
                            if (!string.IsNullOrEmpty(tc) && !targetLangs.Contains(tc))
                                targetLangs.Add(tc);
                            if (sourceLang == null)
                                sourceLang = ld.Attribute("SourceLanguageCode")?.Value;
                        }
                    }

                    // Project files
                    var fileElems = projDoc.Descendants()
                        .Where(e => e.Name.LocalName == "FileVersion" || e.Name.LocalName == "LanguageFile");
                    foreach (var f in fileElems)
                    {
                        var fname = f.Attribute("FileName")?.Value ?? f.Attribute("Name")?.Value;
                        if (!string.IsNullOrEmpty(fname) && !files.Contains(fname)
                            && !fname.EndsWith(".sdlproj", StringComparison.OrdinalIgnoreCase))
                            files.Add(fname);
                    }
                }
                catch { /* Silently skip parse errors */ }
            }

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"name\":").Append(JsonStr(name2));
            sb.Append(",\"status\":").Append(JsonStr(MapStatus(status)));
            sb.Append(",\"created\":").Append(JsonStr(FormatDate(createdAt)));
            if (sourceLang != null)
                sb.Append(",\"sourceLanguage\":").Append(JsonStr(sourceLang));
            if (targetLangs.Count > 0)
            {
                sb.Append(",\"targetLanguages\":[");
                for (int i = 0; i < targetLangs.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(JsonStr(targetLangs[i]));
                }
                sb.Append("]");
            }
            if (files.Count > 0)
            {
                sb.Append(",\"files\":[");
                for (int i = 0; i < files.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(JsonStr(files[i]));
                }
                sb.Append("]");
            }
            var folder = Path.GetDirectoryName(projPath);
            if (!string.IsNullOrEmpty(folder))
                sb.Append(",\"path\":").Append(JsonStr(folder));
            sb.Append("}");
            return sb.ToString();
        }

        private static string ListTranslationMemories()
        {
            // TMs are listed in the Studio settings: ProgramData or user profile
            // Primary location: Documents\Studio 2024\Translation Memories\
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var tmFolders = new[]
            {
                Path.Combine(docsFolder, "Studio 2024", "Translation Memories"),
                Path.Combine(docsFolder, "Studio 2022", "Translation Memories")
            };

            var tmFiles = new List<string>();
            foreach (var folder in tmFolders)
            {
                if (Directory.Exists(folder))
                {
                    tmFiles.AddRange(Directory.GetFiles(folder, "*.sdltm", SearchOption.AllDirectories));
                }
            }

            // Also check projects.xml for TMs referenced in projects
            var xmlPath = GetProjectsXmlPath();
            if (xmlPath != null && File.Exists(xmlPath))
            {
                var doc = XDocument.Load(xmlPath);
                var tmPaths = doc.Descendants()
                    .Where(e => e.Name.LocalName == "TranslationProviderConfiguration"
                             || e.Name.LocalName == "MainTranslationProvider")
                    .SelectMany(e => e.Descendants())
                    .Where(e => e.Attribute("Uri")?.Value?.EndsWith(".sdltm") == true)
                    .Select(e => e.Attribute("Uri")?.Value)
                    .Where(u => u != null)
                    .Distinct();

                foreach (var tmUri in tmPaths)
                {
                    var path = tmUri.Replace("file:///", "").Replace("file://", "");
                    if (File.Exists(path) && !tmFiles.Contains(path))
                        tmFiles.Add(path);
                }
            }

            // Deduplicate by full path
            tmFiles = tmFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var sb = new StringBuilder();
            sb.Append("{\"translationMemories\":[");
            for (int i = 0; i < tmFiles.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var name = Path.GetFileNameWithoutExtension(tmFiles[i]);
                sb.Append("{\"name\":").Append(JsonStr(name));
                sb.Append(",\"path\":").Append(JsonStr(tmFiles[i]));
                sb.Append("}");
            }
            sb.Append("],\"total\":").Append(tmFiles.Count).Append("}");
            return sb.ToString();
        }

        private static string ListProjectTemplates()
        {
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var templateFolders = new[]
            {
                Path.Combine(docsFolder, "Studio 2024", "Project Templates"),
                Path.Combine(docsFolder, "Studio 2022", "Project Templates")
            };

            var templates = new List<string>();
            foreach (var folder in templateFolders)
            {
                if (Directory.Exists(folder))
                {
                    templates.AddRange(Directory.GetFiles(folder, "*.sdltpl", SearchOption.AllDirectories));
                }
            }

            var sb = new StringBuilder();
            sb.Append("{\"projectTemplates\":[");
            for (int i = 0; i < templates.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var name = Path.GetFileNameWithoutExtension(templates[i]);
                sb.Append("{\"name\":").Append(JsonStr(name));
                sb.Append(",\"path\":").Append(JsonStr(templates[i]));
                sb.Append("}");
            }
            sb.Append("],\"total\":").Append(templates.Count).Append("}");
            return sb.ToString();
        }

        // ─── Helpers ────────────────────────────────────────────────

        private static string GetProjectsXmlPath()
        {
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var paths = new[]
            {
                Path.Combine(docsFolder, "Studio 2024", "Projects", "projects.xml"),
                Path.Combine(docsFolder, "Studio 2022", "Projects", "projects.xml")
            };

            foreach (var p in paths)
                if (File.Exists(p)) return p;

            return null;
        }

        private static string ResolveProjectPath(string projectFilePath, string xmlPath)
        {
            if (string.IsNullOrEmpty(projectFilePath)) return null;
            if (Path.IsPathRooted(projectFilePath)) return projectFilePath;

            // Relative to the projects.xml folder
            var xmlDir = Path.GetDirectoryName(xmlPath);
            return xmlDir != null ? Path.Combine(xmlDir, projectFilePath) : projectFilePath;
        }

        private static string MapStatus(string status)
        {
            switch (status)
            {
                case "InProgress": return "Started";
                case "Completed": return "Completed";
                case "Archived": return "Archived";
                default: return status ?? "Unknown";
            }
        }

        private static string FormatDate(string isoDate)
        {
            if (string.IsNullOrEmpty(isoDate)) return "";
            if (DateTime.TryParse(isoDate, out var dt))
                return dt.ToString("d MMM yyyy");
            return isoDate;
        }

        private static string JsonStr(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder(value.Length + 8);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string JsonError(string message)
        {
            return "{\"error\":" + JsonStr(message) + "}";
        }

        /// <summary>
        /// Extracts a string field from a simple JSON object.
        /// Returns null if the field is not found.
        /// </summary>
        private static string ExtractJsonField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var pattern = $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"";
            var match = Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
