using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>Immutable-by-convention vehicle definition (physics + presentation data).</summary>
    public sealed class CarSpec
    {
        public string Id;
        public string Name;
        public string Maker;
        public string Engine;
        public float MassKg = 1300f;
        public float MaxTorqueNm = 620f;
        public float PeakTorqueRpm = 5600f;
        public float IdleRpm = 1100f;
        public float MaxRpm = 8600f;
        public float[] GearRatios = { 3.15f, 2.35f, 1.86f, 1.52f, 1.27f, 1.08f };
        public float ReverseRatio = 3.0f;
        public float FinalDrive = 3.55f;
        public float DrivetrainEfficiency = 0.9f;
        public float BrakeTorqueNm = 7800f;   // total per car at full pedal
        public float BrakeBias = 0.62f;       // front share
        public float DownforceClA = 3.1f;     // lift coefficient * area
        public float AeroBalanceFront = 0.43f;
        public float DragCdA = 0.78f;
        public float WheelRadius = 0.345f;
        public float Wheelbase = 2.66f;
        public float TrackWidth = 1.66f;
        public float MaxSteerDeg = 30f;
        public float GripMultiplier = 1f;
        public Color BodyColor = new Color(0.75f, 0.02f, 0.04f);
        public Color AccentColor = Color.white;
        public Color TrimColor = new Color(0.05f, 0.05f, 0.06f);

        public float PowerKw
        {
            get
            {
                float best = 0f;
                for (int i = 0; i <= 100; i++)
                {
                    float rpm = Mathf.Lerp(IdleRpm, MaxRpm, i / 100f);
                    best = Mathf.Max(best, MaxTorqueNm * TorqueCurve(rpm) * rpm * Mathf.PI / 30f / 1000f);
                }
                return best;
            }
        }
        public float PowerHp => PowerKw * 1.341f;
        public float TopSpeedKmh => MaxRpm / (GearRatios[GearRatios.Length - 1] * FinalDrive) / 60f * 2f * Mathf.PI * WheelRadius * 3.6f;

        public static readonly CarSpec[] Roster =
        {
            new CarSpec
            {
                Id = "apex_r", Name = "APEX R-GT3", Maker = "APEX ENGINEERING", Engine = "4.0L FLAT-6",
                MaxTorqueNm = 545f, PeakTorqueRpm = 6000f, MaxRpm = 9000f, MassKg = 1280f, DownforceClA = 3.2f,
                FinalDrive = 3.7f, BodyColor = new Color(0.78f, 0.02f, 0.04f), AccentColor = new Color(0.95f, 0.95f, 0.95f)
            },
            new CarSpec
            {
                Id = "vanta_evo", Name = "VANTA GT3 EVO", Maker = "VANTA AUTOMOBILI", Engine = "5.2L V10",
                MaxTorqueNm = 580f, PeakTorqueRpm = 6200f, MaxRpm = 8500f, MassKg = 1320f, DownforceClA = 2.85f,
                DragCdA = 0.74f, BodyColor = new Color(0.06f, 0.06f, 0.07f), AccentColor = new Color(0.85f, 0.05f, 0.08f)
            },
            new CarSpec
            {
                Id = "kaiser_rs", Name = "KAISER GT3 RS", Maker = "KAISER MOTORSPORT", Engine = "3.9L TWIN-TURBO V8",
                MaxTorqueNm = 625f, PeakTorqueRpm = 4800f, MaxRpm = 7800f, MassKg = 1310f, DownforceClA = 3.45f,
                DragCdA = 0.84f, FinalDrive = 3.35f, BodyColor = new Color(0.82f, 0.84f, 0.86f), AccentColor = new Color(0.8f, 0.02f, 0.05f)
            }
        };

        /// <summary>Liveries for AI teams (body, accent).</summary>
        public static readonly (Color body, Color accent, string team, int number)[] AiLiveries =
        {
            (new Color(0.05f, 0.18f, 0.55f), new Color(1f, 0.75f, 0.05f), "NOVA RACING", 11),
            (new Color(0.02f, 0.40f, 0.22f), new Color(0.95f, 0.95f, 0.95f), "HELIX MOTORSPORT", 23),
            (new Color(0.95f, 0.45f, 0.02f), new Color(0.06f, 0.06f, 0.07f), "STRATOS ENERGY", 44),
            (new Color(0.45f, 0.05f, 0.55f), new Color(0.9f, 0.9f, 0.92f), "KAIROS TEAM", 63)
        };

        public CarSpec WithColors(Color body, Color accent)
        {
            var c = (CarSpec)MemberwiseClone();
            c.BodyColor = body;
            c.AccentColor = accent;
            return c;
        }

        /// <summary>Normalised engine torque curve (0..1) at rpm.</summary>
        public float TorqueCurve(float rpm)
        {
            if (rpm <= IdleRpm) return 0.55f;
            if (rpm <= PeakTorqueRpm)
            {
                float t = (rpm - IdleRpm) / (PeakTorqueRpm - IdleRpm);
                return Mathf.Lerp(0.55f, 1f, 1f - (1f - t) * (1f - t));
            }
            float k = Mathf.Clamp01((rpm - PeakTorqueRpm) / (MaxRpm - PeakTorqueRpm));
            return Mathf.Lerp(1f, 0.8f, k * k);
        }
    }
}
