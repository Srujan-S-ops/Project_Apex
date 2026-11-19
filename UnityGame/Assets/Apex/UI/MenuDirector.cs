using System.Collections;
using System.Collections.Generic;
using Apex.Core;
using Apex.Rendering;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apex.UI
{
    /// <summary>Main menu scene: live 3D showroom behind the menu, garage, race setup, settings.</summary>
    public sealed class MenuDirector : MonoBehaviour
    {
        private Transform _root;
        private Transform _turntable;
        private CarVisual _car;
        private ShowroomCamera _camera;
        private ReflectionProbe _probe;
        private MainMenuUI _main;
        private RaceSetupUI _setup;
        private GarageUI _garage;
        private SettingsUI _settings;
        private UIScreen _current;

        private IEnumerator Start()
        {
            SceneReady.Progress = 0f;
            ApexControls.Initialise();
            _root = new GameObject("Showroom").transform;
            SceneLighting.SetupShowroom(_root);
            BuildShowroom();
            yield return null;
            SceneReady.Progress = 0.4f;
            SpawnCar(RaceConfig.Current.carIndex);
            var cam = SceneLighting.CreateCamera(_root, "MenuCamera", 34f);
            cam.farClipPlane = 200f;
            _camera = cam.gameObject.AddComponent<ShowroomCamera>();
            _probe = SceneLighting.AddReflectionProbe(_root, new Vector3(0f, 2f, 0f));
            _probe.size = new Vector3(60f, 20f, 60f);
            _probe.RenderProbe();
            yield return null;
            SceneReady.Progress = 0.7f;
            BuildUI();
            MenuMusic.Play();
            ProfileCache.Hook();
            ProfileCache.Request();
            SceneReady.Progress = 1f;
            yield return null;

            var auto = GameServices.Instance.AutoTest;
            if (auto.Enabled)
            {
                yield return new WaitForSecondsRealtime(auto.Screenshots ? 3f : 1f);
                if (auto.Screenshots) AutoTestCapture.Shot("01_main_menu");
                Show(_garage); _camera.Mode = ShowroomCamera.CameraMode.Garage;
                yield return new WaitForSecondsRealtime(2f);
                if (auto.Screenshots) AutoTestCapture.Shot("02_garage");
                Show(_setup);
                yield return new WaitForSecondsRealtime(1f);
                if (auto.Screenshots) AutoTestCapture.Shot("03_race_setup");
                RaceConfig.Current.mode = auto.Mode;
                RaceConfig.Current.laps = auto.Laps;
                RaceConfig.Current.difficulty = auto.Difficulty;
                RaceConfig.Current.opponents = 4;
                StartRace();
            }
        }

        private void BuildShowroom()
        {
            var floorMat = MaterialLibrary.Lit(new Color(0.03f, 0.03f, 0.035f), 0.2f, 0.93f);
            var floor = new MeshBuilder();
            floor.AddLathe(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new List<Vector2> { new Vector2(0f, 60f), new Vector2(0f, 0.01f) }, 64);
            floor.Build("Floor", _root, floorMat, false);

            var tt = new GameObject("Turntable").transform;
            tt.SetParent(_root, false);
            _turntable = tt;
            var disc = new MeshBuilder();
            disc.AddLathe(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new List<Vector2>
            {
                new Vector2(0f, 3.4f), new Vector2(0.06f, 3.4f), new Vector2(0.06f, 3.4f), new Vector2(0.06f, 0.01f)
            }, 72);
            disc.Build("Disc", tt, MaterialLibrary.Lit(new Color(0.16f, 0.16f, 0.18f), 0.85f, 0.55f));
            var ring = new MeshBuilder();
            ring.AddLathe(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new List<Vector2>
            {
                new Vector2(0.062f, 3.42f), new Vector2(0.062f, 3.3f)
            }, 96);
            ring.Build("Ring", _root, MaterialLibrary.Emissive(UITheme.Red, UITheme.Red, 6f), false);

            // backdrop with light strips
            var wall = new MeshBuilder();
            var rings = new List<Vector3[]>();
            for (int i = 0; i <= 2; i++)
            {
                float y = i * 5f;
                var r = new Vector3[40];
                for (int k = 0; k < 40; k++)
                {
                    float a = Mathf.Lerp(-130f, 130f, k / 39f) * Mathf.Deg2Rad;
                    r[k] = new Vector3(Mathf.Sin(a) * 18f, y, Mathf.Cos(a) * -18f);
                }
                rings.Add(r);
            }
            wall.AddLoft(rings, false, 1f, null, false);
            wall.Build("Backdrop", _root, MaterialLibrary.Lit(new Color(0.035f, 0.035f, 0.04f), 0.1f, 0.4f), false);
            var strips = new MeshBuilder();
            var redStrips = new MeshBuilder();
            for (int k = 0; k < 13; k++)
            {
                float a = Mathf.Lerp(-110f, 110f, k / 12f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(a) * 17.6f, 3.2f, Mathf.Cos(a) * -17.6f);
                (k % 4 == 0 ? redStrips : strips).AddBox(p, new Vector3(0.12f, 6.2f, 0.06f), Quaternion.LookRotation(new Vector3(p.x, 0, p.z).normalized, Vector3.up));
            }
            strips.Build("LightStrips", _root, MaterialLibrary.Emissive(Color.white, Color.white, 2.2f), false);
            redStrips.Build("RedStrips", _root, MaterialLibrary.Emissive(UITheme.Red, UITheme.Red, 4f), false);
            var soft = new MeshBuilder();
            soft.AddBox(new Vector3(0f, 7.5f, 0f), new Vector3(6f, 0.05f, 9f));
            soft.Build("Softbox", _root, MaterialLibrary.Emissive(Color.white, Color.white, 1.4f), false);

            var logoTex = Resources.Load<Texture2D>("UI/logo");
            if (logoTex != null)
            {
                var lq = new MeshBuilder();
                float w = 9f, h = 9f * logoTex.height / logoTex.width;
                Vector3 c = new Vector3(0f, 4.6f, -17.4f);
                lq.AddQuad(c + new Vector3(w / 2, -h / 2, 0), c + new Vector3(w / 2, h / 2, 0), c + new Vector3(-w / 2, h / 2, 0), c + new Vector3(-w / 2, -h / 2, 0),
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                lq.Build("WallLogo", _root, MaterialLibrary.Emissive(new Color(0.03f, 0.03f, 0.035f), Color.white, 1.6f, logoTex), false);
            }
        }

        private void SpawnCar(int index)
        {
            if (_car != null) Destroy(_car.transform.parent.gameObject);
            var spec = CarSpec.Roster[Mathf.Clamp(index, 0, CarSpec.Roster.Length - 1)];
            _car = CarFactory.CreateShowroom(_turntable, spec, CarFactory.PlayerLivery(spec));
            _car.SetShowroomPose(-14f);
            _car.transform.parent.localPosition = Vector3.up * 0.06f;
            if (_probe != null) _probe.RenderProbe();
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("MenuCanvas", 10);
            _main = MainMenuUI.Create(canvas.transform);
            _setup = RaceSetupUI.Create(canvas.transform);
            _garage = GarageUI.Create(canvas.transform);
            _settings = SettingsUI.Create(canvas.transform);

            _main.QuickRace = () => { RaceConfig.Current.mode = GameMode.QuickRace; Show(_setup); };
            _main.TimeTrial = () => { RaceConfig.Current.mode = GameMode.TimeTrial; Show(_setup); };
            _main.Garage = () => { Show(_garage); _camera.Mode = ShowroomCamera.CameraMode.Garage; };
            _main.Settings = () => Show(_settings);
            _main.Exit = () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
            _main.BackRequested = () => { };
            _setup.BackRequested = () => Show(_main);
            _setup.StartRace = StartRace;
            _garage.BackRequested = () => { SpawnCar(RaceConfig.Current.carIndex); BackToMain(); };
            _garage.CarChanged = SpawnCar;
            _garage.Confirmed = BackToMain;
            _settings.Closed = () => Show(_main);
            Show(_main);
        }

        private void BackToMain()
        {
            _camera.Mode = ShowroomCamera.CameraMode.Cinematic;
            Show(_main);
        }

        private void Show(UIScreen s)
        {
            if (_current != null && _current != s) _current.Hide();
            _current = s;
            s.Show();
            if (s == _main) ProfileCache.Request();
        }

        private void StartRace()
        {
            MenuMusic.Stop();
            GameServices.Instance.Loader.Load(SceneNames.Race);
        }

        private void Update()
        {
            if (_turntable != null) _turntable.Rotate(0f, (_camera != null && _camera.Mode == ShowroomCamera.CameraMode.Garage ? 4f : 7f) * Time.deltaTime, 0f);
        }
    }

    /// <summary>Showroom camera: slow cinematic orbit (menu) or user orbit (garage).</summary>
    public sealed class ShowroomCamera : MonoBehaviour
    {
        public enum CameraMode { Cinematic, Garage }
        public CameraMode Mode = CameraMode.Cinematic;
        private float _yaw = 35f, _pitch = 8f, _dist = 9f;
        private float _t;
        private Vector3 _pos;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            Vector3 target;
            if (Mode == CameraMode.Cinematic)
            {
                _yaw += dt * 5f;
                _pitch = Mathf.Lerp(_pitch, 7f + Mathf.Sin(_t * 0.15f) * 5f, dt);
                _dist = Mathf.Lerp(_dist, 8.6f + Mathf.Sin(_t * 0.1f) * 1.2f, dt);
                target = new Vector3(0f, 0.65f, 0f);
            }
            else
            {
                Vector2 d = Vector2.zero;
                if (ApexControls.OrbitHold.IsPressed() || Gamepad.current != null) d = ApexControls.OrbitDelta.ReadValue<Vector2>();
                if (d.sqrMagnitude > 0.01f) { _yaw += d.x * 0.25f; _pitch = Mathf.Clamp(_pitch - d.y * 0.15f, 2f, 30f); }
                else _yaw += dt * 3f;
                var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                _dist = Mathf.Clamp(Mathf.Lerp(_dist, 6.4f, dt * 2f) - scroll * 0.002f, 5f, 9f);
                target = new Vector3(0f, 0.6f, 0f);
            }
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 want = target + rot * new Vector3(0f, 0f, -_dist);
            _pos = Vector3.Lerp(_pos == Vector3.zero ? want : _pos, want, dt * 3f);
            transform.position = _pos;
            transform.rotation = Quaternion.LookRotation(target - _pos, Vector3.up);
            // frame the car to the right of the menu column
            transform.position += transform.right * -(Mode == CameraMode.Cinematic ? 1.9f : 1.5f);
        }
    }
}
