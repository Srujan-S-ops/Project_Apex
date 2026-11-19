using Apex.Core;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>Assembles a complete drivable car (physics, colliders, visual, audio, effects).</summary>
    public static class CarFactory
    {
        public const int CarLayer = 0;   // default layer; ghosting handled via Physics.IgnoreCollision
        private static PhysicsMaterial _bodyMaterial;

        public static CarController Create(Transform parent, CarSpec spec, CarVisualBuilder.Livery livery, bool isPlayer, bool withAudio, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<Rigidbody>();

            if (_bodyMaterial == null)
            {
                _bodyMaterial = new PhysicsMaterial("CarBody")
                {
                    dynamicFriction = 0.15f, staticFriction = 0.2f, bounciness = 0.1f,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average
                };
            }
            AddBox(root, new Vector3(0f, 0.5f, -0.05f), new Vector3(1.94f, 0.62f, 4.55f));
            AddBox(root, new Vector3(0f, 1.0f, -0.35f), new Vector3(1.3f, 0.4f, 1.6f));
            AddBox(root, new Vector3(0f, 1.2f, -2.25f), new Vector3(1.9f, 0.12f, 0.35f));   // wing

            var car = root.AddComponent<CarController>();
            car.Setup(spec);

            var settings = GameServices.Instance.Settings;
            bool high = isPlayer || settings.qualityPreset >= 2;
            var visual = CarVisualBuilder.Build(root.transform, spec, livery, high);
            visual.Bind(car);

            var fx = root.AddComponent<CarEffects>();
            fx.Setup(car, visual, GraphicsQuality.Detail(settings));
            if (withAudio)
            {
                var audio = root.AddComponent<CarAudio>();
                audio.Setup(car, isPlayer);
            }
            if (isPlayer) AddHeadlights(root.transform, visual);
            return car;
        }

        private static void AddBox(GameObject root, Vector3 center, Vector3 size)
        {
            var b = root.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
            b.sharedMaterial = _bodyMaterial;
        }

        private static void AddHeadlights(Transform root, CarVisual visual)
        {
            foreach (var p in visual.HeadlightPoints)
            {
                var go = new GameObject("HeadlightSpot");
                go.transform.SetParent(root, false);
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 60f;
                l.spotAngle = 55f;
                l.innerSpotAngle = 25f;
                l.intensity = 6f;
                l.color = new Color(0.92f, 0.95f, 1f);
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>Visual-only car for the garage / menu showroom.</summary>
        public static CarVisual CreateShowroom(Transform parent, CarSpec spec, CarVisualBuilder.Livery livery)
        {
            var root = new GameObject("ShowroomCar_" + spec.Id);
            root.transform.SetParent(parent, false);
            var visual = CarVisualBuilder.Build(root.transform, spec, livery, true);
            return visual;
        }

        public static CarVisualBuilder.Livery PlayerLivery(CarSpec spec) => new CarVisualBuilder.Livery
        {
            Body = spec.BodyColor, Accent = spec.AccentColor, Trim = spec.TrimColor,
            Helmet = new Color(0.9f, 0.9f, 0.92f), Number = 7, Team = "APEX WORKS"
        };
    }
}
