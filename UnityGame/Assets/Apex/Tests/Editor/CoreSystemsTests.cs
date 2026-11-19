using Apex.AI;
using Apex.Net;
using Apex.Track;
using Apex.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Apex.Tests
{
    public sealed class TrackTests
    {
        private static TrackSpline Spline => TrackSpline.FromLayout(TrackLayout.ControlPoints, TrackLayout.TargetLength);

        [Test]
        public void CircuitLengthIsAboutThreePointFiveKilometres()
        {
            var s = Spline;
            Assert.That(s.Length, Is.InRange(3450f, 3650f), "3D length includes elevation, target is 3.5 km horizontal");
        }

        [Test]
        public void CentreLineIsAClosedLoopWithUniformSpacing()
        {
            var s = Spline;
            float closing = Vector3.Distance(s.Pos[s.Count - 1], s.Pos[0]);
            Assert.That(closing, Is.LessThan(TrackSpline.Spacing * 1.6f));
            for (int i = 0; i < s.Count; i++)
            {
                float d = Vector3.Distance(s.Pos[i], s.Pos[(i + 1) % s.Count]);
                Assert.That(d, Is.InRange(TrackSpline.Spacing * 0.6f, TrackSpline.Spacing * 1.6f), $"spacing at {i}");
            }
        }

        [Test]
        public void TrackDoesNotSelfIntersect()
        {
            var s = Spline;
            for (int i = 0; i < s.Count; i += 3)
                for (int j = i + 1; j < s.Count; j += 3)
                {
                    float along = Mathf.Min(j - i, s.Count - (j - i)) * TrackSpline.Spacing;
                    if (along < 200f) continue;
                    Vector3 a = s.Pos[i], b = s.Pos[j];
                    float flat = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                    Assert.That(flat, Is.GreaterThan(40f), $"sections {i} and {j} too close");
                }
        }

        [Test]
        public void ElevationVariesAcrossTheLap()
        {
            var s = Spline;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in s.Pos) { min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y); }
            Assert.That(max - min, Is.GreaterThan(10f));
        }

        [Test]
        public void ProjectionRecoversDistanceAndLateralOffset()
        {
            var s = Spline;
            foreach (float d in new[] { 10f, 777f, 1500f, 2900f, 3400f })
            {
                Vector3 p = s.PointAt(d, 2.5f);
                var proj = s.Project(p, -1);
                Assert.That(Mathf.Abs(s.Delta(d, proj.Distance)), Is.LessThan(1.0f), $"distance at {d}");
                Assert.That(proj.Lateral, Is.EqualTo(2.5f).Within(0.3f), $"lateral at {d}");
            }
        }

        [Test]
        public void CornerSetIncludesHairpinAndHeavyBrakingZones()
        {
            var s = Spline;
            var a = TrackAnalysis.Compute(s, VehicleEnvelope.Gt3);
            Assert.That(a.Corners.Count, Is.GreaterThanOrEqualTo(8));
            float minCornerSpeed = float.MaxValue;
            foreach (var c in a.Corners) minCornerSpeed = Mathf.Min(minCornerSpeed, c.MinSpeed);
            Assert.That(minCornerSpeed * 3.6f, Is.LessThan(110f), "hairpin should be slow");
            Assert.That(a.BrakingZones.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void RacingLineStaysInsideTrackLimitsAndSpeedProfileIsPhysical()
        {
            var s = Spline;
            var a = TrackAnalysis.Compute(s, VehicleEnvelope.Gt3);
            Assert.That(TrackAnalysis.IsValid(a.LineOffset, a.Speed, s), Is.True);
            float ds = s.Length / s.Count;
            var env = VehicleEnvelope.Gt3;
            for (int i = 0; i < s.Count; i++)
            {
                float v0 = a.Speed[i], v1 = a.Speed[(i + 1) % s.Count];
                float decel = (v0 * v0 - v1 * v1) / (2f * ds);
                Assert.That(decel, Is.LessThanOrEqualTo(33f), $"braking limit violated at {i}");
                Assert.That(v0, Is.LessThanOrEqualTo(env.TopSpeed + 0.01f));
            }
        }

        [Test]
        public void SignedCurvatureIsPositiveForRightTurns()
        {
            // heading +z then turning toward +x (right)
            var pts = new Vector3[64];
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                pts[i] = new Vector3(-Mathf.Cos(a) * 50f, 0f, Mathf.Sin(a) * 50f);   // clockwise when viewed from above
            }
            float k = TrackSpline.SignedCurvature(pts, 10, 2);
            Assert.That(k, Is.EqualTo(1f / 50f).Within(0.002f));
        }
    }

    public sealed class VehicleTests
    {
        [Test]
        public void RosterHasGt3PowerAndTopSpeeds()
        {
            foreach (var c in CarSpec.Roster)
            {
                Assert.That(c.PowerHp, Is.InRange(480f, 620f), c.Name);
                Assert.That(c.TopSpeedKmh, Is.InRange(250f, 310f), c.Name);
                Assert.That(c.GearRatios.Length, Is.EqualTo(6), c.Name);
                for (int g = 1; g < c.GearRatios.Length; g++) Assert.That(c.GearRatios[g], Is.LessThan(c.GearRatios[g - 1]));
            }
        }

        [Test]
        public void TorqueCurvePeaksAtPeakRpm()
        {
            var c = CarSpec.Roster[0];
            Assert.That(c.TorqueCurve(c.PeakTorqueRpm), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(c.TorqueCurve(c.IdleRpm), Is.LessThan(0.6f));
            Assert.That(c.TorqueCurve(c.MaxRpm), Is.InRange(0.75f, 0.85f));
        }
    }

    public sealed class ProtocolTests
    {
        [Test]
        public void EnvelopeRoundTripsWithEscapedPayload()
        {
            var cmd = new AICommandsMsg { sessionId = "abc", commands = new[] { new AICommandDto { id = 2, mode = "OVERTAKE", speedScale = 1.02f, lateralTarget = -3f, aggression = 0.9f } } };
            string json = JsonUtility.ToJson(new Envelope { type = MsgType.AiCommands, seq = 7, payload = JsonUtility.ToJson(cmd) });
            var env = JsonUtility.FromJson<Envelope>(json);
            Assert.That(env.type, Is.EqualTo("aiCommands"));
            var back = JsonUtility.FromJson<AICommandsMsg>(env.payload);
            Assert.That(back.commands[0].mode, Is.EqualTo("OVERTAKE"));
            Assert.That(back.commands[0].lateralTarget, Is.EqualTo(-3f));
        }

        [Test]
        public void JavaStyleJsonParsesIntoUnityDtos()
        {
            const string java = "{\"sessionId\":\"s1\",\"entries\":[{\"id\":0,\"position\":1,\"lap\":2,\"gapToLeader\":0.0,\"interval\":0.0,\"bestLap\":101.5}]}";
            var st = JsonUtility.FromJson<StandingsMsg>(java);
            Assert.That(st.entries.Length, Is.EqualTo(1));
            Assert.That(st.entries[0].bestLap, Is.EqualTo(101.5f));
        }

        [Test]
        public void TacticalModesMatchJavaEnumNames()
        {
            foreach (var n in new[] { "RACE", "PUSH", "OVERTAKE", "DEFEND", "CONSERVE", "RECOVER" })
                Assert.That(System.Enum.TryParse(n, true, out TacticalMode _), Is.True, n);
        }
    }

    public sealed class FormattingTests
    {
        [Test]
        public void LapTimesFormatAsMinutesSecondsMillis()
        {
            Assert.That(UI.UITheme.FormatTime(83.4567f), Is.EqualTo("1:23.457"));
            Assert.That(UI.UITheme.FormatTime(0f), Is.EqualTo("--:--.---"));
            Assert.That(UI.UITheme.FormatDelta(-0.25f), Is.EqualTo("-0.250"));
        }
    }
}
