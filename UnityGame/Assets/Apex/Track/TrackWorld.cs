using System.Collections.Generic;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>Runtime description of the generated circuit, shared by race, AI, HUD and camera systems.</summary>
    public sealed class TrackWorld : MonoBehaviour
    {
        public TrackSpline Spline;
        public TrackAnalysis Analysis;
        public float StartLine;                     // centre-line distance of the start/finish line
        public float[] SectorStarts = new float[3]; // distances (sector 1 starts at the line)
        public float[] Checkpoints;                 // distances measured from the start line
        public float[] LeftWall, RightWall;         // lateral distance of barriers per sample
        public readonly List<Pose> GridSlots = new List<Pose>();
        public Pose TimeTrialStart;
        public Material[] StartLights = new Material[5];
        public Light[] StartLightLamps = new Light[5];
        public Bounds Bounds;
        public Transform CameraPostsRoot;
        public readonly List<Vector3> TvCameraPosts = new List<Vector3>();
        public readonly List<Vector3> CrowdPoints = new List<Vector3>();
        public Vector3 PitAmbiencePoint;

        public float TrackDistanceFromLine(float centreDistance) => Spline.WrapDistance(centreDistance - StartLine);

        public int SectorOf(float distFromLine)
        {
            float l = Spline.Length;
            if (distFromLine < l / 3f) return 0;
            if (distFromLine < 2f * l / 3f) return 1;
            return 2;
        }

        public void SetStartLights(int lit, bool green = false)
        {
            for (int i = 0; i < 5; i++)
            {
                if (StartLights[i] == null) continue;
                bool on = i < lit;
                Color c = green ? new Color(0.1f, 1f, 0.2f) : new Color(1f, 0.04f, 0.02f);
                Rendering.MaterialLibrary.SetEmission(StartLights[i], c, on || green ? 14f : 0f);
                if (StartLightLamps[i] != null)
                {
                    StartLightLamps[i].enabled = on || green;
                    StartLightLamps[i].color = c;
                }
            }
        }
    }
}
