using Apex.Core;
using Apex.Track;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Rendering
{
    /// <summary>Race cameras: chase far/near, hood, bumper, trackside TV; speed FOV and shake.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public CameraView View { get; private set; }
        public CarController Target { get; private set; }

        private Camera _cam;
        private TrackWorld _track;
        private GameSettings _settings;
        private Vector3 _vel;
        private Vector3 _smoothFwd = Vector3.forward;
        private float _shake;
        private Vector3 _tvPos;
        private float _tvTimer;
        private bool _lookBack;
        private float _introT = -1f;
        private Vector3 _introStart;

        public void Setup(Camera cam, TrackWorld track, GameSettings settings)
        {
            _cam = cam;
            _track = track;
            _settings = settings;
            View = settings.defaultCamera;
        }

        public void SetTarget(CarController car, bool snap)
        {
            Target = car;
            if (snap && car != null)
            {
                _smoothFwd = car.transform.forward;
                transform.position = car.transform.position - car.transform.forward * 7f + Vector3.up * 2.5f;
                transform.LookAt(car.transform.position + Vector3.up);
            }
        }

        /// <summary>Starts a sweeping intro move toward the chase camera (grid shot).</summary>
        public void PlayIntro()
        {
            _introT = 0f;
            _introStart = Target != null ? Target.transform.position + Target.transform.forward * 7.5f + Target.transform.right * 1.2f + Vector3.up * 1.6f : transform.position;
            _vel = Vector3.zero;
            transform.position = _introStart;
        }

        public void SetView(CameraView v) => View = v;
        public void NextView() => View = (CameraView)(((int)View + 1) % 5);
        public void SetLookBack(bool b) => _lookBack = b;
        public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

        private void LateUpdate()
        {
            if (Target == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime * Time.timeScale, 1e-4f);
            if (Time.timeScale <= 0f) return;
            Transform t = Target.transform;
            Vector3 vel = Target.Body != null ? Target.Body.linearVelocity : Vector3.zero;
            float speed = vel.magnitude;
            Vector3 flatFwd = t.forward; flatFwd.y *= 0.3f; flatFwd.Normalize();
            _smoothFwd = Vector3.Slerp(_smoothFwd, speed > 3f && Target.ForwardSpeed > 0 ? Vector3.Lerp(flatFwd, vel.normalized, 0.25f) : flatFwd, dt * 4.5f);
            Vector3 fwd = _lookBack ? -flatFwd : _smoothFwd;

            float baseFov = _settings.fieldOfView;
            float targetFov = baseFov + Mathf.Clamp01(speed / 80f) * 9f;

            Vector3 desiredPos;
            Quaternion desiredRot;
            switch (View)
            {
                case CameraView.ChaseFar:
                case CameraView.ChaseNear:
                {
                    bool far = View == CameraView.ChaseFar;
                    float dist = far ? 7.6f : 5.6f;
                    float height = far ? 2.5f : 1.75f;
                    dist += Mathf.Clamp(Target.LongitudinalG, -1.5f, 1.5f) * 0.35f;
                    desiredPos = t.position - fwd * dist + Vector3.up * height;
                    Vector3 look = t.position + fwd * 3f + Vector3.up * (far ? 0.9f : 0.8f);
                    // keep the camera above the road / out of walls
                    if (Physics.Linecast(t.position + Vector3.up * 1.2f, desiredPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != Target.Body)
                        desiredPos = hit.point + hit.normal * 0.3f;
                    transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, 0.06f, Mathf.Infinity, dt);
                    desiredRot = Quaternion.LookRotation(look - transform.position, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, dt * 14f);
                    break;
                }
                case CameraView.Hood:
                    transform.position = t.TransformPoint(new Vector3(0f, 1.08f, 0.55f));
                    transform.rotation = _lookBack ? Quaternion.LookRotation(-t.forward, t.up) : Quaternion.LookRotation(t.forward, t.up);
                    targetFov += 4f;
                    break;
                case CameraView.Bumper:
                    transform.position = t.TransformPoint(new Vector3(0f, 0.55f, 2.35f));
                    transform.rotation = _lookBack ? Quaternion.LookRotation(-t.forward, t.up) : Quaternion.LookRotation(t.forward, t.up);
                    targetFov += 6f;
                    break;
                default: // TV
                    UpdateTv(dt, t);
                    targetFov = Mathf.Clamp(1600f / Mathf.Max(8f, Vector3.Distance(transform.position, t.position)), 12f, 55f);
                    break;
            }

            // grid intro blend
            if (_introT >= 0f)
            {
                _introT += dt / 3.2f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_introT));
                transform.position = Vector3.Lerp(_introStart, transform.position, k);
                transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(t.position + Vector3.up - _introStart), transform.rotation, k);
                if (_introT >= 1f) _introT = -1f;
            }

            // speed shake
            if (_settings.cameraShake && View != CameraView.TV)
            {
                float s = Mathf.Clamp01((speed - 45f) / 40f) * 0.012f + _shake;
                transform.position += new Vector3(Mathf.PerlinNoise(Time.time * 23f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 21f) - 0.5f, 0f) * s;
            }
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 0.5f);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, dt * 3f);
        }

        private void UpdateTv(float dt, Transform t)
        {
            _tvTimer -= dt;
            if (_track != null && _track.TvCameraPosts.Count > 0 && (_tvTimer <= 0f || Vector3.Distance(_tvPos, t.position) > 170f))
            {
                // choose the post closest ahead of the car
                float best = float.MaxValue;
                foreach (var p in _track.TvCameraPosts)
                {
                    Vector3 to = p - t.position;
                    float d = to.magnitude;
                    float ahead = Vector3.Dot(to, t.forward);
                    float score = d + (ahead < -20f ? 400f : 0f);
                    if (score < best) { best = score; _tvPos = p; }
                }
                _tvTimer = 2f;
            }
            transform.position = _tvPos;
            Quaternion want = Quaternion.LookRotation(t.position + Vector3.up * 0.6f - _tvPos, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * 6f);
        }
    }
}
