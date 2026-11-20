using UnityEngine;

namespace Apex.Vehicle
{
    public enum CarClass { GT3, Formula }

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

        // Class-specific chassis parameters. Defaults reproduce the original GT3 setup exactly.
        public CarClass Class = CarClass.GT3;
        public float CenterOfMassHeight = 0.33f;
        public float SuspensionDistance = 0.14f;
        public float SpringFront = 82000f, SpringRear = 88000f;
        public float DamperFront = 5600f, DamperRear = 6000f;
        public float AntiRollFront = 11000f, AntiRollRear = 8500f;
        public float WheelMass = 22f;
        public float FrontTyreHalfWidth = 0.155f, RearTyreHalfWidth = 0.155f;
        public Vector3 HoodCamera = new Vector3(0f, 1.2f, 0.3f);

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

        /// <summary>Original open-wheel single-seaters (~1000 hp, 798 kg, 8-speed, very high downforce).</summary>
        public static readonly CarSpec[] FormulaRoster =
        {
            Formula("apex_f1", "APEX FX-26", "APEX ENGINEERING", new Color(0.8f, 0.02f, 0.05f), new Color(0.95f, 0.95f, 0.95f), 640f, 1f),
            Formula("vanta_f1", "VANTA VF-1", "VANTA AUTOMOBILI", new Color(0.05f, 0.05f, 0.06f), new Color(0.9f, 0.75f, 0.2f), 655f, 0.97f),
            Formula("kaiser_f1", "KAISER K-26", "KAISER MOTORSPORT", new Color(0.82f, 0.84f, 0.86f), new Color(0.05f, 0.25f, 0.65f), 625f, 1.04f),
        };

        private static CarSpec Formula(string id, string name, string maker, Color body, Color accent, float torque, float downforceScale) => new CarSpec
        {
            Id = id, Name = name, Maker = maker, Engine = "1.6L V6 TURBO HYBRID", Class = CarClass.Formula,
            MassKg = 798f, MaxTorqueNm = torque, PeakTorqueRpm = 10800f, IdleRpm = 4000f, MaxRpm = 12500f,
            GearRatios = new[] { 3.05f, 2.45f, 2.02f, 1.72f, 1.48f, 1.31f, 1.18f, 1.08f }, FinalDrive = 4.6f, ReverseRatio = 3.2f,
            DrivetrainEfficiency = 0.92f, BrakeTorqueNm = 10500f, BrakeBias = 0.57f,
            DownforceClA = 4.8f * downforceScale, AeroBalanceFront = 0.42f, DragCdA = 1.15f,
            WheelRadius = 0.36f, Wheelbase = 3.6f, TrackWidth = 1.6f, MaxSteerDeg = 22f, GripMultiplier = 1.12f,
            CenterOfMassHeight = 0.24f, SuspensionDistance = 0.09f,
            SpringFront = 150000f, SpringRear = 165000f, DamperFront = 7200f, DamperRear = 7800f,
            AntiRollFront = 16000f, AntiRollRear = 12000f, WheelMass = 18f,
            FrontTyreHalfWidth = 0.15f, RearTyreHalfWidth = 0.2f,
            HoodCamera = new Vector3(0f, 1.05f, -0.25f),
            BodyColor = body, AccentColor = accent, TrimColor = new Color(0.04f, 0.04f, 0.045f)
        };

        public static CarSpec[] RosterFor(CarClass c) => c == CarClass.Formula ? FormulaRoster : Roster;

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
