using Apex.Rendering;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>Tyre smoke, skid marks, off-track dust, collision sparks and exhaust flames.</summary>
    public sealed class CarEffects : MonoBehaviour
    {
        private CarController _car;
        private CarVisual _visual;
        private ParticleSystem[] _smoke;
        private ParticleSystem[] _dust;
        private TrailRenderer[] _skids;
        private ParticleSystem _sparks;
        private ParticleSystem[] _flames;
        private Light _flameLight;
        private float _flameLightT;
        private static Material _smokeMat, _sparkMat, _flameMat, _skidMat;

        public void Setup(CarController car, CarVisual visual, float detail)
        {
            _car = car;
            _visual = visual;
            if (_smokeMat == null)
            {
                _smokeMat = MaterialLibrary.Particle(MaterialLibrary.Tex("smoke"), false);
                _sparkMat = MaterialLibrary.Particle(MaterialLibrary.Tex("spark"), true);
                _flameMat = MaterialLibrary.Particle(MaterialLibrary.Tex("flame"), true);
                _skidMat = MaterialLibrary.Particle(MaterialLibrary.Tex("skid"), false);
            }
            _smoke = new ParticleSystem[4];
            _dust = new ParticleSystem[4];
            _skids = new TrailRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                _smoke[i] = MakeSmoke("Smoke" + i, new Color(0.85f, 0.85f, 0.87f, 0.55f), (int)(220 * detail), 2.2f);
                _dust[i] = MakeSmoke("Dust" + i, new Color(0.55f, 0.47f, 0.36f, 0.5f), (int)(120 * detail), 1.4f);
                _skids[i] = MakeSkid("Skid" + i);
            }
            _sparks = MakeSparks();
            _flames = new ParticleSystem[visual.ExhaustPoints.Count];
            for (int i = 0; i < _flames.Length; i++) _flames[i] = MakeFlame(visual.ExhaustPoints[i]);
            var lgo = new GameObject("FlameLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(0f, 0.3f, -2.6f);
            _flameLight = lgo.AddComponent<Light>();
            _flameLight.type = LightType.Point;
            _flameLight.color = new Color(1f, 0.55f, 0.15f);
            _flameLight.range = 5f;
            _flameLight.intensity = 0f;
            _flameLight.shadows = LightShadows.None;

            car.Collided += OnCollision;
            car.Backfire += OnBackfire;
        }

        private ParticleSystem MakeSmoke(string name, Color color, int max, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform.parent, false);   // world-ish: parented to race root
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = Mathf.Max(30, max);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = _smokeMat;
            r.sortingFudge = 10f;
            ps.Play();
            return ps;
        }

        private TrailRenderer MakeSkid(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 12f;
            tr.minVertexDistance = 0.4f;
            tr.widthMultiplier = 0.26f;
            tr.alignment = LineAlignment.TransformZ;
            tr.material = _skidMat;
            tr.startColor = new Color(0.02f, 0.02f, 0.02f, 0.65f);
            tr.endColor = new Color(0.02f, 0.02f, 0.02f, 0f);
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return tr;
        }

        private ParticleSystem MakeSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new Color(1f, 0.75f, 0.35f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            r.material = _sparkMat;
            return ps;
        }

        private ParticleSystem MakeFlame(Vector3 localPos)
        {
            var go = new GameObject("ExhaustFlame");
            go.transform.SetParent(_visual.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
            main.startColor = new Color(1f, 0.6f, 0.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.03f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = _flameMat;
            return ps;
        }

        private void LateUpdate()
        {
            if (_car == null || _car.Wheels == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                var wc = _car.Wheels[i];
                bool grounded = wc.GetGroundHit(out WheelHit hit);
                float side = Mathf.Abs(_car.SideSlip[i]);
                float fwd = Mathf.Abs(_car.ForwardSlip[i]);
                float slip = Mathf.Max(side * 1.5f, fwd);
                bool offRoad = grounded && hit.collider != null && hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.name.StartsWith("Grass");
                Vector3 contact = grounded ? hit.point : wc.transform.position - wc.transform.up * (wc.radius + wc.suspensionDistance);

                var em = _smoke[i].emission;
                float smokeRate = grounded && !offRoad ? Mathf.Clamp01((slip - 0.45f) * 2f) * 60f * Mathf.Clamp01(_car.SpeedMs / 4f + fwd) : 0f;
                em.rateOverTime = smokeRate;
                _smoke[i].transform.position = contact + Vector3.up * 0.25f;

                var dem = _dust[i].emission;
                dem.rateOverTime = offRoad ? Mathf.Clamp01(_car.SpeedMs / 15f) * 45f : 0f;
                _dust[i].transform.position = contact + Vector3.up * 0.2f;

                bool skid = grounded && !offRoad && slip > 0.55f && _car.SpeedMs > 3f;
                _skids[i].emitting = skid;
                _skids[i].transform.position = contact + hit.normal * 0.02f;
            }

            if (_flameLightT > 0f)
            {
                _flameLightT -= dt;
                _flameLight.intensity = Mathf.Max(0f, _flameLightT) * 40f;
            }
            else if (_flameLight.intensity > 0f) _flameLight.intensity = 0f;
        }

        private void OnCollision(float impact, Vector3 point)
        {
            if (impact < 4f) return;
            _sparks.transform.position = point;
            _sparks.Emit(Mathf.Clamp((int)(impact * 3f), 8, 60));
        }

        private void OnBackfire()
        {
            foreach (var f in _flames) f.Emit(Random.Range(4, 9));
            _flameLightT = 0.08f;
        }

        private void OnDestroy()
        {
            if (_car != null)
            {
                _car.Collided -= OnCollision;
                _car.Backfire -= OnBackfire;
            }
            if (_smoke != null)
                for (int i = 0; i < 4; i++)
                {
                    if (_smoke[i] != null) Destroy(_smoke[i].gameObject);
                    if (_dust[i] != null) Destroy(_dust[i].gameObject);
                }
        }
    }
}
