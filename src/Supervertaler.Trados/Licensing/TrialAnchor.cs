using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Supervertaler.Trados.Licensing
{
    /// <summary>
    /// Tamper-resistant, location-independent record of when the 14-day trial
    /// first started on this machine + Windows user.
    ///
    /// The primary trial timestamp lives in <c>license.json</c> inside the
    /// shared user-data folder. That file is, however, trivially lost: moving
    /// the data folder, re-installing the plugin, or a corrupt/deleted
    /// <c>license.json</c> all make <see cref="LicenseInfo.Load"/> fall back to
    /// "start a fresh trial", silently handing the user another 14 days.
    ///
    /// This anchor closes that gap by mirroring the trial start into the
    /// registry under <c>HKCU\Software\Supervertaler\Trados</c>, which is
    /// independent of the data-folder location and survives both re-installs
    /// and deletion of <c>license.json</c>. The stored value is signed with an
    /// HMAC keyed on the machine fingerprint, so it cannot be hand-edited to a
    /// different date and still be trusted, and the clock can only ever move
    /// <em>earlier</em> — never reset to "now".
    ///
    /// Per-user (HKCU) and fingerprint-bound by design: a genuinely new Windows
    /// account or a different machine legitimately starts a fresh trial.
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

        /// <summary>
        /// Returns the authoritative trial-start timestamp for this machine,
        /// seeding the anchor on first use.
        ///
        /// If a valid anchor already exists, the <em>earlier</em> of
        /// (anchor, <paramref name="candidate"/>) is returned and persisted, so
        /// the clock can only move earlier. This defends against both a lost
        /// <c>license.json</c> (where the candidate is "now", i.e. later) and a
        /// hand-edited <c>license.json</c> carrying a future date.
        ///
        /// If no anchor exists yet, <paramref name="candidate"/> is written and
        /// returned. On any failure (registry unavailable, locked-down machine,
        /// etc.) the candidate is returned unchanged, so behaviour degrades
        /// gracefully to the pre-anchor logic.
        /// </summary>
        /// <param name="fingerprint">
        /// Live machine fingerprint from <see cref="MachineId.GetFingerprint"/>.
        /// Used as the HMAC key so an anchor copied to another machine fails its
        /// signature check and is ignored.
        /// </param>
        /// <param name="candidate">
        /// The trial start the caller would otherwise use (typically the value
        /// from <c>license.json</c>, or <see cref="DateTime.UtcNow"/> for a
        /// brand-new trial).
        /// </param>
        public static DateTime GetOrSeed(string fingerprint, DateTime candidate)
        {
            try
            {
                var existing = Read(fingerprint);
                if (existing.HasValue)
                {
                    var earliest = existing.Value < candidate ? existing.Value : candidate;
                    if (earliest != existing.Value)
                        Write(fingerprint, earliest);
                    return earliest;
                }

                Write(fingerprint, candidate);
                return candidate;
            }
            catch
            {
                // Registry unavailable or access denied – fall back to the
                // caller's candidate so the trial still works, just without the
                // extra anti-reset protection.
                return candidate;
            }
        }

        private static DateTime? Read(string fingerprint)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SubKey))
            {
                if (!(key?.GetValue(ValueName) is string raw) || string.IsNullOrEmpty(raw))
                    return null;

                var dot = raw.IndexOf('.');
                if (dot <= 0 || dot >= raw.Length - 1)
                    return null;

                var payload = raw.Substring(0, dot);
                var sig = raw.Substring(dot + 1);

                // Reject tampered values or anchors copied from another machine
                // (different fingerprint -> different HMAC key -> mismatch).
                if (!ConstantTimeEquals(sig, Sign(payload, fingerprint)))
                    return null;

                if (DateTime.TryParse(payload, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var ts))
                    return ts.ToUniversalTime();

                return null;
            }
        }

        private static void Write(string fingerprint, DateTime ts)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SubKey))
            {
                if (key == null) return;
                var payload = ts.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                key.SetValue(ValueName, payload + "." + Sign(payload, fingerprint),
                    RegistryValueKind.String);
            }
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
