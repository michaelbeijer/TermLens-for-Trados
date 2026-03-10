using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Supervertaler.Trados.Models;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Represents a merge-candidate match found in the termbase.
    /// </summary>
    public class MergeMatch
    {
        /// <summary>Row ID of the existing term entry.</summary>
        public long TermId { get; set; }

        /// <summary>Existing entry's source term.</summary>
        public string SourceTerm { get; set; }

        /// <summary>Existing entry's target term.</summary>
        public string TargetTerm { get; set; }

        /// <summary>Termbase ID the match was found in.</summary>
        public long TermbaseId { get; set; }

        /// <summary>Display name of the termbase.</summary>
        public string TermbaseName { get; set; }

        /// <summary>"source" if the source term matched, "target" if the target term matched.</summary>
        public string MatchType { get; set; }
    }

    /// <summary>
    /// Checks whether a new source/target pair partially overlaps with an existing
    /// termbase entry (same source but different target, or same target but different source).
    /// Used to offer a "merge as synonym" prompt instead of creating near-duplicates.
    /// </summary>
    public static class TermMergeChecker
    {
        /// <summary>
        /// Finds existing entries that share the same source term (but different target)
        /// or the same target term (but different source) across the given write termbases.
        /// Returns an empty list when there are no merge candidates.
        /// </summary>
        public static List<MergeMatch> FindMergeMatches(
            string dbPath, string sourceTerm, string targetTerm,
            List<TermbaseInfo> termbases)
        {
            var matches = new List<MergeMatch>();

            if (string.IsNullOrWhiteSpace(dbPath) ||
                string.IsNullOrWhiteSpace(sourceTerm) ||
                string.IsNullOrWhiteSpace(targetTerm) ||
                termbases == null || termbases.Count == 0)
                return matches;

            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();

                foreach (var tb in termbases)
                {
                    const string sql = @"
                        SELECT id, source_term, target_term
                        FROM termbase_terms
                        WHERE CAST(termbase_id AS INTEGER) = @tbId
                          AND (
                            (LOWER(TRIM(source_term)) = LOWER(@source)
                             AND LOWER(TRIM(target_term)) != LOWER(@target))
                            OR
                            (LOWER(TRIM(target_term)) = LOWER(@target)
                             AND LOWER(TRIM(source_term)) != LOWER(@source))
                          )";

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@tbId", tb.Id);
                        cmd.Parameters.AddWithValue("@source", sourceTerm.Trim());
                        cmd.Parameters.AddWithValue("@target", targetTerm.Trim());

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var existingSource = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var existingTarget = reader.IsDBNull(2) ? "" : reader.GetString(2);

                                // Determine whether the source or target matched
                                bool sourceMatched = string.Equals(
                                    existingSource.Trim(), sourceTerm.Trim(),
                                    StringComparison.OrdinalIgnoreCase);

                                matches.Add(new MergeMatch
                                {
                                    TermId = reader.GetInt64(0),
                                    SourceTerm = existingSource,
                                    TargetTerm = existingTarget,
                                    TermbaseId = tb.Id,
                                    TermbaseName = tb.Name ?? "",
                                    MatchType = sourceMatched ? "source" : "target"
                                });
                            }
                        }
                    }
                }
            }

            return matches;
        }
    }
}
