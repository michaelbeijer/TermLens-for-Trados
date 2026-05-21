using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Client for the Workbench-side Supervertaler Bridge server (the Python
    /// half lives in <c>modules/supervertaler_bridge_server.py</c> in the
    /// Supervertaler-Workbench repo). Inverse of <see cref="SupervertalerBridge"/>:
    /// here we *push* a QuickLauncher prompt into the Workbench's Sidekick
    /// Chat instead of reading Trados context for the Workbench to consume.
    ///
    /// Discovery flow:
    ///   1. Resolve the shared user-data root from
    ///      <c>%APPDATA%\Supervertaler\config.json</c> (same pointer the
    ///      rest of the plugin uses).
    ///   2. Read the handshake file at
    ///      <c>&lt;root&gt;\workbench\runtime\sidekick-bridge.json</c> –
    ///      contains <c>{version, port, token, pid, startedAt}</c>.
    ///   3. POST to <c>http://127.0.0.1:&lt;port&gt;/v1/run-prompt</c> with
    ///      a Bearer token.
    ///
    /// All methods are synchronous with short timeouts so they're safe to
    /// call from the editor thread on a QuickLauncher click.
    /// </summary>
    internal static class WorkbenchBridgeClient
    {
        private const int HandshakeVersion = 1;
        private const int TimeoutMs = 5000;

        // Windows blocks SetForegroundWindow / activateWindow when called by
        // a background process (Workbench is in the background while Trados
        // is the active window). Calling AllowSetForegroundWindow from the
        // foreground process (Trados) grants the target process (Workbench)
        // permission to come to the front, just for this user-initiated
        // event. Without this the Sidekick window opens but stays hidden
        // behind Trados – the user has to Alt+K to find it.
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DataContract]
        private class Handshake
        {
            [DataMember(Name = "version")] public int Version { get; set; }
            [DataMember(Name = "port")] public int Port { get; set; }
            [DataMember(Name = "token")] public string Token { get; set; }
            [DataMember(Name = "pid")] public int Pid { get; set; }
            [DataMember(Name = "startedAt")] public string StartedAt { get; set; }
        }

        [DataContract]
        private class RunPromptRequest
        {
            [DataMember(Name = "prompt")] public string Prompt { get; set; }
            [DataMember(Name = "displayPrompt")] public string DisplayPrompt { get; set; }
            [DataMember(Name = "promptName")] public string PromptName { get; set; }
        }

        /// <summary>
        /// Sends a QuickLauncher prompt to the Workbench Sidekick. Returns
        /// (true, null) on success, (false, "<reason>") on any failure –
        /// no Sidekick running, handshake stale, network error, non-200
        /// response. Callers typically fall back to the in-Trados
        /// Assistant on failure with a status line citing the reason.
        /// </summary>
        public static (bool ok, string error) RunPrompt(string expandedPrompt,
            string displayPrompt, string promptName)
        {
            if (string.IsNullOrEmpty(expandedPrompt))
                return (false, "empty prompt");

            Handshake hs;
            try
            {
                hs = ReadHandshake();
            }
            catch (FileNotFoundException)
            {
                return (false, "Workbench Sidekick not running (no handshake file)");
            }
            catch (Exception ex)
            {
                return (false, "could not read handshake: " + ex.Message);
            }

            if (hs == null || hs.Version != HandshakeVersion || hs.Port <= 0
                || string.IsNullOrEmpty(hs.Token))
            {
                return (false, "handshake malformed or version mismatch");
            }

            if (!IsPidAlive(hs.Pid))
            {
                // Stale handshake from a hard kill.
                return (false, "Workbench appears to have exited");
            }

            // Grant Workbench permission to bring its Sidekick window to the
            // front. Best-effort – if the call fails, the prompt still gets
            // delivered, the user just has to summon Sidekick manually.
            try { AllowSetForegroundWindow(hs.Pid); } catch { /* ignore */ }

            try
            {
                var url = "http://127.0.0.1:" + hs.Port + "/v1/run-prompt";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = TimeoutMs;
                req.ReadWriteTimeout = TimeoutMs;
                req.Headers["Authorization"] = "Bearer " + hs.Token;

                var body = SerializeRequest(new RunPromptRequest
                {
                    Prompt = expandedPrompt,
                    DisplayPrompt = displayPrompt ?? expandedPrompt,
                    PromptName = promptName ?? ""
                });
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                req.ContentLength = bodyBytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bodyBytes, 0, bodyBytes.Length);

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    if ((int)resp.StatusCode == 200)
                        return (true, null);
                    return (false, "bridge returned HTTP " + (int)resp.StatusCode);
                }
            }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse httpResp)
                    return (false, "bridge returned HTTP " + (int)httpResp.StatusCode);
                return (false, "could not reach bridge: " + wex.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static Handshake ReadHandshake()
        {
            var path = Path.Combine(Settings.UserDataPath.Root,
                "workbench", "runtime", "sidekick-bridge.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("handshake not found", path);

            using (var stream = File.OpenRead(path))
            {
                var s = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                var ser = new DataContractJsonSerializer(typeof(Handshake), s);
                return (Handshake)ser.ReadObject(stream);
            }
        }

        private static string SerializeRequest(RunPromptRequest req)
        {
            using (var ms = new MemoryStream())
            {
                var s = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                var ser = new DataContractJsonSerializer(typeof(RunPromptRequest), s);
                ser.WriteObject(ms, req);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static bool IsPidAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(pid);
                return !proc.HasExited;
            }
            catch
            {
                // ArgumentException → no such pid; ditto for any odd state.
                return false;
            }
        }
    }
}
