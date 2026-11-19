using System.Collections.Generic;
using Apex.Core;
using Apex.Track;
using UnityEngine;

namespace Apex.Audio
{
    /// <summary>Crowd (grandstands), pit-lane and city ambience; crowd swells on key race events.</summary>
    public sealed class AmbienceController : MonoBehaviour
    {
        private readonly List<AudioSource> _crowd = new List<AudioSource>();
        private AudioSource _pit, _city;
        private GameSettings _settings;
        private float _cheer;

        public void Setup(TrackWorld world, GameSettings settings)
        {
            _settings = settings;
            var lib = GameServices.Instance.AudioLib;
            foreach (var p in world.CrowdPoints)
            {
                var s = Make("Crowd", p, lib.CrowdAmbience(), 25f, 260f);
                _crowd.Add(s);
            }
            _pit = Make("PitLane", world.PitAmbiencePoint, lib.PitAmbience(), 15f, 160f);
            _city = Make("City", Vector3.zero, lib.CityAmbience(), 1f, 10f);
            _city.spatialBlend = 0f;
            ApplyVolume();
        }

        private AudioSource Make(string name, Vector3 pos, AudioClip clip, float min, float max)
        {
            var go = new GameObject("Amb_" + name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
            s.priority = 200;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.Play();
            return s;
        }

        public void ApplyVolume()
        {
            float a = _settings.ambienceVolume;
            foreach (var c in _crowd) c.volume = a * (0.7f + _cheer * 0.6f);
            if (_pit != null) _pit.volume = a * 0.7f;
            if (_city != null) _city.volume = a * 0.18f;
        }

        public void Cheer(float amount) => _cheer = Mathf.Max(_cheer, amount);

        private void Update()
        {
            if (_cheer <= 0f) return;
            _cheer = Mathf.MoveTowards(_cheer, 0f, Time.deltaTime * 0.12f);
            ApplyVolume();
        }
    }
}
