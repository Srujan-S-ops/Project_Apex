using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Apex.Net
{
    /// <summary>
    /// Starts the bundled Java engine if no backend answers. Uses the bundled runtime
    /// (Backend/jre) when present, otherwise `java` from PATH. The process is stopped on quit.
    /// </summary>
    public sealed class BackendLauncher : MonoBehaviour
    {
        public string Status { get; private set; } = "SEARCHING";
        private Process _process;

        public void Begin(BackendClient client)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-apexNoBackend") { Status = "DISABLED"; return; }
            StartCoroutine(Run(client));
        }

        private IEnumerator Run(BackendClient client)
        {
            float t = 0f;
            while (t < 2.5f && !client.IsOnline) { t += Time.unscaledDeltaTime; yield return null; }
            if (client.IsOnline) { Status = "EXTERNAL"; yield break; }

            string jar = FindJar();
            if (jar == null) { Status = "NOT FOUND"; Debug.LogWarning("[Launcher] apex-engine.jar not found; running offline."); yield break; }
            string java = FindJava(Path.GetDirectoryName(jar));
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = java,
                    Arguments = $"-Xmx384m -jar \"{jar}\"",
                    WorkingDirectory = Path.GetDirectoryName(jar),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _process = Process.Start(psi);
                Status = "STARTING";
                Debug.Log($"[Launcher] started backend: {java} -jar {jar}");
            }
            catch (Exception e)
            {
                Status = "NO JAVA";
                Debug.LogWarning("[Launcher] could not start Java backend: " + e.Message);
                yield break;
            }
            t = 0f;
            while (t < 40f && !client.IsOnline)
            {
                if (_process != null && _process.HasExited) { Status = "CRASHED"; yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Status = client.IsOnline ? "MANAGED" : "TIMEOUT";
        }

        private static string FindJar()
        {
            string data = Application.dataPath;
            string[] candidates =
            {
                Path.Combine(data, "..", "Backend", "apex-engine.jar"),                       // player build
                Path.Combine(data, "..", "..", "JavaEngine", "target", "apex-engine.jar"),    // editor
                Path.Combine(data, "..", "..", "Build", "Windows", "Backend", "apex-engine.jar")
            };
            foreach (var c in candidates)
            {
                string full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            return null;
        }

        private static string FindJava(string jarDir)
        {
            string bundled = Path.Combine(jarDir, "jre", "bin", "java.exe");
            if (File.Exists(bundled)) return bundled;
            string home = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(home))
            {
                string j = Path.Combine(home, "bin", "java.exe");
                if (File.Exists(j)) return j;
            }
            return "java";
        }

        private void OnApplicationQuit() => StopBackend();
        private void OnDestroy() => StopBackend();

        private void StopBackend()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Launcher] stop failed: " + e.Message); }
            _process = null;
        }
    }
}
