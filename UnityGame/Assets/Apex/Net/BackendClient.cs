using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Apex.Net
{
    public enum LinkState { Offline, Connecting, Online }

    /// <summary>
    /// Asynchronous WebSocket link to the Java engine. Networking runs on a background task;
    /// messages are dispatched on the Unity main thread. Handles heartbeats, timeouts, invalid
    /// messages and reconnection with exponential backoff. Reliable messages are queued while offline.
    /// </summary>
    public sealed class BackendClient : MonoBehaviour
    {
        public const string DefaultUrl = "ws://127.0.0.1:8765/apex";
        public const string ClientVersion = "2.0.0";

        public LinkState State { get; private set; } = LinkState.Offline;
        public string ServerVersion { get; private set; } = "";
        public float LatencyMs { get; private set; }
        public int ReconnectCount { get; private set; }
        public long MessagesIn { get; private set; }
        public long MessagesOut { get; private set; }

        public event Action<string, string> MessageReceived;   // (type, payload json)
        public event Action<LinkState> StateChanged;
        public event Action Connected;

        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _outgoing = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<LinkState> _stateEvents = new ConcurrentQueue<LinkState>();
        private readonly List<string> _reliableBacklog = new List<string>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        private CancellationTokenSource _cts;
        private volatile ClientWebSocket _ws;
        private long _seq;
        private float _lastReceive;
        private float _lastPing;
        private string _url = DefaultUrl;
        private volatile bool _online;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-apexBackend") _url = args[i + 1];
            _cts = new CancellationTokenSource();
            Task.Run(() => RunAsync(_cts.Token));
        }

        public bool IsOnline => State == LinkState.Online;

        /// <summary>Serialises and queues a message. Reliable messages survive disconnections.</summary>
        public void Send<T>(string type, T payload, bool reliable = false)
        {
            string json = JsonUtility.ToJson(new Envelope { type = type, seq = Interlocked.Increment(ref _seq), payload = JsonUtility.ToJson(payload) });
            if (_online) { _outgoing.Enqueue(json); _sendSignal.Release(); }
            else if (reliable) { lock (_reliableBacklog) _reliableBacklog.Add(json); }
        }

        private void Update()
        {
            while (_stateEvents.TryDequeue(out var s))
            {
                State = s;
                StateChanged?.Invoke(s);
                if (s == LinkState.Online)
                {
                    _lastReceive = Time.realtimeSinceStartup;
                    Send(MsgType.Hello, new HelloMsg { clientVersion = ClientVersion });
                    lock (_reliableBacklog)
                    {
                        foreach (var m in _reliableBacklog) _outgoing.Enqueue(m);
                        if (_reliableBacklog.Count > 0) _sendSignal.Release();
                        _reliableBacklog.Clear();
                    }
                    Connected?.Invoke();
                }
            }

            int budget = 64;
            while (budget-- > 0 && _incoming.TryDequeue(out var raw))
            {
                _lastReceive = Time.realtimeSinceStartup;
                MessagesIn++;
                Envelope env;
                try { env = JsonUtility.FromJson<Envelope>(raw); }
                catch (Exception e) { Debug.LogWarning("[Backend] invalid message ignored: " + e.Message); continue; }
                if (env == null || string.IsNullOrEmpty(env.type)) { Debug.LogWarning("[Backend] message without type ignored"); continue; }
                switch (env.type)
                {
                    case MsgType.Pong:
                        try { LatencyMs = (float)((Time.realtimeSinceStartupAsDouble - JsonUtility.FromJson<PingMsg>(env.payload).t) * 1000.0); } catch { /* ignore */ }
                        break;
                    case MsgType.Welcome:
                        try { ServerVersion = JsonUtility.FromJson<WelcomeMsg>(env.payload).serverVersion; } catch { /* ignore */ }
                        break;
                    case MsgType.Error:
                        Debug.LogWarning("[Backend] server error: " + env.payload);
                        break;
                }
                try { MessageReceived?.Invoke(env.type, env.payload); }
                catch (Exception e) { Debug.LogException(e); }
            }

            if (State == LinkState.Online)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastPing > 2f)
                {
                    _lastPing = now;
                    Send(MsgType.Ping, new PingMsg { t = Time.realtimeSinceStartupAsDouble });
                }
                if (now - _lastReceive > 7f)
                {
                    Debug.LogWarning("[Backend] heartbeat timeout — reconnecting");
                    try { _ws?.Abort(); } catch { /* ignore */ }
                    _lastReceive = now;
                }
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            float backoff = 0.5f;
            while (!ct.IsCancellationRequested)
            {
                var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                _ws = ws;
                _stateEvents.Enqueue(LinkState.Connecting);
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(3));
                        await ws.ConnectAsync(new Uri(_url), timeout.Token).ConfigureAwait(false);
                    }
                    backoff = 0.5f;
                    _online = true;
                    _stateEvents.Enqueue(LinkState.Online);
                    var sendTask = SendLoop(ws, ct);
                    await ReceiveLoop(ws, ct).ConfigureAwait(false);
                    _sendSignal.Release();
                    await Task.WhenAny(sendTask, Task.Delay(500, ct)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception) { /* connection refused / dropped: fall through to retry */ }
                finally
                {
                    bool wasOnline = _online;
                    _online = false;
                    try { ws.Dispose(); } catch { /* ignore */ }
                    _stateEvents.Enqueue(LinkState.Offline);
                    if (wasOnline) ReconnectCount++;
                }
                // drop stale unreliable traffic
                while (_outgoing.TryDequeue(out _)) { }
                try { await Task.Delay(TimeSpan.FromSeconds(backoff), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                backoff = Mathf.Min(backoff * 2f, 8f);
            }
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (sb.Length > 8 * 1024 * 1024) throw new InvalidOperationException("message too large");
                } while (!r.EndOfMessage);
                _incoming.Enqueue(sb.ToString());
            }
        }

        private async Task SendLoop(ClientWebSocket ws, CancellationToken ct)
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                await _sendSignal.WaitAsync(ct).ConfigureAwait(false);
                while (ws.State == WebSocketState.Open && _outgoing.TryDequeue(out var msg))
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                    MessagesOut++;
                }
            }
        }

        private void OnDestroy()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _ws?.Abort(); } catch { /* ignore */ }
        }
    }
}
