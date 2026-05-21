using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Append-only log file for the Supervertaler Bridge, written to
    /// <c>UserDataPath.TradosRuntimeDir\bridge.log</c>. Visible diagnostics so
    /// users can tell whether the bridge actually started, what port it bound
    /// to, and what went wrong if it didn't. Truncated on every plugin start
    /// so the log doesn't grow without bound.
    /// </summary>
    internal static class BridgeLog
    {
        private static readonly object _lock = new object();
        private static bool _truncatedThisSession;

        // Fallback path: %TEMP%\Supervertaler-bridge.log. Used as a *second*
        // write target whenever we log, plus a *first* write target if the
        // primary UserDataPath resolution throws or the directory can't be
        // created. %TEMP% is always writable, so this guarantees we always
        // get diagnostic output somewhere even if UserDataPath is broken.
        private static string FallbackPath
        {
            get
            {
                try { return Path.Combine(Path.GetTempPath(), "Supervertaler-bridge.log"); }
                catch { return null; }
            }
        }

        public static void Write(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n";
            lock (_lock)
            {
                // First-write-of-session header, mirrored to both targets.
                string header = null;
                if (!_truncatedThisSession)
                {
                    header = $"--- Bridge session started at {DateTime.Now:O} " +
                             $"(PID {System.Diagnostics.Process.GetCurrentProcess().Id}) ---\r\n";
                    _truncatedThisSession = true;

                    // Try to log the resolved UserDataPath so we can see WHERE
                    // the plugin thinks the user data folder is.
                    try
                    {
                        header += $"UserDataPath.Root  = {UserDataPath.Root}\r\n";
                        header += $"TradosRuntimeDir   = {UserDataPath.TradosRuntimeDir}\r\n";
                        header += $"SupervertalerBridgeFile = {UserDataPath.SupervertalerBridgeFile}\r\n";
                    }
                    catch (Exception ex)
                    {
                        header += $"UserDataPath resolution THREW: {ex.GetType().Name}: {ex.Message}\r\n";
                    }
                }

                // Primary target: the user's Supervertaler data folder.
                try
                {
                    Directory.CreateDirectory(UserDataPath.TradosRuntimeDir);
                    var logPath = Path.Combine(UserDataPath.TradosRuntimeDir, "bridge.log");
                    if (header != null)
                        File.WriteAllText(logPath, header);
                    File.AppendAllText(logPath, line);
                }
                catch { /* primary write failed – fallback below will catch us */ }

                // Fallback target: %TEMP%\Supervertaler-bridge.log.
                try
                {
                    var fb = FallbackPath;
                    if (fb != null)
                    {
                        if (header != null)
                            File.WriteAllText(fb, header);
                        File.AppendAllText(fb, line);
                    }
                }
                catch { /* never let logging break the caller */ }
            }
        }
    }

    // ─── DataContract types for the bridge JSON wire format ──────────────────
    //
    // These mirror the in-Trados ChatContext shape the existing AI Assistant
    // already builds, but in a serialisation-friendly form. External clients
    // (notably Supervertaler Workbench's Sidekick Chat) consume these, so any
    // changes here are a wire-format change – bump the URL path version.

    [DataContract]
    public class BridgeContextSnapshot
    {
        [DataMember(Name = "available", Order = 0)]
        public bool Available { get; set; }

        [DataMember(Name = "project", Order = 1, EmitDefaultValue = false)]
        public BridgeProjectInfo Project { get; set; }

        [DataMember(Name = "activeSegment", Order = 2, EmitDefaultValue = false)]
        public BridgeSegmentInfo ActiveSegment { get; set; }

        [DataMember(Name = "surroundingSegments", Order = 3, EmitDefaultValue = false)]
        public List<BridgeSegmentInfo> SurroundingSegments { get; set; }

        [DataMember(Name = "tmMatches", Order = 4, EmitDefaultValue = false)]
        public List<BridgeTmMatch> TmMatches { get; set; }

        [DataMember(Name = "termbaseHits", Order = 5, EmitDefaultValue = false)]
        public List<BridgeTermbaseHit> TermbaseHits { get; set; }
    }

    [DataContract]
    public class BridgeProjectInfo
    {
        [DataMember(Name = "name", Order = 0, EmitDefaultValue = false)] public string Name { get; set; }
        [DataMember(Name = "fileName", Order = 1, EmitDefaultValue = false)] public string FileName { get; set; }
        [DataMember(Name = "sourceLang", Order = 2, EmitDefaultValue = false)] public string SourceLang { get; set; }
        [DataMember(Name = "targetLang", Order = 3, EmitDefaultValue = false)] public string TargetLang { get; set; }
    }

    [DataContract]
    public class BridgeSegmentInfo
    {
        [DataMember(Name = "source", Order = 0)] public string Source { get; set; }
        [DataMember(Name = "target", Order = 1, EmitDefaultValue = false)] public string Target { get; set; }
    }

    [DataContract]
    public class BridgeTmMatch
    {
        [DataMember(Name = "score", Order = 0)] public int Score { get; set; }
        [DataMember(Name = "source", Order = 1)] public string Source { get; set; }
        [DataMember(Name = "target", Order = 2)] public string Target { get; set; }
        [DataMember(Name = "tmName", Order = 3, EmitDefaultValue = false)] public string TmName { get; set; }
    }

    [DataContract]
    public class BridgeTermbaseHit
    {
        [DataMember(Name = "source", Order = 0)] public string Source { get; set; }
        [DataMember(Name = "target", Order = 1)] public string Target { get; set; }
        [DataMember(Name = "termbaseName", Order = 2, EmitDefaultValue = false)] public string TermbaseName { get; set; }
        [DataMember(Name = "definition", Order = 3, EmitDefaultValue = false)] public string Definition { get; set; }
        [DataMember(Name = "domain", Order = 4, EmitDefaultValue = false)] public string Domain { get; set; }
        [DataMember(Name = "notes", Order = 5, EmitDefaultValue = false)] public string Notes { get; set; }
    }

    [DataContract]
    internal class BridgeHandshake
    {
        [DataMember(Name = "version", Order = 0)] public int Version { get; set; }
        [DataMember(Name = "port", Order = 1)] public int Port { get; set; }
        [DataMember(Name = "token", Order = 2)] public string Token { get; set; }
        [DataMember(Name = "pid", Order = 3)] public int Pid { get; set; }
        [DataMember(Name = "startedAt", Order = 4)] public string StartedAt { get; set; }
    }

    [DataContract]
    internal class BridgeInsertRequest
    {
        [DataMember(Name = "text", IsRequired = true)] public string Text { get; set; }
    }

    [DataContract]
    internal class BridgeResultResponse
    {
        [DataMember(Name = "ok", Order = 0)] public bool Ok { get; set; }
        [DataMember(Name = "error", Order = 1, EmitDefaultValue = false)] public string Error { get; set; }
    }

    /// <summary>
    /// Localhost-only HTTP bridge that exposes the active Trados project context
    /// to external Supervertaler clients (currently: Workbench's Sidekick Chat).
    ///
    /// Lifecycle:
    ///   * Started by AiAssistantViewPart on plugin init when the user has
    ///     Assistant access (paid or trial) AND AiSettings.SidekickBridgeEnabled.
    ///   * Binds to <c>http://127.0.0.1:&lt;random-port&gt;/</c> – never accepts
    ///     non-loopback connections.
    ///   * Generates a fresh per-session auth token on Start; clients must
    ///     present it as <c>Authorization: Bearer &lt;token&gt;</c>.
    ///   * Writes a handshake file at <c>UserDataPath.SupervertalerBridgeFile</c>
    ///     with port + token + PID + timestamp so clients can discover it.
    ///     Deleted on Stop. Stale files from hard kills are detected by the
    ///     client checking PID liveness.
    ///
    /// Endpoints:
    ///   * <c>GET /v1/active-context</c> – returns a BridgeContextSnapshot
    ///     describing the current Trados document state (active segment,
    ///     surrounding segments, TM matches, termbase hits, project metadata).
    ///   * <c>POST /v1/insert-translation</c> – inserts text into the active
    ///     target segment via the same path the in-Trados Apply-To-Target
    ///     button uses.
    ///
    /// Threading:
    ///   * Listener runs on a dedicated background thread; one request at a
    ///     time (Trados editor operations are not concurrency-safe).
    ///   * Both endpoint handlers marshal back to the UI thread via the
    ///     supplied delegates – callers MUST be safe to invoke from any
    ///     thread; the bridge itself does not synchronise with WinForms.
    /// </summary>
    public sealed class SupervertalerBridge : IDisposable
    {
        private const int HandshakeVersion = 1;

        private readonly Func<BridgeContextSnapshot> _getContext;
        private readonly Func<string, string> _insertText; // returns null on success, error message otherwise

        private HttpListener _listener;
        private Thread _listenerThread;
        private CancellationTokenSource _cts;
        private string _token;
        private int _port;
        private bool _disposed;

        public SupervertalerBridge(
            Func<BridgeContextSnapshot> getContext,
            Func<string, string> insertText)
        {
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _insertText = insertText ?? throw new ArgumentNullException(nameof(insertText));
        }

        public bool IsRunning => _listener != null && _listener.IsListening;
        public int Port => _port;

        /// <summary>
        /// Start the listener. Returns silently on failure (logged to Debug)
        /// rather than throwing – the bridge is a non-essential feature and
        /// must never break the rest of the plugin.
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                BridgeLog.Write("Start() called but bridge already running – no-op");
                return;
            }

            BridgeLog.Write("Start() entered");
            _token = Guid.NewGuid().ToString("N");

            // HttpListener doesn't accept "port 0 = OS-pick" so we try a
            // handful of random high ports until one is free.
            var rng = new Random();
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int candidate = rng.Next(49152, 65535);
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{candidate}/");
                    listener.Start();
                    _listener = listener;
                    _port = candidate;
                    BridgeLog.Write($"HttpListener bound on port {candidate} (attempt {attempt + 1})");
                    break;
                }
                catch (HttpListenerException ex)
                {
                    BridgeLog.Write($"port {candidate} bind failed: HttpListenerException code={ex.ErrorCode} message=\"{ex.Message}\"");
                }
                catch (Exception ex)
                {
                    BridgeLog.Write($"port {candidate} bind failed: {ex.GetType().Name} message=\"{ex.Message}\"");
                }
            }

            if (_listener == null)
            {
                BridgeLog.Write("FAILED: no free port could be bound after 16 attempts. " +
                    "On Windows, HttpListener may need URL ACL registration for non-admin processes – see " +
                    "`netsh http show urlacl`. Bridge disabled this session.");
                return;
            }

            _cts = new CancellationTokenSource();
            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "SupervertalerBridge"
            };
            _listenerThread.Start();
            BridgeLog.Write("listener thread started");

            try
            {
                WriteHandshakeFile();
                BridgeLog.Write($"handshake file written at {UserDataPath.SupervertalerBridgeFile}");
            }
            catch (Exception ex)
            {
                BridgeLog.Write($"FAILED to write handshake file: {ex.GetType().Name}: {ex.Message}");
                // Bridge is still usable, just not discoverable – not fatal.
            }

            BridgeLog.Write($"Start() complete. Bridge live on http://127.0.0.1:{_port}/ with token {_token.Substring(0, 8)}…");
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _listener?.Stop(); } catch { /* ignore */ }
            try { _listener?.Close(); } catch { /* ignore */ }
            _listener = null;

            try
            {
                if (File.Exists(UserDataPath.SupervertalerBridgeFile))
                    File.Delete(UserDataPath.SupervertalerBridgeFile);
            }
            catch (Exception ex)
            {
                BridgeLog.Write($"[SupervertalerBridge] failed to delete handshake file: {ex.Message}");
            }

            // Don't Join the thread – HttpListener.Stop unblocks GetContext
            // but the thread cleanup is best-effort. It's a background thread
            // and will die with the process anyway.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts?.Dispose();
        }

        // ── Listener loop ────────────────────────────────────────────────

        private void ListenLoop()
        {
            while (_listener != null && _listener.IsListening && !_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    // Listener.Stop() unblocks with this exception – clean shutdown
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    BridgeLog.Write($"[SupervertalerBridge] GetContext failed: {ex.Message}");
                    return;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    BridgeLog.Write($"[SupervertalerBridge] HandleRequest threw: {ex.Message}");
                    TryWriteError(context, 500, "internal error");
                }
                finally
                {
                    try { context.Response.Close(); } catch { /* ignore */ }
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Defence in depth: HttpListener already binds to 127.0.0.1 so
            // remote requests can't reach us, but we double-check the
            // remote address for paranoia (and to fail loud if the binding
            // ever drifts).
            if (request.RemoteEndPoint == null
                || !IPAddress.IsLoopback(request.RemoteEndPoint.Address))
            {
                TryWriteError(context, 403, "loopback only");
                return;
            }

            // Bearer token auth
            var authHeader = request.Headers["Authorization"] ?? "";
            const string prefix = "Bearer ";
            if (!authHeader.StartsWith(prefix, StringComparison.Ordinal)
                || authHeader.Substring(prefix.Length) != _token)
            {
                TryWriteError(context, 401, "unauthorized");
                return;
            }

            var path = request.Url.AbsolutePath;
            var method = request.HttpMethod;

            if (method == "GET" && path == "/v1/active-context")
            {
                HandleGetActiveContext(context);
                return;
            }

            if (method == "POST" && path == "/v1/insert-translation")
            {
                HandleInsertTranslation(context);
                return;
            }

            TryWriteError(context, 404, "not found");
        }

        private void HandleGetActiveContext(HttpListenerContext context)
        {
            BridgeContextSnapshot snapshot;
            try
            {
                snapshot = _getContext() ?? new BridgeContextSnapshot { Available = false };
            }
            catch (Exception ex)
            {
                BridgeLog.Write($"[SupervertalerBridge] context provider threw: {ex.Message}");
                snapshot = new BridgeContextSnapshot { Available = false };
            }

            WriteJson(context, 200, snapshot);
        }

        private void HandleInsertTranslation(HttpListenerContext context)
        {
            BridgeInsertRequest req;
            try
            {
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        WriteJson(context, 400, new BridgeResultResponse { Ok = false, Error = "empty body" });
                        return;
                    }
                    req = DeserializeJson<BridgeInsertRequest>(body);
                }
            }
            catch (Exception ex)
            {
                WriteJson(context, 400, new BridgeResultResponse { Ok = false, Error = "malformed body: " + ex.Message });
                return;
            }

            if (req == null || string.IsNullOrEmpty(req.Text))
            {
                WriteJson(context, 400, new BridgeResultResponse { Ok = false, Error = "missing 'text'" });
                return;
            }

            string err;
            try
            {
                err = _insertText(req.Text); // null on success
            }
            catch (Exception ex)
            {
                err = "insert failed: " + ex.Message;
            }

            if (err == null)
                WriteJson(context, 200, new BridgeResultResponse { Ok = true });
            else
                WriteJson(context, 409, new BridgeResultResponse { Ok = false, Error = err });
        }

        // ── Handshake file ───────────────────────────────────────────────

        private void WriteHandshakeFile()
        {
            Directory.CreateDirectory(UserDataPath.TradosRuntimeDir);

            var handshake = new BridgeHandshake
            {
                Version = HandshakeVersion,
                Port = _port,
                Token = _token,
                Pid = Process.GetCurrentProcess().Id,
                StartedAt = DateTime.UtcNow.ToString("o")
            };

            var bytes = SerializeJson(handshake);
            File.WriteAllBytes(UserDataPath.SupervertalerBridgeFile, bytes);
        }

        // ── JSON helpers ─────────────────────────────────────────────────

        private static void WriteJson<T>(HttpListenerContext context, int statusCode, T payload)
        {
            try
            {
                var bytes = SerializeJson(payload);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                BridgeLog.Write($"[SupervertalerBridge] WriteJson failed: {ex.Message}");
            }
        }

        private static void TryWriteError(HttpListenerContext context, int statusCode, string message)
        {
            try
            {
                WriteJson(context, statusCode, new BridgeResultResponse { Ok = false, Error = message });
            }
            catch { /* nothing more we can do */ }
        }

        private static byte[] SerializeJson<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return ms.ToArray();
            }
        }

        private static T DeserializeJson<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(ms);
            }
        }
    }
}
