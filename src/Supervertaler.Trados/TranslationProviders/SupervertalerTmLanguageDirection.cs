using System;
using System.Collections.Generic;
using Sdl.Core.Globalization;
using Sdl.LanguagePlatform.Core;
using Sdl.LanguagePlatform.TranslationMemory;
using Sdl.LanguagePlatform.TranslationMemoryApi;
using Supervertaler.Trados.Core;

namespace Supervertaler.Trados.TranslationProviders
{
    /// <summary>
    /// Per-language-pair search engine for <see cref="SupervertalerTmProvider"/>.
    ///
    /// Two real entry points in v1:
    ///   * <see cref="SearchSegment"/> – exact source-text lookup, returns
    ///     100% matches only. Studio's TM-results pane shows these as plain
    ///     "100%" hits alongside any other providers attached to the project.
    ///   * <see cref="SearchText"/> – concordance lookup, returns up to
    ///     <see cref="MaxConcordanceResults"/> rows where the source text
    ///     contains the query substring. Source-side by default; Studio's
    ///     Concordance window calls this for both directions and uses the
    ///     <see cref="SearchSettings.Mode"/> field to indicate which.
    ///
    /// Every Add/Update/Delete method on the interface throws
    /// <see cref="NotSupportedException"/> because v1 is read-only
    /// (<see cref="ITranslationProvider.IsReadOnly"/> = true). Phase 3 of
    /// the Shared TM work will implement write-back; until then any caller
    /// that ignores <c>IsReadOnly</c> and tries to write should fail loudly.
    /// </summary>
    public class SupervertalerTmLanguageDirection : ITranslationProviderLanguageDirection
    {
        // Concordance search caps. Trados' concordance window already paginates,
        // so we don't need to return everything – capping at 30 keeps the
        // SQL fast and the round-trip light.
        private const int MaxConcordanceResults = 30;

        // Exact-match cap. Multiple TUs CAN share an identical source text
        // (different translations of the same string). We want all of them
        // surfaced; 10 is generous and avoids accidental denial-of-service
        // on a pathological TM.
        private const int MaxExactResults = 10;

        private readonly SupervertalerTmProvider _provider;
        private readonly LanguagePair _languagePair;
        private readonly TmInfo _tmInfo;
        private readonly string _dbPath;

        internal SupervertalerTmLanguageDirection(
            SupervertalerTmProvider provider,
            LanguagePair languagePair,
            TmInfo tmInfo,
            string dbPath)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _languagePair = languagePair ?? throw new ArgumentNullException(nameof(languagePair));
            _tmInfo = tmInfo;
            _dbPath = dbPath;
            try
            {
                TmBridgeLog.Info(
                    "LanguageDirection ctor: TM=" + (tmInfo != null ? tmInfo.Name : "(null)") +
                    ", langPair=" + (languagePair.SourceCulture.Name ?? "(empty)") +
                    "->" + (languagePair.TargetCulture.Name ?? "(empty)"));
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("LanguageDirection ctor: log line threw (continuing anyway)", ex);
            }
        }

        // ─── Identity ─────────────────────────────────────────────────

        public ITranslationProvider TranslationProvider
        {
            get
            {
                TmBridgeLog.Info("LanguageDirection.TranslationProvider get => " + (_provider == null ? "(null!)" : "OK"));
                return _provider;
            }
        }
        public CultureCode SourceLanguage
        {
            get
            {
                try
                {
                    var c = _languagePair.SourceCulture;
                    TmBridgeLog.Info("LanguageDirection.SourceLanguage get => " + (c.Name ?? "(empty)"));
                    return c;
                }
                catch (Exception ex)
                {
                    TmBridgeLog.Error("LanguageDirection.SourceLanguage get: threw", ex);
                    throw;
                }
            }
        }
        public CultureCode TargetLanguage
        {
            get
            {
                try
                {
                    var c = _languagePair.TargetCulture;
                    TmBridgeLog.Info("LanguageDirection.TargetLanguage get => " + (c.Name ?? "(empty)"));
                    return c;
                }
                catch (Exception ex)
                {
                    TmBridgeLog.Error("LanguageDirection.TargetLanguage get: threw", ex);
                    throw;
                }
            }
        }
        public bool CanReverseLanguageDirection
        {
            get
            {
                TmBridgeLog.Info("LanguageDirection.CanReverseLanguageDirection => false");
                return false;
            }
        }

        // ─── Search: exact (SearchSegment) ────────────────────────────

