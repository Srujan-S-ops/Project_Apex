using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Rendering
{
    /// <summary>
    /// Photoscanned CC0 props from Poly Haven (Resources/Props/*.fbx, textures Resources/Textures/pbr/ph_*).
    /// Models are imported as geometry only; a URP PBR material is created once per prop and shared by all
    /// instances (SRP-batcher friendly).
    /// </summary>
    public static class PropLibrary
    {
        public const string Lamp = "street_lamp_01";
        public const string Barrier = "concrete_road_barrier";
        public const string UtilityBox = "utility_box_01";
        public const string Planter = "planter_box_01";
        public const string Hydrant = "fire_hydrant";
        public const string Shrub = "shrub_02";

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public static int Spawned { get; private set; }

        public static bool Available(string id) => Prefab(id) != null;

        private static GameObject Prefab(string id)
        {
            if (Prefabs.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Props/" + id);
            if (p == null) Debug.LogWarning($"[PropLibrary] missing prop model '{id}'");
            Prefabs[id] = p;
            return p;
        }

        private static Material MaterialFor(string id)
        {
            if (Materials.TryGetValue(id, out var m) && m != null) return m;
            if (id == Shrub)
                m = MaterialLibrary.Cutout(MaterialLibrary.Tex("pbr/ph_" + id + "_albedo"), new Color(0.9f, 1f, 0.9f), 0.4f, null, 0.2f);
            else
                m = MaterialLibrary.PBR("ph_" + id, Vector2.one, Color.white, 1f, 1f);
            m.enableInstancing = true;
            Materials[id] = m;
            return m;
        }

        /// <summary>Instantiates a prop (uniform scale) and applies its shared PBR material.</summary>
        public static GameObject Spawn(string id, Transform parent, Vector3 position, Quaternion rotation, float scale = 1f, bool castShadows = true)
        {
            var prefab = Prefab(id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, position, rotation, parent);
            go.name = id;
            go.transform.localScale = Vector3.one * scale;
            var mat = MaterialFor(id);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            Spawned++;
            return go;
        }
    }
}
