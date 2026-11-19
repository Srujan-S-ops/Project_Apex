using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>Synchronises the visual model with the physics (wheels, lights, brake glow).</summary>
    public sealed class CarVisual : MonoBehaviour
    {
        public readonly Transform[] Knuckles = new Transform[4];
        public readonly Transform[] Spinners = new Transform[4];
        public readonly Material[] DiscMaterials = new Material[4];
        public readonly List<Vector3> ExhaustPoints = new List<Vector3>();
        public readonly List<Vector3> HeadlightPoints = new List<Vector3>();
        public Material TailLightMaterial;
        public Material HeadLightMaterial;

        private CarController _car;
        private readonly float[] _spin = new float[4];
        private float _brakeLight;
        private static readonly Color TailColor = new Color(1f, 0.05f, 0.03f);
        private static readonly Color GlowColor = new Color(1f, 0.32f, 0.04f);

        public void Bind(CarController car) => _car = car;

        /// <summary>Static showroom pose (garage / menu).</summary>
        public void SetShowroomPose(float steerDeg)
        {
            for (int i = 0; i < 2; i++)
                if (Knuckles[i] != null) Knuckles[i].localRotation = Quaternion.Euler(0f, steerDeg, 0f);
        }

        private void LateUpdate()
        {
            if (_car == null || _car.Wheels == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                var wc = _car.Wheels[i];
                if (wc == null || Knuckles[i] == null) continue;
                wc.GetWorldPose(out Vector3 pos, out _);
                Knuckles[i].position = pos;
                Knuckles[i].localRotation = Quaternion.Euler(0f, wc.steerAngle, 0f);
                bool left = i == 0 || i == 2;
                _spin[i] = (_spin[i] + wc.rpm / 60f * 360f * dt * (left ? -1f : 1f)) % 360f;
                Spinners[i].localRotation = Quaternion.Euler(_spin[i], 0f, 0f);

                if (DiscMaterials[i] != null)
                {
                    float heat = Mathf.Clamp01((_car.BrakeHeat[i] - 0.35f) / 0.65f);
                    MaterialLibrary.SetEmission(DiscMaterials[i], GlowColor, heat * heat * 6f);
                }
            }

            float target = _car.BrakeInput > 0.05f ? 1f : 0f;
            _brakeLight = Mathf.MoveTowards(_brakeLight, target, dt * 12f);
            if (TailLightMaterial != null)
                MaterialLibrary.SetEmission(TailLightMaterial, TailColor, Mathf.Lerp(1.4f, 9f, _brakeLight));
        }
    }
}
