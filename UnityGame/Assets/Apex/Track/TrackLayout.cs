using UnityEngine;

namespace Apex.Track
{
    /// <summary>
    /// Hand-designed control points of the Porto Vela Street Circuit (original layout, clockwise).
    /// x/z in metres (pre-scale), y = elevation, w = road width.
    /// </summary>
    public static class TrackLayout
    {
        public const string Id = "harbour_gp";
        public const string Name = "PORTO VELA STREET CIRCUIT";
        public const float TargetLength = 3500f;
        public const float StartLineDistance = 240f;     // metres from spline start to start/finish line

        public static readonly Vector4[] ControlPoints =
        {
            // x,     z,     y,    width
            new Vector4(-350f,    0f,  0.0f, 15f),
            new Vector4(-150f,    0f,  0.0f, 15f),
            new Vector4( 100f,    0f,  0.0f, 15f),
            new Vector4( 330f,    0f,  0.0f, 14f),
            new Vector4( 395f,  -12f, -0.3f, 14f),   // T1 "Bastione"
            new Vector4( 428f,  -60f, -1.0f, 13f),
            new Vector4( 432f, -150f, -2.5f, 13f),
            new Vector4( 436f, -215f, -3.5f, 13f),
            new Vector4( 462f, -245f, -4.0f, 12f),   // T2-T3 chicane "Molo"
            new Vector4( 440f, -275f, -4.3f, 12f),
            new Vector4( 445f, -320f, -4.8f, 13f),
            new Vector4( 470f, -400f, -6.0f, 13f),   // T4 fast sweep "Faro"
            new Vector4( 455f, -480f, -7.0f, 13f),
            new Vector4( 410f, -528f, -7.5f, 13f),   // T5 onto the harbour
            new Vector4( 330f, -548f, -7.8f, 14f),
            new Vector4( 150f, -556f, -8.0f, 14f),   // Harbour straight
            new Vector4(-100f, -560f, -7.0f, 14f),
            new Vector4(-350f, -552f, -5.5f, 14f),
            new Vector4(-480f, -538f, -4.5f, 13f),   // T6 "Dogana" heavy braking
            new Vector4(-535f, -505f, -3.5f, 12f),
            new Vector4(-555f, -455f, -1.0f, 12f),
            new Vector4(-548f, -405f,  2.0f, 11.5f), // Old town climb "Castello"
            new Vector4(-520f, -378f,  3.5f, 11.5f),
            new Vector4(-540f, -345f,  5.0f, 11.5f),
            new Vector4(-522f, -305f,  6.5f, 11.5f),
            new Vector4(-480f, -275f,  7.5f, 12f),
            new Vector4(-400f, -262f,  8.0f, 13f),   // Boulevard
            new Vector4(-270f, -265f,  8.0f, 13f),
            new Vector4(-170f, -282f,  7.6f, 13f),
            new Vector4(-110f, -275f,  7.2f, 13f),   // "Tornante" hairpin
            new Vector4( -88f, -250f,  7.0f, 13f),
            new Vector4(-100f, -220f,  6.8f, 13f),
            new Vector4(-135f, -205f,  6.5f, 13f),
            new Vector4(-230f, -195f,  5.5f, 13f),
            new Vector4(-340f, -190f,  4.0f, 13f),
            new Vector4(-410f, -175f,  3.0f, 13f),
            new Vector4(-440f, -135f,  2.0f, 13f),   // "Fontana"
            new Vector4(-445f,  -70f,  1.0f, 14f),
            new Vector4(-425f,  -18f,  0.2f, 14f),   // final corner "Lanterna"
            new Vector4(-395f,   -2f,  0.0f, 15f),
        };

        /// <summary>Approximate centre-line distances (post-scale) of named corners for HUD/briefing.</summary>
        public static readonly (string name, int controlPoint)[] Corners =
        {
            ("BASTIONE", 4), ("MOLO", 8), ("FARO", 11), ("PORTO", 13), ("DOGANA", 18),
            ("CASTELLO", 22), ("TORNANTE", 30), ("FONTANA", 36), ("LANTERNA", 38)
        };
    }
}