        public SearchResults SearchSegment(SearchSettings settings, Segment segment)
        {
            SafeLog("SearchSegment ENTRY");
            // Defensive: Studio occasionally passes null segments in
            // exploratory pre-flight calls. Returning an empty SearchResults
            // is the documented correct behaviour – throwing here causes
            // Trados to surface "An error has occurred while using the
            // translation provider" and disable the provider for the
            // session.
            //
            // v4.20.31 root-cause fix: SearchResults.SourceSegment MUST be
            // non-null on every code path. Trados' internal
            // SearchResultsMerged.CopyFromSearchResults does
            // `base.SourceSegment = other.SourceSegment.Duplicate();` with
            // no null guard, so handing it a SearchResults with a default
            // (null) SourceSegment throws NullReferenceException deep
            // inside Cascade.MergeSearchResults – the cause of every
            // "Object reference not set to an instance of an object" we
            // saw in v4.20.26 through v4.20.30. The fix: NewSearchResults
            // always pre-populates SourceSegment with a real Segment.
            var results = NewSearchResults(segment);

            if (_tmInfo == null)
            {
                TmBridgeLog.Warn("SearchSegment called on provider with no TmInfo (TM no longer bridged?)");
                return results;
            }
            if (segment == null)
            {
                TmBridgeLog.Warn("SearchSegment called with null segment");
                return results;
            }

            string queryText;
            try
            {
                queryText = segment.ToPlain() ?? string.Empty;
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("SearchSegment: segment.ToPlain() threw", ex);
                return results;
            }
            if (string.IsNullOrEmpty(queryText)) return results;

            try
            {
                using (var reader = new TmReader(_dbPath))
                {
                    if (!reader.Open())
                    {
                        TmBridgeLog.Warn("SearchSegment: TmReader.Open() failed: " + (reader.LastError ?? "(no message)"));
                        return results;
                    }

                    var matches = reader.SearchExact(_tmInfo.TmId, queryText, MaxExactResults);
                    foreach (var m in matches)
                    {
                        var sr = TryBuildSearchResult(m);
                        if (sr != null) results.Add(sr);
                    }
                }
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error(
                    "SearchSegment: lookup against TM '" + _tmInfo.TmId +
                    "' failed for query '" + Truncate(queryText, 80) + "'", ex);
            }

            return results;
        }

        public SearchResults[] SearchSegments(SearchSettings settings, Segment[] segments)
        {
            TmBridgeLog.Info("LanguageDirection.SearchSegments ENTRY (n=" + (segments == null ? 0 : segments.Length) + ")");
            if (segments == null) return new SearchResults[0];
            var output = new SearchResults[segments.Length];
            for (int i = 0; i < segments.Length; i++)
                output[i] = SearchSegment(settings, segments[i]);
            return output;
        }

        public SearchResults[] SearchSegmentsMasked(SearchSettings settings, Segment[] segments, bool[] mask)
        {
            SafeLog("LanguageDirection.SearchSegmentsMasked ENTRY (n=" + (segments == null ? 0 : segments.Length) + ", mask=" + (mask == null ? "(null)" : mask.Length.ToString()) + ")");
            if (segments == null) return new SearchResults[0];
            var output = new SearchResults[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                if (mask != null && i < mask.Length && !mask[i])
                {
                    // Even masked-out slots must have a non-null SourceSegment
                    // so Cascade's merger doesn't NRE on them.
                    output[i] = NewSearchResults(segments[i]);
                    continue;
                }
                output[i] = SearchSegment(settings, segments[i]);
            }
            return output;
        }

        // ─── Search: concordance (SearchText) ─────────────────────────

        public SearchResults SearchText(SearchSettings settings, string segment)
        {
            SafeLog("LanguageDirection.SearchText ENTRY (len=" + (segment == null ? -1 : segment.Length) + ")");
            // v4.20.31: always populate SourceSegment so Cascade's merger
            // (which dereferences it unguarded) never NREs on our return.
            var results = NewSearchResultsFromText(segment);
            if (_tmInfo == null || string.IsNullOrEmpty(segment)) return results;

            // Direction comes from settings.Mode – source-side or target-side
            // concordance. Default to source if Studio passes something we
            // don't recognise.
            var searchTarget = settings != null
                && settings.Mode == SearchMode.ConcordanceSearch
                && false; // SearchMode doesn't distinguish src/tgt at this level
            // Studio actually issues two separate concordance calls (one with
            // each direction); we cover both by also looking at TargetConcordance
            // in higher-level callers. For SearchText specifically, the convention
            // is that the caller has already picked the direction it wants, so
            // we run the source-side search here and let SearchTranslationUnit
            // handle the target variant.

            try
            {
                using (var reader = new TmReader(_dbPath))
                {
                    if (!reader.Open())
                    {
                        TmBridgeLog.Warn("SearchText: TmReader.Open() failed: " + (reader.LastError ?? "(no message)"));
                        return results;
                    }
                    var matches = reader.SearchConcordance(
                        _tmInfo.TmId, segment, searchTarget: false, MaxConcordanceResults);
                    foreach (var m in matches)
                    {
                        var sr = TryBuildSearchResult(m);
                        if (sr != null) results.Add(sr);
                    }
                }
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("SearchText: failed for query '" + Truncate(segment, 80) + "'", ex);
            }

            return results;
        }

