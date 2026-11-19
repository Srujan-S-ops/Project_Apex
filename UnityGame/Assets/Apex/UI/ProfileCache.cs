using Apex.Core;
using Apex.Net;
using UnityEngine;

namespace Apex.UI
{
    /// <summary>Latest player profile from the Java engine (H2), with local fallback.</summary>
    public static class ProfileCache
    {
        public static ProfileMsg Latest;
        public static float ReceivedAt = -1f;
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            var be = GameServices.Instance.Backend;
            be.MessageReceived += (type, payload) =>
            {
                if (type != MsgType.Profile) return;
                try
                {
                    Latest = JsonUtility.FromJson<ProfileMsg>(payload);
                    ReceivedAt = Time.realtimeSinceStartup;
                }
                catch (System.Exception e) { Debug.LogWarning("[Profile] bad profile payload: " + e.Message); }
            };
            be.Connected += Request;
        }

        public static void Request()
        {
            var be = GameServices.Instance.Backend;
            if (be.IsOnline) be.Send(MsgType.GetProfile, new GetProfileMsg { trackId = RaceConfig.Current.trackId });
        }

        public static string Describe()
        {
            var rec = GameServices.Instance.Records;
            if (Latest != null && GameServices.Instance.Backend.IsOnline)
            {
                string last = Latest.recent != null && Latest.recent.Length > 0
                    ? $"LAST  P{Latest.recent[0].position}/{Latest.recent[0].fieldSize}  {Latest.recent[0].mode.ToUpper()}  {Latest.recent[0].difficulty.ToUpper()}"
                    : "LAST  —";
                return $"RACES  <b>{Latest.races}</b>     WINS  <b>{Latest.wins}</b>     PODIUMS  <b>{Latest.podiums}</b>\n" +
                       $"BEST LAP  <b>{UITheme.FormatTime(Latest.bestLap)}</b>     LAPS  <b>{Latest.totalLaps}</b>\n{last}\n<color=#6B7079>SOURCE  JAVA ENGINE · H2 DATABASE</color>";
            }
            float pb = rec.BestLap(RaceConfig.Current.trackId, GameMode.QuickRace.ToString(), out _, out _);
            float tt = rec.BestLap(RaceConfig.Current.trackId, GameMode.TimeTrial.ToString(), out _, out _);
            float best = pb > 0 && (tt <= 0 || pb < tt) ? pb : tt;
            string lastLocal = rec.History.Count > 0 ? $"LAST  P{rec.History[0].playerPosition}/{rec.History[0].fieldSize}  {rec.History[0].mode.ToUpper()}" : "LAST  —";
            return $"RACES  <b>{rec.Races}</b>     WINS  <b>{rec.Wins}</b>     PODIUMS  <b>{rec.Podiums}</b>\n" +
                   $"BEST LAP  <b>{UITheme.FormatTime(best)}</b>     LAPS  <b>{rec.TotalLaps}</b>\n{lastLocal}\n<color=#6B7079>SOURCE  LOCAL RECORDS{(GameServices.Instance.Backend.IsOnline ? " · SYNCING WITH ENGINE" : " (ENGINE OFFLINE)")}</color>";
        }
    }

    /// <summary>Menu music loop with cross-scene volume control.</summary>
    public static class MenuMusic
    {
        private static AudioSource _src;
        private static float _target;

        private sealed class Fader : MonoBehaviour
        {
            private void Update()
            {
                if (_src == null) return;
                _src.volume = Mathf.MoveTowards(_src.volume, _target, Time.unscaledDeltaTime * 0.6f);
                if (_src.volume <= 0.001f && _target <= 0f && _src.isPlaying) _src.Pause();
                else if (_target > 0f && !_src.isPlaying) _src.UnPause();
            }
        }

        public static void Play()
        {
            var svc = GameServices.Instance;
            if (_src == null)
            {
                _src = svc.gameObject.AddComponent<AudioSource>();
                _src.clip = svc.AudioLib.MenuMusic();
                _src.loop = true;
                _src.spatialBlend = 0f;
                _src.volume = 0f;
                _src.ignoreListenerPause = true;
                _src.Play();
                svc.gameObject.AddComponent<Fader>();
            }
            _target = svc.Settings.musicVolume * 0.6f;
        }

        public static void Stop() => _target = 0f;

        public static void SetVolume(float v)
        {
            if (_target > 0f) _target = v * 0.6f;
        }
    }
}
