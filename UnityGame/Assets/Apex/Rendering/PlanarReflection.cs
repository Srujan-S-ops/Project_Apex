using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Apex.Rendering
{
    /// <summary>
    /// Renders a mirrored view of the scene into a RenderTexture for a horizontal reflective plane.
    /// The mirror camera is placed at the reflected position with reflected forward/up vectors (a proper
    /// rotation, so no culling inversion is needed); the resulting image is horizontally mirrored and the
    /// shader samples it with 1-u. An oblique near plane clips everything below the floor.
    /// </summary>
    public sealed class PlanarReflection : MonoBehaviour
    {
        public Camera Source;
        public float PlaneHeight;
        public int Resolution = 1024;
        public Material Target;

        private Camera _mirror;
        private RenderTexture _rt;
        private static readonly int ReflectionTex = Shader.PropertyToID("_ReflectionTex");

        private void Start()
        {
            var go = new GameObject("MirrorCamera");
            go.transform.SetParent(transform, false);
            _mirror = go.AddComponent<Camera>();
            _mirror.enabled = true;
            _mirror.depth = (Source != null ? Source.depth : 0) - 1;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            float aspect = Source != null ? Source.aspect : 16f / 9f;
            _rt = new RenderTexture(Resolution, Mathf.RoundToInt(Resolution / aspect), 24, RenderTextureFormat.DefaultHDR) { name = "PlanarReflectionRT" };
            _mirror.targetTexture = _rt;
            // the mirror must not render the reflection layer itself
            _mirror.cullingMask = ~(1 << 2);   // layer 2 = Ignore Raycast hosts the reflection quad
            if (Target != null) Target.SetTexture(ReflectionTex, _rt);
        }

        private void LateUpdate()
        {
            if (Source == null || _mirror == null) return;
            Transform s = Source.transform;
            Vector3 p = s.position;
            Vector3 mp = new Vector3(p.x, 2f * PlaneHeight - p.y, p.z);
            Vector3 f = s.forward, u = s.up;
            Vector3 mf = new Vector3(f.x, -f.y, f.z), mu = new Vector3(u.x, -u.y, u.z);
            _mirror.transform.SetPositionAndRotation(mp, Quaternion.LookRotation(mf, mu));
            _mirror.fieldOfView = Source.fieldOfView;
            _mirror.nearClipPlane = Source.nearClipPlane;
            _mirror.farClipPlane = Source.farClipPlane;
            _mirror.aspect = Source.aspect;
            _mirror.ResetProjectionMatrix();
            // oblique clip plane at the floor, in mirror-camera space
            Vector4 planeWS = new Vector4(0f, 1f, 0f, -PlaneHeight - 0.01f);
            Matrix4x4 w2c = _mirror.worldToCameraMatrix;
            Vector4 planeCS = w2c.inverse.transpose * planeWS;
            _mirror.projectionMatrix = _mirror.CalculateObliqueMatrix(planeCS);
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