        public SearchResults SearchTranslationUnit(SearchSettings settings, TranslationUnit translationUnit)
        {
            SafeLog("LanguageDirection.SearchTranslationUnit ENTRY");
            // Studio uses this for target-side concordance when the user
            // searches for text that should appear in the *target* of an
            // existing TU. Decide which side to search by checking which
            // segment the caller populated.
            //
            // v4.20.31: SourceSegment on the returned SearchResults must
            // never be null – seed it from the TU's source segment.
            var results = NewSearchResults(translationUnit?.SourceSegment);
            if (_tmInfo == null || translationUnit == null) return results;

            string query = null;
            bool searchTarget = false;
            if (translationUnit.TargetSegment != null && !translationUnit.TargetSegment.IsEmpty)
            {
                query = translationUnit.TargetSegment.ToPlain();
                searchTarget = true;
            }
            else if (translationUnit.SourceSegment != null && !translationUnit.SourceSegment.IsEmpty)
            {
                query = translationUnit.SourceSegment.ToPlain();
                searchTarget = false;
            }
            if (string.IsNullOrEmpty(query)) return results;

            try
            {
                using (var reader = new TmReader(_dbPath))
                {
                    if (!reader.Open())
                    {
                        TmBridgeLog.Warn("SearchTranslationUnit: TmReader.Open() failed: " + (reader.LastError ?? "(no message)"));
                        return results;
                    }
                    var matches = reader.SearchConcordance(
                        _tmInfo.TmId, query, searchTarget, MaxConcordanceResults);
                    foreach (var m in matches)
                    {
                        var sr = TryBuildSearchResult(m);
                        if (sr != null) results.Add(sr);
                    }
                }
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("SearchTranslationUnit: failed for query '" + Truncate(query, 80) + "'", ex);
            }

            return results;
        }

