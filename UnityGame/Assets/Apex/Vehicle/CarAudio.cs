using Apex.Audio;
using Apex.Core;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>
    /// RPM/load-driven engine audio (crossfaded synthesised layers), tyre squeal, wind, gear shifts,
    /// backfires and impacts — all driven by live CarController state.
    /// </summary>
    public sealed class CarAudio : MonoBehaviour
    {
        private CarController _car;
        private AudioSource[] _on, _off;
        private AudioSource _squeal, _wind, _oneShot;
        private bool _isPlayer;
        private float _load;
        private GameSettings _settings;
        private AudioLibrary _lib;
        private int _layers;
        private float _masterFade = 1f;

        public void Setup(CarController car, bool isPlayer)
        {
            _car = car;
            _isPlayer = isPlayer;
            _settings = GameServices.Instance.Settings;
            _lib = GameServices.Instance.AudioLib;
            var voice = EngineVoice.ForCar(car.Spec.Id);
            _layers = AudioLibrary.EngineLayerRpm.Length;
            _on = new AudioSource[_layers];
            _off = isPlayer ? new AudioSource[_layers] : null;
            for (int i = 0; i < _layers; i++)
            {
                _on[i] = MakeSource("EngOn" + i, _lib.EngineLayer(voice, i, true), true, isPlayer ? 0 : 128);
                if (isPlayer) _off[i] = MakeSource("EngOff" + i, _lib.EngineLayer(voice, i, false), true, 0);
            }
            _squeal = MakeSource("Squeal", _lib.TyreSqueal(), true, isPlayer ? 10 : 150);
            if (isPlayer) _wind = MakeSource("Wind", _lib.Wind(), true, 20);
            _oneShot = MakeSource("OneShot", null, false, isPlayer ? 5 : 140);

            car.GearChanged += OnGear;
            car.Backfire += OnBackfire;
            car.Collided += OnCollision;
        }

        private AudioSource MakeSource(string name, AudioClip clip, bool loop, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.5f, -1.2f);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.priority = priority;
            s.spatialBlend = _isPlayer ? 0.35f : 1f;
            s.dopplerLevel = _isPlayer ? 0f : 0.7f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 6f;
            s.maxDistance = 400f;
            s.volume = 0f;
            if (loop && clip != null)
            {
                s.time = Random.Range(0f, clip.length * 0.9f);
                s.Play();
            }
            return s;
        }

        public void SetMuted(bool muted) => _masterFade = muted ? 0f : 1f;

        private void Update()
        {
            if (_car == null || _car.Spec == null) return;
            float dt = Time.deltaTime;
            float engVol = _settings.engineVolume * _masterFade;
            float fxVol = _settings.effectsVolume * _masterFade;

            float rpm = _car.EngineRpm;
            float throttle = _car.Gear < 0 ? _car.BrakeInput : _car.ThrottleInput;
            float targetLoad = _car.ShiftCut > 0f ? 0.2f : throttle;
            _load = Mathf.MoveTowards(_load, targetLoad, dt * 6f);

            // locate adjacent rpm layers and equal-power crossfade
            int[] l = AudioLibrary.EngineLayerRpm;
            int lo = 0;
            for (int i = 0; i < _layers - 1; i++) if (rpm >= l[i]) lo = i;
            int hi = Mathf.Min(lo + 1, _layers - 1);
            float t = hi == lo ? 0f : Mathf.Clamp01((rpm - l[lo]) / (l[hi] - l[lo]));
            if (rpm < l[0]) t = 0f;
            float gLo = Mathf.Cos(t * Mathf.PI * 0.5f), gHi = Mathf.Sin(t * Mathf.PI * 0.5f);
            float limiterDip = _car.RevLimiterHit ? 0.75f : 1f;

            for (int i = 0; i < _layers; i++)
            {
                float g = i == lo ? gLo : (i == hi ? gHi : 0f);
                float pitch = Mathf.Clamp(rpm / l[i], 0.3f, 2.2f);
                float onShare = _isPlayer ? Mathf.Lerp(0.25f, 1f, _load) : Mathf.Lerp(0.6f, 1f, _load);
                _on[i].pitch = pitch;
                _on[i].volume = g * onShare * engVol * (_isPlayer ? 0.8f : 1f) * limiterDip;
                if (_off != null)
                {
                    _off[i].pitch = pitch;
                    _off[i].volume = g * (1f - _load) * engVol * 0.7f;
                }
            }

            // tyre squeal from combined slip of grounded wheels
            float slip = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (!_car.Grounded[i]) continue;
                slip = Mathf.Max(slip, Mathf.Abs(_car.SideSlip[i]) * 1.6f, Mathf.Abs(_car.ForwardSlip[i]));
            }
            float squeal = Mathf.Clamp01((slip - 0.32f) * 2.2f) * Mathf.Clamp01(_car.SpeedMs / 6f);
            _squeal.volume = Mathf.MoveTowards(_squeal.volume, squeal * fxVol * 0.65f, dt * 4f);
            _squeal.pitch = 0.85f + slip * 0.25f;

            if (_wind != null)
            {
                float w = Mathf.Clamp01(_car.SpeedMs / 80f);
                _wind.volume = w * w * fxVol * 0.5f;
                _wind.pitch = 0.7f + w * 0.6f;
            }
        }

        private void OnGear(int from, int to)
        {
            if (_oneShot == null) return;
            _oneShot.PlayOneShot(_lib.GearShift(), _settings.effectsVolume * 0.6f * _masterFade);
        }

        private void OnBackfire()
        {
            if (_oneShot == null) return;
            _oneShot.pitch = Random.Range(0.85f, 1.2f);
            _oneShot.PlayOneShot(_lib.Backfire(Random.Range(0, 3)), _settings.effectsVolume * (_isPlayer ? 0.55f : 0.8f) * _masterFade);
        }

        private void OnCollision(float impact, Vector3 point)
        {
            if (_oneShot == null) return;
            float v = Mathf.Clamp01(impact / 18f);
            _oneShot.PlayOneShot(_lib.Impact(Random.Range(0, 3)), Mathf.Lerp(0.2f, 1f, v) * _settings.effectsVolume * _masterFade);
        }

        private void OnDestroy()
        {
            if (_car == null) return;
            _car.GearChanged -= OnGear;
            _car.Backfire -= OnBackfire;
            _car.Collided -= OnCollision;
        }
    }
}
