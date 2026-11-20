using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apex.Audio
{
    /// <summary>Engine voice parameters (per car model).</summary>
    public sealed class EngineVoice
    {
        public string Id;
        public int Cylinders = 6;
        public float Resonance = 260f;        // exhaust resonance Hz
        public float Brightness = 0.55f;      // 0..1 low-pass openness
        public float Lope = 0.18f;            // per-cylinder amplitude variance
        public float Grit = 0.35f;            // combustion noise share

        public static EngineVoice ForCar(string carId)
        {
            switch (carId)
            {
                case "vanta_evo": return new EngineVoice { Id = carId, Cylinders = 10, Resonance = 330f, Brightness = 0.7f, Lope = 0.1f, Grit = 0.3f };
                // turbo-hybrid V6 single-seaters: high-revving, bright, smooth
                case "apex_f1": return new EngineVoice { Id = carId, Cylinders = 6, Resonance = 540f, Brightness = 0.86f, Lope = 0.03f, Grit = 0.22f };
                case "vanta_f1": return new EngineVoice { Id = carId, Cylinders = 6, Resonance = 600f, Brightness = 0.9f, Lope = 0.02f, Grit = 0.2f };
                case "kaiser_f1": return new EngineVoice { Id = carId, Cylinders = 6, Resonance = 500f, Brightness = 0.82f, Lope = 0.04f, Grit = 0.26f };
                case "kaiser_rs": return new EngineVoice { Id = carId, Cylinders = 8, Resonance = 190f, Brightness = 0.45f, Lope = 0.3f, Grit = 0.42f };
                default: return new EngineVoice { Id = carId, Cylinders = 6, Resonance = 280f, Brightness = 0.62f, Lope = 0.16f, Grit = 0.33f };
            }
        }
    }

    /// <summary>
    /// Procedurally synthesised, original audio. All clips are generated once and cached.
    /// </summary>
    public sealed class AudioLibrary
    {
        public const int SampleRate = 44100;
        public static readonly int[] EngineLayerRpm = { 2040, 4080, 6000, 7920 };   // multiples of 120 → seamless loops

        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        // ------------------------------------------------------------------ public clips

        public AudioClip EngineLayer(EngineVoice v, int layer, bool onLoad)
        {
            string key = $"eng_{v.Id}_{layer}_{onLoad}";
            if (_cache.TryGetValue(key, out var c)) return c;
            float[] data = SynthEngine(v, EngineLayerRpm[layer], onLoad);
            return Store(key, data, true);
        }

        public AudioClip TyreSqueal() => Get("tyre", () => MakeLoop(SynthSqueal(2.2f), 0.2f));
        public AudioClip Wind() => Get("wind", () => MakeLoop(SynthFilteredNoise(3f, 0.04f, 0.9f, 31), 0.4f));
        public AudioClip Scrub() => Get("scrub", () => MakeLoop(SynthFilteredNoise(2f, 0.12f, 0.5f, 77), 0.3f));
        public AudioClip GearShift() => Get("gear", () => SynthThump(0.16f, 70f, 0.6f, 5));
        public AudioClip Backfire(int variant) => Get("pop" + variant, () => SynthPop(variant));
        public AudioClip Impact(int variant) => Get("impact" + variant, () => SynthImpact(variant));
        public AudioClip CrowdAmbience() => Get("crowd", () => MakeLoop(SynthCrowd(8f), 1.0f));
        public AudioClip PitAmbience() => Get("pit", () => MakeLoop(SynthPit(8f), 0.8f));
        public AudioClip CityAmbience() => Get("city", () => MakeLoop(SynthFilteredNoise(6f, 0.015f, 0.3f, 9), 1f));
        public AudioClip Beep(bool high) => Get(high ? "beepHi" : "beepLo", () => SynthBeep(high ? 1320f : 660f, high ? 0.6f : 0.25f));
        public AudioClip UiTick() => Get("uitick", () => SynthBeep(2400f, 0.035f, 0.25f));
        public AudioClip UiConfirm() => Get("uiconfirm", () => SynthSweep(600f, 1400f, 0.14f));
        public AudioClip UiBack() => Get("uiback", () => SynthSweep(900f, 420f, 0.12f));
        public AudioClip MenuMusic() => Get("music", SynthMusic);

        // ------------------------------------------------------------------ helpers

        private AudioClip Get(string key, Func<float[]> make)
        {
            if (_cache.TryGetValue(key, out var c)) return c;
            return Store(key, make(), false);
        }

        private AudioClip Store(string key, float[] data, bool loop)
        {
            var clip = AudioClip.Create(key, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            _cache[key] = clip;
            return clip;
        }

        private static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>Crossfades the tail into the head so the clip loops seamlessly.</summary>
        private static float[] MakeLoop(float[] src, float fadeSeconds)
        {
            int f = Mathf.Min((int)(fadeSeconds * SampleRate), src.Length / 3);
            int n = src.Length - f;
            var outp = new float[n];
            for (int i = 0; i < n; i++) outp[i] = src[i];
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                outp[i] = src[i] * Mathf.Sqrt(t) + src[n + i] * Mathf.Sqrt(1f - t);
            }
            return outp;
        }

        private static void Normalize(float[] d, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        // ------------------------------------------------------------------ engine

        private static float[] SynthEngine(EngineVoice v, int rpm, bool onLoad)
        {
            int n = SampleRate;                       // 1 s loop
            int warm = SampleRate;                    // 1 s warm-up for filter state
            float cycleHz = rpm / 120f;               // 4-stroke: one engine cycle per two revolutions
            int cylinders = v.Cylinders;
            float firingHz = cycleHz * cylinders;
            float decay = onLoad ? Mathf.Lerp(90f, 220f, rpm / 8000f) : Mathf.Lerp(150f, 320f, rpm / 8000f);
            float res = v.Resonance * (onLoad ? 1f : 0.8f) * Mathf.Lerp(0.85f, 1.35f, rpm / 8000f);
            float grit = v.Grit * (onLoad ? 1f : 0.55f);
            float cutoff = Mathf.Lerp(900f, 5200f, v.Brightness) * (onLoad ? 1f : 0.6f) * Mathf.Lerp(0.7f, 1.2f, rpm / 8000f);
            float lpA = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
            float hpA = 1f - Mathf.Exp(-2f * Mathf.PI * 35f / SampleRate);

            var amp = new float[cylinders];
            for (int k = 0; k < cylinders; k++) amp[k] = 1f + v.Lope * Hash(k, cylinders, 3);

            var outp = new float[n];
            float lp = 0f, lp2 = 0f, hp = 0f;
            int cyclesPerLoop = Mathf.RoundToInt(cycleHz);   // integer by construction
            for (int i = 0; i < n + warm; i++)
            {
                float t = (i % n) / (float)SampleRate;
                float cyc = t * cycleHz;
                int cycleIndex = ((int)Mathf.Floor(cyc)) % Mathf.Max(1, cyclesPerLoop);
                float phase = cyc - Mathf.Floor(cyc);
                float s = 0f;
                for (int k = 0; k < cylinders; k++)
                {
                    float p = phase - k / (float)cylinders;
                    if (p < 0f) p += 1f;
                    float dt = p / cycleHz;            // seconds since this cylinder fired
                    if (dt > 0.03f) continue;
                    float env = Mathf.Exp(-dt * decay);
                    float tone = Mathf.Sin(2f * Mathf.PI * res * dt) * (1f - grit);
                    float noise = Hash(cycleIndex, k, (int)(dt * SampleRate)) * grit;
                    s += env * (tone + noise) * amp[k];
                }
                // firing-order fundamental and crank half-order for body
                s += 0.35f * Mathf.Sin(2f * Mathf.PI * firingHz * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * cycleHz * 0.5f * cylinders * t + 1.3f);
                s += 0.12f * Mathf.Sin(2f * Mathf.PI * cycleHz * t);
                lp += (s - lp) * lpA;
                lp2 += (lp - lp2) * lpA;
                hp += (lp2 - hp) * hpA;
                float y = lp2 - hp;
                y = (float)Math.Tanh(y * (onLoad ? 2.2f : 1.4f));
                if (i >= warm) outp[i - warm] = y;
            }
            Normalize(outp, onLoad ? 0.9f : 0.6f);
            return outp;
        }

        // ------------------------------------------------------------------ effects

        private static float[] SynthSqueal(float seconds)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var rng = new System.Random(5);
            float bp1 = 0, bp2 = 0, mod = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                // resonant band-pass around 1.1 kHz (state-variable filter)
                float f = 2f * Mathf.Sin(Mathf.PI * (1050f + 220f * Mathf.Sin(t * 5.3f)) / SampleRate);
                float q = 0.06f;
                bp2 += f * bp1;
                float hpv = noise - bp2 - q * bp1;
                bp1 += f * hpv;
                mod += ((float)rng.NextDouble() - mod) * 0.0005f;
                float tone = Mathf.Sin(2f * Mathf.PI * (1380f + 90f * Mathf.Sin(t * 3.1f)) * t) * 0.35f
                             + Mathf.Sin(2f * Mathf.PI * 2760f * t + 0.5f) * 0.12f;
                d[i] = bp1 * 0.6f + tone * (0.5f + mod);
            }
            Normalize(d, 0.8f);
            return d;
        }

        private static float[] SynthFilteredNoise(float seconds, float lowA, float gain, int seed)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var rng = new System.Random(seed);
            float lp = 0, lp2 = 0, swell = 0;
            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * lowA;
                lp2 += (lp - lp2) * lowA;
                swell += ((float)rng.NextDouble() - swell) * 0.00005f;
                d[i] = lp2 * (0.6f + swell);
            }
            Normalize(d, gain);
            return d;
        }

        private static float[] SynthThump(float seconds, float freq, float gain, int seed)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 28f);
                float click = i < 300 ? (float)(rng.NextDouble() * 2 - 1) * (1f - i / 300f) : 0f;
                d[i] = Mathf.Sin(2f * Mathf.PI * freq * t * (1f - t * 2f)) * env + click * 0.5f;
            }
            Normalize(d, gain);
            return d;
        }

        private static float[] SynthPop(int variant)
        {
            int n = (int)(0.22f * SampleRate);
            var d = new float[n];
            var rng = new System.Random(100 + variant);
            float lp = 0;
            int bursts = 1 + variant % 3;
            for (int b = 0; b < bursts; b++)
            {
                int start = (int)(b * 0.045f * SampleRate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-t * (55f + variant * 10f));
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    lp += (noise - lp) * 0.25f;
                    d[i] += (lp * 0.8f + Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.7f) * env * (1f - b * 0.25f);
                }
            }
            Normalize(d, 0.95f);
            return d;
        }

        private static float[] SynthImpact(int variant)
        {
            int n = (int)(0.6f * SampleRate);
            var d = new float[n];
            var rng = new System.Random(200 + variant);
            float lp = 0;
            float[] partials = { 410f + variant * 37f, 1130f + variant * 61f, 2310f, 3170f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.08f;
                float body = lp * Mathf.Exp(-t * 14f) * 1.4f + Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t * 18f);
                float ring = 0f;
                for (int p = 0; p < partials.Length; p++) ring += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (9f + p * 6f)) / (p + 1.5f);
                float crunch = noise * Mathf.Exp(-t * 30f) * 0.6f;
                d[i] = body + ring * 0.5f + crunch;
            }
            Normalize(d, 0.95f);
            return d;
        }

        private static float[] SynthCrowd(float seconds)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var rng = new System.Random(42);
            const int voices = 6;
            var lp = new float[voices];
            var bp = new float[voices];
            var phase = new float[voices];
            for (int v = 0; v < voices; v++) phase[v] = (float)rng.NextDouble() * 10f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0;
                for (int v = 0; v < voices; v++)
                {
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    float a = 0.05f + v * 0.02f;
                    lp[v] += (noise - lp[v]) * a;
                    bp[v] += (lp[v] - bp[v]) * 0.01f;
                    float swell = 0.55f + 0.45f * Mathf.Sin(t * (0.21f + v * 0.07f) + phase[v]);
                    s += (lp[v] - bp[v]) * swell;
                }
                // occasional cheer swell
                float cheer = Mathf.Max(0f, Mathf.Sin(t * 0.55f + 1.2f)) ;
                d[i] = s * (0.7f + 0.6f * cheer * cheer * cheer);
            }
            Normalize(d, 0.7f);
            return d;
        }

        private static float[] SynthPit(float seconds)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            var rng = new System.Random(9);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.15f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.07f;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.03f;
                // impact-wrench bursts
                float burstT = t % 2.7f;
                float wrench = 0f;
                if (burstT > 1.1f && burstT < 1.55f)
                {
                    float saw = (t * 72f) % 1f * 2f - 1f;
                    wrench = (saw * 0.5f + noise * 0.3f) * (0.6f + 0.4f * Mathf.Sin(t * 60f));
                }
                d[i] = hum + lp * 0.6f + wrench * 0.5f;
            }
            Normalize(d, 0.6f);
            return d;
        }

        private static float[] SynthBeep(float freq, float seconds, float gain = 0.7f)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Min(1f, (seconds - t) * 60f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f) * env;
            }
            Normalize(d, gain);
            return d;
        }

        private static float[] SynthSweep(float f0, float f1, float seconds)
        {
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                ph += 2f * Mathf.PI * f / SampleRate;
                float env = Mathf.Min(1f, t * 30f) * (1f - t);
                d[i] = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2f)) * env;
            }
            Normalize(d, 0.35f);
            return d;
        }

        /// <summary>Original 16-bar ambient synthwave loop for the menus.</summary>
        private static float[] SynthMusic()
        {
            const float bpm = 100f;
            float beat = 60f / bpm;
            int bars = 8;
            float seconds = bars * 4 * beat;
            int n = (int)(seconds * SampleRate);
            var d = new float[n];
            // A minor progression: Am – F – C – G (two bars each)
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f },
                new[] { 130.81f, 196f, 261.63f }, new[] { 196f, 246.94f, 293.66f }
            };
            float[] bass = { 55f, 43.65f, 65.41f, 49f };
            float lp = 0;
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                int bar = (int)(t / (4 * beat));
                int ci = (bar / 2) % 4;
                float inBar = t % (4 * beat);
                float s = 0;
                // pad (detuned saws, slow attack per chord)
                float chordT = t % (8 * beat);
                float padEnv = Mathf.Min(1f, chordT / 1.2f) * Mathf.Min(1f, (8 * beat - chordT) / 0.6f + 0.3f);
                foreach (float f in chords[ci])
                {
                    s += (Saw(f * t) + Saw(f * 1.006f * t)) * 0.06f * padEnv;
                }
                // pulsing bass on 8ths
                float eighth = t % (beat * 0.5f);
                float bassEnv = Mathf.Exp(-eighth * 9f);
                s += Mathf.Sin(2f * Mathf.PI * bass[ci] * t) * 0.32f * bassEnv + Saw(bass[ci] * 2f * t) * 0.05f * bassEnv;
                // arpeggio 16ths
                int step = (int)(t / (beat * 0.25f)) % 8;
                float arpT = t % (beat * 0.25f);
                float arpF = chords[ci][step % 3] * (step >= 4 ? 4f : 2f);
                s += Mathf.Sin(2f * Mathf.PI * arpF * t) * Mathf.Exp(-arpT * 18f) * 0.07f;
                // kick on beats, hat on off-beats
                float beatT = t % beat;
                s += Mathf.Sin(2f * Mathf.PI * (50f + 90f * Mathf.Exp(-beatT * 40f)) * beatT) * Mathf.Exp(-beatT * 9f) * 0.45f;
                float offT = (t + beat * 0.5f) % beat;
                s += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-offT * 60f) * 0.06f;
                lp += (s - lp) * 0.35f;
                d[i] = (float)Math.Tanh(lp * 1.3f);
                _ = inBar;
            }
            Normalize(d, 0.7f);
            return d;
        }

        private static float Saw(float phaseCycles) => (phaseCycles - Mathf.Floor(phaseCycles)) * 2f - 1f;
    }
}