        public SearchResults[] SearchTranslationUnits(SearchSettings settings, TranslationUnit[] translationUnits)
        {
            SafeLog("LanguageDirection.SearchTranslationUnits ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            if (translationUnits == null) return new SearchResults[0];
            var output = new SearchResults[translationUnits.Length];
            for (int i = 0; i < translationUnits.Length; i++)
                output[i] = SearchTranslationUnit(settings, translationUnits[i]);
            return output;
        }

        public SearchResults[] SearchTranslationUnitsMasked(SearchSettings settings, TranslationUnit[] translationUnits, bool[] mask)
        {
            SafeLog("LanguageDirection.SearchTranslationUnitsMasked ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ", mask=" + (mask == null ? "(null)" : mask.Length.ToString()) + ")");
            if (translationUnits == null) return new SearchResults[0];
            var output = new SearchResults[translationUnits.Length];
            for (int i = 0; i < translationUnits.Length; i++)
            {
                if (mask != null && i < mask.Length && !mask[i])
                {
                    // Masked-out slots: still must have a non-null
                    // SourceSegment so Cascade's merger doesn't NRE.
                    output[i] = NewSearchResults(translationUnits[i]?.SourceSegment);
                    continue;
                }
                output[i] = SearchTranslationUnit(settings, translationUnits[i]);
            }
            return output;
        }

        // ─── Write API (Phase 3 – currently all throw) ────────────────

        // v4.20.27: write methods used to throw NotSupportedException to
        // make any consumer that ignored IsReadOnly = true fail loudly,
        // but Trados Studio's batch-tasks pipeline (notably "Update Main
        // Translation Memories") calls these speculatively even when
        // IsReadOnly is reported true – and the thrown exception bubbles
        // up to the user as a generic "provider error". Returning a safe
        // empty result instead is the documented well-behaved pattern.
        public ImportResult AddTranslationUnit(TranslationUnit translationUnit, ImportSettings settings)
        {
            TmBridgeLog.Info("LanguageDirection.AddTranslationUnit ENTRY");
            return SafeNotSupportedResult();
        }

        public ImportResult[] AddTranslationUnits(TranslationUnit[] translationUnits, ImportSettings settings)
        {
            TmBridgeLog.Info("LanguageDirection.AddTranslationUnits ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            return SafeNotSupportedResults(translationUnits);
        }

        public ImportResult[] AddOrUpdateTranslationUnits(TranslationUnit[] translationUnits, int[] previousTranslationHashes, ImportSettings settings)
        {
            TmBridgeLog.Info("LanguageDirection.AddOrUpdateTranslationUnits ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            return SafeNotSupportedResults(translationUnits);
        }

        public ImportResult[] AddTranslationUnitsMasked(TranslationUnit[] translationUnits, ImportSettings settings, bool[] mask)
        {
            TmBridgeLog.Info("LanguageDirection.AddTranslationUnitsMasked ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            return SafeNotSupportedResults(translationUnits);
        }

        public ImportResult[] AddOrUpdateTranslationUnitsMasked(TranslationUnit[] translationUnits, int[] previousTranslationHashes, ImportSettings settings, bool[] mask)
        {
            TmBridgeLog.Info("LanguageDirection.AddOrUpdateTranslationUnitsMasked ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            return SafeNotSupportedResults(translationUnits);
        }

        public ImportResult UpdateTranslationUnit(TranslationUnit translationUnit)
        {
            TmBridgeLog.Info("LanguageDirection.UpdateTranslationUnit ENTRY");
            return SafeNotSupportedResult();
        }

        public ImportResult[] UpdateTranslationUnits(TranslationUnit[] translationUnits)
        {
            TmBridgeLog.Info("LanguageDirection.UpdateTranslationUnits ENTRY (n=" + (translationUnits == null ? 0 : translationUnits.Length) + ")");
            return SafeNotSupportedResults(translationUnits);
        }

        private static ImportResult SafeNotSupportedResult()
        {
            // ImportResult has no explicit "not supported" status; an empty
            // (default-constructed) one signals "no rows applied" without
            // throwing. Trados treats it as a no-op.
            return new ImportResult();
        }

        private static ImportResult[] SafeNotSupportedResults(TranslationUnit[] tus)
        {
            var len = tus != null ? tus.Length : 0;
            var arr = new ImportResult[len];
            for (int i = 0; i < len; i++) arr[i] = new ImportResult();
            return arr;
        }

        // ─── Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Constructs a <see cref="SearchResults"/> whose <c>SourceSegment</c>
        /// is GUARANTEED non-null. Trados' Cascade merger
        /// (<c>SearchResultsMerged.CopyFromSearchResults</c>) calls
        /// <c>other.SourceSegment.Duplicate()</c> unguarded – returning a
        /// SearchResults with a default (null) SourceSegment is what produced
        /// the "Object reference not set to an instance of an object" NRE
        /// seen in v4.20.26 through v4.20.30.
        ///
        /// When the input segment is available we duplicate it so context is
        /// preserved; otherwise we fall back to a minimal empty Segment
        /// stamped with the right SourceLanguage. The duplicate itself is
        /// also try/catched – Trados' Segment.Duplicate has been observed to
        /// throw under odd conditions, and a failed duplicate must not let
        /// us return a null SourceSegment.
        /// </summary>
        private SearchResults NewSearchResults(Segment seedSegment)
        {
            var results = new SearchResults();
            try
            {
                if (seedSegment != null)
                {
                    var dup = seedSegment.Duplicate();
                    results.SourceSegment = dup ?? new Segment(SafeSourceCulture());
                }
                else
                {
                    results.SourceSegment = new Segment(SafeSourceCulture());
                }
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("NewSearchResults: Duplicate threw, falling back to empty Segment", ex);
                try { results.SourceSegment = new Segment(SafeSourceCulture()); }
                catch { /* truly impossible – Segment(CultureCode) doesn't throw */ }
            }
            return results;
        }

        /// <summary>
        /// Text-input variant. Wraps the query text in a Segment so Cascade's
        /// merger has something non-null to duplicate.
        /// </summary>
        private SearchResults NewSearchResultsFromText(string text)
        {
            var results = new SearchResults();
            try
            {
                var seg = new Segment(SafeSourceCulture());
                if (!string.IsNullOrEmpty(text)) seg.Add(text);
                results.SourceSegment = seg;
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("NewSearchResultsFromText: Segment ctor threw", ex);
            }
            return results;
        }

        /// <summary>
        /// Returns the LD's source culture, or the project's source culture
        /// as a last-ditch fallback. Used only when we have to manufacture a
        /// Segment from scratch.
        /// </summary>
        private CultureCode SafeSourceCulture()
        {
            try { return _languagePair.SourceCulture; }
            catch { return new CultureCode("en"); }
        }

        /// <summary>
        /// Log helper that NEVER throws – string concatenation arguments are
        /// stringified inside the try/catch so a misbehaving Segment.ToPlain
        /// can't take down the method that's logging. Previously, the entry
        /// log line in <see cref="SearchSegment"/> evaluated
        /// <c>segment.ToPlain()</c> eagerly; if that threw, the log line
        /// never wrote AND the method exited abnormally – explaining why
        /// v4.20.28/4.20.30 showed Cascade merging null SourceSegments
        /// without our entry log appearing.
        /// </summary>
        private static void SafeLog(string message)
        {
            try { TmBridgeLog.Info(message); }
            catch { /* logging must never throw */ }
        }

        /// <summary>
        /// Wraps <see cref="BuildTranslationUnit"/> + <see cref="SearchResult"/>
        /// construction in a try/catch that logs the failure and returns null.
        /// Callers add to the SearchResults list only when this returns non-null,
        /// so one bad row never poisons a whole result batch.
        /// </summary>
        private Sdl.LanguagePlatform.TranslationMemory.SearchResult TryBuildSearchResult(BridgedTu m)
        {
            try
            {
                var tu = BuildTranslationUnit(m);
                if (tu == null) return null;
                // Fully-qualify SearchResult: Supervertaler.Trados.Core has
                // its own SearchResult that would otherwise win the
                // unqualified-name lookup. Match is read-only on
                // ScoringResult – computed from BaseScore minus penalties,
                // so setting BaseScore = 100 with no penalties yields 100%.
                return new Sdl.LanguagePlatform.TranslationMemory.SearchResult(tu)
                {
                    ScoringResult = new ScoringResult { BaseScore = 100 },
                };
            }
            catch (Exception ex)
            {
                TmBridgeLog.Error("TryBuildSearchResult: failed for TU id=" + m.Id, ex);
                return null;
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        /// <summary>
        /// Builds a Trados <see cref="TranslationUnit"/> from a raw row
        /// pulled out of Supervertaler's <c>translation_units</c> table.
        /// We only carry over the bare minimum that Studio's UI uses:
        /// source text, target text, system fields (creator + dates). No
        /// custom field values, no contexts – those live on Trados-native
        /// TMs and aren't represented in Workbench's schema.
        /// </summary>
        private TranslationUnit BuildTranslationUnit(BridgedTu m)
        {
            // CultureCode is a value type – it's not nullable, but it CAN
            // be the default/empty value. Trados accepts that; the Segment
            // ctor just stores it. We log if we hit an empty culture so we
            // know our SupportsLanguageDirection plumbing is off.
            if (SourceLanguage.Name == null || TargetLanguage.Name == null)
            {
                TmBridgeLog.Warn(
                    "BuildTranslationUnit: empty culture on language pair " +
                    "(src=" + SourceLanguage.Name + ", tgt=" + TargetLanguage.Name + ")");
            }

            var src = new Segment(SourceLanguage);
            src.Add(m.SourceText ?? string.Empty);

            var tgt = new Segment(TargetLanguage);
            tgt.Add(m.TargetText ?? string.Empty);

            var tu = new TranslationUnit(src, tgt)
            {
                Origin = TranslationUnitOrigin.TM,
                OriginSystem = "Supervertaler",
            };

            // Best-effort system field population – Workbench stores dates
            // as ISO strings; Trados expects DateTime. Parse permissively.
            try
            {
                if (tu.SystemFields != null)
                {
                    if (!string.IsNullOrEmpty(m.CreatedBy))
                        tu.SystemFields.CreationUser = m.CreatedBy;
                    DateTime parsed;
                    if (!string.IsNullOrEmpty(m.CreatedDate) &&
                        DateTime.TryParse(m.CreatedDate, out parsed))
                        tu.SystemFields.CreationDate = parsed;
                    if (!string.IsNullOrEmpty(m.ModifiedDate) &&
                        DateTime.TryParse(m.ModifiedDate, out parsed))
                        tu.SystemFields.ChangeDate = parsed;
                    tu.SystemFields.UseCount = (int)Math.Min(int.MaxValue, m.UsageCount);
                }
            }
            catch
            {
                // SystemFields can be picky about its setters across Trados
                // versions; missing metadata is cosmetic, never fatal.
            }

            return tu;
        }
    }
}
