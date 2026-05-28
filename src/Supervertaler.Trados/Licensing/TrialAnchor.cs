using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Supervertaler.Trados.Licensing
{
    /// <summary>
    /// Tamper-resistant, location-independent anchor for the 14-day trial.
    ///
    /// The primary trial timestamp lives in <c>license.json</c> inside the
    /// shared user-data folder. That file is, however, trivially lost: moving
    /// the data folder, re-installing the plugin, or a corrupt/deleted
    /// <c>license.json</c> all make <see cref="LicenseInfo.Load"/> fall back to
    /// "start a fresh trial", silently handing the user another 14 days.
    ///
    /// This anchor closes that gap by mirroring two timestamps into the registry
    /// under <c>HKCU\Software\Supervertaler\Trados</c>, independent of the
    /// data-folder location and surviving both re-installs and deletion of
    /// <c>license.json</c>:
    ///
    ///   • <b>start</b>     – when the trial began. Can only ever move
    ///                        <em>earlier</em>, so a lost or hand-edited
    ///                        <c>license.json</c> cannot push it forward.
    ///   • <b>lastSeen</b>  – the latest wall-clock time the plugin has ever
    ///                        observed (a high-water mark). Expiry is measured
    ///                        against <c>max(now, lastSeen)</c>, so winding the
    ///                        system clock backwards cannot buy more trial time.
    ///
    /// The stored value is signed with an HMAC keyed on the machine fingerprint,
    /// so it cannot be hand-edited and still be trusted, and an anchor copied to
    /// another machine fails its signature check and is ignored.
    ///
    /// Per-user (HKCU) and fingerprint-bound by design: a genuinely new Windows
    /// account or a different machine legitimately starts a fresh trial. On any
    /// registry failure the methods degrade gracefully to the caller's inputs so
    /// the trial still works (just without the extra protection) and a legitimate
    /// user is never locked out by a registry hiccup.
    /// </summary>
    internal static class TrialAnchor
    {
        private const string SubKey = @"Software\Supervertaler\Trados";

        // Obfuscated value name – nondescript so it isn't obvious in regedit.
        private const string ValueName = "ts";

        // Static pepper folded into the HMAC key. Not a secret in the
        // cryptographic sense (it ships in the binary), but it raises the bar
        // for casual registry editing well beyond what a normal user will do.
        private static readonly byte[] Pepper =
            Encoding.UTF8.GetBytes("sv-trados-trial-anchor-v1");

        /// <summary>Result of <see cref="Reconcile"/>.</summary>
        internal struct Anchored
        {
            /// <summary>Authoritative trial start (earliest known).</summary>
            public DateTime Start;

            /// <summary>
            /// Effective "now" to measure expiry against: <c>max(realNow, lastSeen)</c>.
            /// Equal to the real clock for honest users; ahead of it only when the
            /// system clock has been wound backwards since a previous launch.
            /// </summary>
            public DateTime EffectiveNow;
        }

        /// <summary>
        /// Reconciles the caller's candidate trial start and the real clock with
        /// the persisted anchor, returning the authoritative start plus an
        /// effective "now" that is immune to a backwards system clock.
        ///
        /// • <paramref name="candidateStart"/> is the value the caller would
        ///   otherwise use (from <c>license.json</c>, or "now" for a brand-new
        ///   trial). The stored start moves to the earlier of the two.
        /// • <paramref name="realNow"/> is the current wall clock. The stored
        ///   high-water mark advances to the later of the two, and the returned
        ///   <see cref="Anchored.EffectiveNow"/> is never earlier than it.
        ///
        /// On any failure the caller's own inputs are echoed back unchanged.
        /// </summary>
        public static Anchored Reconcile(string fingerprint, DateTime candidateStart, DateTime realNow)
        {
            candidateStart = candidateStart.ToUniversalTime();
            realNow = realNow.ToUniversalTime();

            try
            {
                var rec = Read(fingerprint);

                var start = rec.HasValue && rec.Value.Start < candidateStart
                    ? rec.Value.Start
                    : candidateStart;

                // High-water mark: never let "now" appear earlier than the
                // furthest point the clock has ever reached.
                var lastSeen = rec.HasValue && rec.Value.Seen > realNow
                    ? rec.Value.Seen
                    : realNow;

                // Persist if anything changed (or on first write).
                if (!rec.HasValue || rec.Value.Start != start || rec.Value.Seen != lastSeen)
                    Write(fingerprint, start, lastSeen);

                return new Anchored { Start = start, EffectiveNow = lastSeen };
            }
            catch
            {
                // Registry unavailable or access denied – fall back to the
                // caller's inputs so the trial still works, just without the
                // extra anti-reset / anti-rollback protection.
                return new Anchored { Start = candidateStart, EffectiveNow = realNow };
            }
        }

        private struct Record
        {
            public DateTime Start;
            public DateTime Seen;
        }

        private static Record? Read(string fingerprint)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SubKey))
            {
                if (!(key?.GetValue(ValueName) is string raw) || string.IsNullOrEmpty(raw))
                    return null;

                // Format: "<payload>.<hmac>" where payload is "start" (legacy,
                // v4.20.34) or "start|seen" (current).
                var dot = raw.LastIndexOf('.');
                if (dot <= 0 || dot >= raw.Length - 1)
                    return null;

                var payload = raw.Substring(0, dot);
                var sig = raw.Substring(dot + 1);

                // Reject tampered values or anchors copied from another machine
                // (different fingerprint -> different HMAC key -> mismatch).
                if (!ConstantTimeEquals(sig, Sign(payload, fingerprint)))
                    return null;

                var parts = payload.Split('|');
                if (!TryParseUtc(parts[0], out var start))
                    return null;

                // Legacy single-value anchors had no high-water mark; seed it
                // from the start so older anchors keep working.
                var seen = parts.Length > 1 && TryParseUtc(parts[1], out var s) ? s : start;

                return new Record { Start = start, Seen = seen };
            }
        }

        private static void Write(string fingerprint, DateTime start, DateTime seen)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SubKey))
            {
                if (key == null) return;
                var payload =
                    start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "|" +
                    seen.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                key.SetValue(ValueName, payload + "." + Sign(payload, fingerprint),
                    RegistryValueKind.String);
            }
        }

        private static bool TryParseUtc(string s, out DateTime utc)
        {
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var ts))
            {
                utc = ts.ToUniversalTime();
                return true;
            }
            utc = default(DateTime);
            return false;
        }

        private static string Sign(string payload, string fingerprint)
        {
            using (var hmac = new HMACSHA256(DeriveKey(fingerprint)))
            {
                var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(mac.Length * 2);
                foreach (var b in mac)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static byte[] DeriveKey(string fingerprint)
        {
            using (var sha = SHA256.Create())
            {
                var fp = Encoding.UTF8.GetBytes(fingerprint ?? "");
                var buf = new byte[fp.Length + Pepper.Length];
                Buffer.BlockCopy(fp, 0, buf, 0, fp.Length);
                Buffer.BlockCopy(Pepper, 0, buf, fp.Length, Pepper.Length);
                return sha.ComputeHash(buf);
            }
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
