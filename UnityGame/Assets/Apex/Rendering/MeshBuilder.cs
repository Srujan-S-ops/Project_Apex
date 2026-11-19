using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Rendering
{
    /// <summary>
    /// Accumulates procedural geometry (single material) and produces a Mesh.
    /// Used by the vehicle and track generators.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(1024);
        public readonly List<Vector3> Normals = new List<Vector3>(1024);
        public readonly List<Vector2> Uvs = new List<Vector2>(1024);
        public readonly List<Color> Colors = new List<Color>(0);
        public readonly List<int> Triangles = new List<int>(3072);

        public int VertexCount => Vertices.Count;

        /// <summary>Optional transform applied to everything added (local→builder space).</summary>
        public Matrix4x4 Transform = Matrix4x4.identity;

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            Vertices.Add(Transform.MultiplyPoint3x4(p));
            Normals.Add(Transform.MultiplyVector(n).normalized);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
        }

        /// <summary>Quad with flat normal; vertices in clockwise order seen from the front.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = AddVertex(a, n, uvA);
            AddVertex(b, n, uvB);
            AddVertex(c, n, uvC);
            AddVertex(d, n, uvD);
            AddTriangle(i, i + 1, i + 2);
            AddTriangle(i, i + 2, i + 3);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        /// <summary>Axis-aligned (in local frame given by rotation) box.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3[] c =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            for (int i = 0; i < 8; i++) c[i] = center + rotation * c[i];
            float ux = size.x * uvScale, uy = size.y * uvScale, uz = size.z * uvScale;
            // -Z face
            AddQuad(c[0], c[3], c[2], c[1], new Vector2(0, 0), new Vector2(0, uy), new Vector2(ux, uy), new Vector2(ux, 0));
            // +Z face
            AddQuad(c[5], c[6], c[7], c[4], new Vector2(0, 0), new Vector2(0, uy), new Vector2(ux, uy), new Vector2(ux, 0));
            // -X face
            AddQuad(c[4], c[7], c[3], c[0], new Vector2(0, 0), new Vector2(0, uy), new Vector2(uz, uy), new Vector2(uz, 0));
            // +X face
            AddQuad(c[1], c[2], c[6], c[5], new Vector2(0, 0), new Vector2(0, uy), new Vector2(uz, uy), new Vector2(uz, 0));
            // +Y face
            AddQuad(c[3], c[7], c[6], c[2], new Vector2(0, 0), new Vector2(0, uz), new Vector2(ux, uz), new Vector2(ux, 0));
            // -Y face
            AddQuad(c[4], c[0], c[1], c[5], new Vector2(0, 0), new Vector2(0, uz), new Vector2(ux, uz), new Vector2(ux, 0));
        }

        public void AddBox(Vector3 center, Vector3 size) => AddBox(center, size, Quaternion.identity);

        /// <summary>
        /// Surface of revolution around the local X axis. Profile points are (x = axial, y = radius).
        /// </summary>
        public void AddLathe(Vector3 center, Quaternion rotation, IList<Vector2> profile, int segments, bool smooth = true)
        {
            int start = Vertices.Count;
            int rows = profile.Count;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int r = 0; r < rows; r++)
                {
                    Vector2 p = profile[r];
                    Vector3 pos = new Vector3(p.x, p.y * ca, p.y * sa);
                    // profile tangent → normal
                    Vector2 prev = profile[Mathf.Max(0, r - 1)];
                    Vector2 next = profile[Mathf.Min(rows - 1, r + 1)];
                    // duplicated consecutive points mark a hard edge
                    Vector2 t;
                    if ((p - prev).sqrMagnitude < 1e-10f) t = next - p;
                    else if ((next - p).sqrMagnitude < 1e-10f) t = p - prev;
                    else t = next - prev;
                    t = t.sqrMagnitude > 1e-12f ? t.normalized : Vector2.right;
                    Vector2 n2 = new Vector2(-t.y, t.x);
                    Vector3 n = new Vector3(n2.x, n2.y * ca, n2.y * sa);
                    AddVertex(center + rotation * pos, rotation * n, new Vector2(s / (float)segments, r / (float)(rows - 1)));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    int i0 = start + s * rows + r;
                    int i1 = start + (s + 1) * rows + r;
                    AddTriangle(i0, i1 + 1, i0 + 1);
                    AddTriangle(i0, i1, i1 + 1);
                }
            }
        }

        /// <summary>Cylinder between two points.</summary>
        public void AddCylinder(Vector3 from, Vector3 to, float radius, int segments = 10, bool caps = true)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-5f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.right, dir / len);
            var profile = caps
                ? new List<Vector2> { new Vector2(0, 0), new Vector2(0, radius), new Vector2(0, radius), new Vector2(len, radius), new Vector2(len, radius), new Vector2(len, 0) }
                : new List<Vector2> { new Vector2(0, radius), new Vector2(len, radius) };
            AddLathe(from, rot, profile, segments);
        }

        public void AddSphere(Vector3 center, Vector3 radii, int segments = 12)
        {
            var profile = new List<Vector2>();
            int rings = Mathf.Max(4, segments / 2);
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings * Mathf.PI;
                profile.Add(new Vector2(-Mathf.Cos(t), Mathf.Sin(t)));
            }
            int start = Vertices.Count;
            AddLathe(Vector3.zero, Quaternion.identity, profile, segments);
            for (int i = start; i < Vertices.Count; i++)
            {
                Vector3 v = Vertices[i];
                Vertices[i] = center + new Vector3(v.x * radii.x, v.y * radii.y, v.z * radii.z);
                Vector3 n = Normals[i];
                Normals[i] = new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z).normalized;
            }
        }

        /// <summary>
        /// Lofts closed (or open) rings of equal vertex count. rings[i][j]; u runs along j, v along i.
        /// </summary>
        public void AddLoft(IList<Vector3[]> rings, bool closedRing, float vScale = 1f, IList<float> vCoords = null, bool flip = false)
        {
            int start = Vertices.Count;
            int n = rings[0].Length;
            int cols = closedRing ? n + 1 : n;
            for (int i = 0; i < rings.Count; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int jj = j % n;
                    Vector3 p = rings[i][jj];
                    Vector3 along = rings[Mathf.Min(i + 1, rings.Count - 1)][jj] - rings[Mathf.Max(i - 1, 0)][jj];
                    int jPrev = closedRing ? (jj - 1 + n) % n : Mathf.Max(jj - 1, 0);
                    int jNext = closedRing ? (jj + 1) % n : Mathf.Min(jj + 1, n - 1);
                    Vector3 around = rings[i][jNext] - rings[i][jPrev];
                    Vector3 nrm = Vector3.Cross(around, along).normalized;
                    if (flip) nrm = -nrm;
                    float v = vCoords != null ? vCoords[i] : i * vScale;
                    AddVertex(p, nrm, new Vector2(j / (float)(cols - 1), v));
                }
            }
            for (int i = 0; i < rings.Count - 1; i++)
            {
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = start + i * cols + j;
                    int b = a + 1;
                    int c = a + cols;
                    int d = c + 1;
                    if (!flip) { AddTriangle(a, b, d); AddTriangle(a, d, c); }
                    else { AddTriangle(a, d, b); AddTriangle(a, c, d); }
                }
            }
        }

        /// <summary>Triangle-fan cap over a ring (ring assumed roughly planar & convex-ish).</summary>
        public void AddCap(Vector3[] ring, Vector3 normal, bool reverse)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in ring) center += p;
            center /= ring.Length;
            int c = AddVertex(center, normal, new Vector2(0.5f, 0.5f));
            int start = Vertices.Count;
            foreach (var p in ring) AddVertex(p, normal, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < ring.Length; i++)
            {
                int a = start + i, b = start + (i + 1) % ring.Length;
                if (reverse) AddTriangle(c, b, a); else AddTriangle(c, a, b);
            }
        }

        public Mesh ToMesh(string name = "ProceduralMesh")
        {
            var mesh = new Mesh { name = name };
            if (Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            if (Colors.Count == Vertices.Count) mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Creates a child GameObject carrying the built mesh.</summary>
        public GameObject Build(string name, Transform parent, Material material, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
