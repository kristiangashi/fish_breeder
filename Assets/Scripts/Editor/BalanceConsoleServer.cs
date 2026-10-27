using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using FishBreeder.Core;
using UnityEditor;
using UnityEngine;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Serves the balance console (Tools/BalanceConsole/index.html) at http://localhost:8642 while
    /// the Unity editor is open. Saving in the console writes Assets/Resources/Balance/balance.json
    /// (which ships with the game) and, if the game is playing, applies the values immediately.
    /// Editor-only: neither this server nor the web page is part of a build.
    /// Open it from the menu: Fish Breeder > Balance Console.
    /// </summary>
    [InitializeOnLoad]
    static class BalanceConsoleServer
    {
        const int Port = 8642;
        static readonly string Url = $"http://localhost:{Port}/";
        static readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        static HttpListener listener;
        static Thread thread;
        static string projectRoot;
        static string defaultJson;
        static volatile bool playing;

        static BalanceConsoleServer()
        {
            projectRoot = Path.GetDirectoryName(Application.dataPath);
            defaultJson = Balance.DefaultJson();
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            Start();
        }

        [MenuItem("Fish Breeder/Balance Console")]
        static void OpenConsole()
        {
            Start();
            Application.OpenURL(Url);
        }

        static void Start()
        {
            if (listener != null && listener.IsListening) return;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(Url);
                listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                listener.Start();
                thread = new Thread(Listen) { IsBackground = true, Name = "Balance console" };
                thread.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FishBreeder] Balance console could not start on port {Port}: {e.Message}");
                listener = null;
            }
        }

        static void Stop()
        {
            try { listener?.Stop(); listener?.Close(); }
            catch (Exception) { /* already closed */ }
            listener = null;
        }

        static void Pump()
        {
            playing = EditorApplication.isPlaying;
            while (mainThread.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Listen()
        {
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch (Exception) { return; }

                try { Handle(context); }
                catch (Exception e) { Send(context, 500, "text/plain", e.Message); }
            }
        }

        static string BalancePath => Path.Combine(projectRoot, Balance.AssetPath);

        static void Handle(HttpListenerContext context)
        {
            var request = context.Request;
            string path = request.Url.AbsolutePath;

            if (request.HttpMethod == "GET" && (path == "/" || path == "/index.html"))
            {
                var page = Path.Combine(projectRoot, "Tools", "BalanceConsole", "index.html");
                Send(context, 200, "text/html; charset=utf-8", File.Exists(page) ? File.ReadAllText(page) : "Tools/BalanceConsole/index.html is missing.");
                return;
            }

            // Fish pictures for the Swimming tab (made by Tools/Meshy/console_pictures.py).
            if (request.HttpMethod == "GET" && path.StartsWith("/fish/"))
            {
                string name = path.Substring("/fish/".Length);
                var file = Path.Combine(projectRoot, "Tools", "BalanceConsole", "fish", name);
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9_]+\.webp$") && File.Exists(file))
                    SendBytes(context, 200, "image/webp", File.ReadAllBytes(file));
                else
                    Send(context, 404, "text/plain", "Not found");
                return;
            }

            if (path == "/api/balance" && request.HttpMethod == "GET")
            {
                // Parsed and written back out so species added since the last save are included.
                var saved = File.Exists(BalancePath) ? Balance.Parse(File.ReadAllText(BalancePath)) : null;
                string json = saved != null ? JsonUtility.ToJson(saved, true) : defaultJson;
                Send(context, 200, "application/json", json);
                return;
            }

            if (path == "/api/defaults" && request.HttpMethod == "GET")
            {
                Send(context, 200, "application/json", defaultJson);
                return;
            }

            if (path == "/api/status" && request.HttpMethod == "GET")
            {
                Send(context, 200, "application/json", $"{{\"playing\":{(playing ? "true" : "false")},\"saved\":{(File.Exists(BalancePath) ? "true" : "false")}}}");
                return;
            }

            if (path == "/api/balance" && request.HttpMethod == "POST")
            {
                string json;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    json = reader.ReadToEnd();

                if (Balance.Parse(json) == null)
                {
                    Send(context, 400, "application/json", "{\"ok\":false,\"error\":\"That JSON could not be read.\"}");
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(BalancePath));
                File.WriteAllText(BalancePath, json);
                bool applied = playing;
                mainThread.Enqueue(() =>
                {
                    AssetDatabase.ImportAsset(Balance.AssetPath);
                    if (EditorApplication.isPlaying && Balance.Apply(json))
                        Debug.Log("[FishBreeder] Balance console: new values applied to the running game.");
                });
                Send(context, 200, "application/json", $"{{\"ok\":true,\"applied\":{(applied ? "true" : "false")}}}");
                return;
            }

            Send(context, 404, "text/plain", "Not found");
        }

        static void Send(HttpListenerContext context, int status, string contentType, string body) =>
            SendBytes(context, status, contentType, Encoding.UTF8.GetBytes(body));

        static void SendBytes(HttpListenerContext context, int status, string contentType, byte[] bytes)
        {
            var response = context.Response;
            response.StatusCode = status;
            response.ContentType = contentType;
            response.Headers["Cache-Control"] = "no-store";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
    }
}
