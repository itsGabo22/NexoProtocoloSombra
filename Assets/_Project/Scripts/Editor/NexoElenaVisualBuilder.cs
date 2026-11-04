using System.IO;
using System.Linq;
using Nexo.AI.Elena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Nexo › Elena › Rebuild Visual: construye PF_ElenaVisual (figura de Elena, niña de 9 años, ~1,30 m) y la coloca tras el
    /// cristal de la sala de servidores del Piso 12, sustituyendo las primitivas azules del marcador (Elena_Placeholder).
    /// Modelo intercambiable sin código: si hay un FBX o prefab en Assets/_Project/Models/Characters/Elena/ se usa ese
    /// (con sus materiales); si no, se usa el humanoide del proyecto (Ch33) escalado a la altura de una niña, con tinte
    /// propio (chaqueta gris clara) y una melena oscura añadida. Ver docs/ELENA_VISUAL.md.
    /// La figura solo está mientras Elena espera tras el cristal; al salir de la sala desaparece (ElenaVisualIdle).
    /// -executeMethod Nexo.EditorTools.NexoElenaVisualBuilder.RebuildBatch
    /// </summary>
    public static class NexoElenaVisualBuilder
    {
        public const string DropFolder = "Assets/_Project/Models/Characters/Elena";
        public const string StandInModel = "Assets/_Project/Models/Characters/Ch33_nonPBR1.fbx";
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/PF_ElenaVisual.prefab";
        private const string Floor12 = "Assets/_Project/Scenes/Floors/Floor12_Offices.unity";
        private const float TargetHeight = 1.30f;

        [MenuItem("Nexo/Elena/Rebuild Visual (modelo intercambiable)")]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Rebuild());
        }

        public static void RebuildBatch()
        {
            try { Debug.Log(Rebuild()); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static string Rebuild()
        {
            Directory.CreateDirectory(DropFolder);
            string source = FindDroppedModel() ?? StandInModel;
            bool standIn = source == StandInModel;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (model == null) return $"[NexoElenaVisualBuilder] No se encontró el modelo {source}";

            var root = new GameObject("PF_ElenaVisual");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Animator a in instance.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;

            // Altura de una niña de 9 años, con los pies en el origen
            Bounds b = RendererBounds(instance);
            float scale = b.size.y > 0.01f ? TargetHeight / b.size.y : 0.72f;
            instance.transform.localScale = Vector3.one * scale;
            b = RendererBounds(instance);
            instance.transform.localPosition = new Vector3(0f, -b.min.y, 0f);

            if (standIn) TintStandIn(instance);

            root.AddComponent<ElenaVisualIdle>();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            string placed = PlaceInFloor12(prefab);
            return $"[NexoElenaVisualBuilder] PF_ElenaVisual desde {(standIn ? "Ch33 (sustituto, escala " + scale.ToString("0.00") + ")" : source)}; {placed}";
        }

        private static string FindDroppedModel()
        {
            if (!AssetDatabase.IsValidFolder(DropFolder)) return null;
            return AssetDatabase.FindAssets("t:GameObject", new[] { DropFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .FirstOrDefault();
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Sustituto: chaqueta gris clara, piel y una melena larga oscura para que no se lea como el adulto.</summary>
        private static void TintStandIn(GameObject instance)
        {
            Material jacket = NexoGameplayPassBuilder.EmissiveMaterial("MAT_Elena_Jacket", new Color(0.72f, 0.74f, 0.78f), new Color(0.04f, 0.05f, 0.06f));
            Material hair = NexoGameplayPassBuilder.EmissiveMaterial("MAT_Elena_Hair", new Color(0.07f, 0.05f, 0.04f), Color.black);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = jacket;
                r.sharedMaterials = mats;
            }

            Transform head = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Head"));
            if (head == null) return;
            Bounds b = RendererBounds(instance);
            float h = b.size.y;
            Vector3 headPos = head.position;
            Vector3 back = -instance.transform.forward;

            AddHair(head, "Hair_Cap", headPos + Vector3.up * h * 0.045f + back * h * 0.012f, new Vector3(0.16f, 0.15f, 0.17f) * (h / 1.3f), hair, PrimitiveType.Sphere);
            AddHair(head, "Hair_Long", headPos - Vector3.up * h * 0.07f + back * h * 0.05f, new Vector3(0.15f, 0.2f, 0.07f) * (h / 1.3f), hair, PrimitiveType.Capsule);
        }

        private static void AddHair(Transform head, string name, Vector3 worldPos, Vector3 worldScale, Material mat, PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.position = worldPos;
            go.transform.localScale = worldScale;
            go.transform.SetParent(head, true);
        }

        /// <summary>Coloca la figura en el marcador de Elena del Piso 12 (tras el cristal) y oculta sus primitivas.</summary>
        private static string PlaceInFloor12(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(Floor12, OpenSceneMode.Single);
            ElenaCompanion companion = Object.FindAnyObjectByType<ElenaCompanion>(FindObjectsInactive.Include);
            if (companion == null) return "sin ElenaCompanion en el Piso 12 (no se coloca)";

            foreach (ElenaVisualIdle old in companion.GetComponentsInChildren<ElenaVisualIdle>(true)) Object.DestroyImmediate(old.gameObject);
            foreach (string part in new[] { "Body", "Head", "Legs" })
            {
                Transform t = companion.transform.Find(part);
                if (t != null && t.TryGetComponent(out MeshRenderer mr)) mr.enabled = false;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.transform.SetParent(companion.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var so = new SerializedObject(visual.GetComponent<ElenaVisualIdle>());
            so.FindProperty("_companion").objectReferenceValue = companion;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"colocada en {companion.name} ({companion.transform.position.x:0.0}, {companion.transform.position.y:0.0}, {companion.transform.position.z:0.0}) del Piso 12";
        }
    }
}
