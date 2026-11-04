using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nexo.AI.Infected;
using Nexo.Core;
using Nexo.Interaction;
using Nexo.Narrative;
using Nexo.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Pase de jugabilidad 2026-11-04 (idempotente: borra y rehace lo suyo en cada ejecución).
    /// - PF_Medkit (caja blanca con cruz roja, emisiva) y sus materiales.
    /// - PF_Player: PlayerCheckpoints (caídas del mundo y puntos de control).
    /// - Cada piso: raíz «Fxx_GameplayPass» con 3 puntos de control, 2 botiquines y (Piso 12) el parche del hueco del
    ///   ascensor. En el Piso 1 nada se coloca a menos de 10 m del BioScanner ni de la zona del giro.
    /// - Botín (InfectedLootDrop) en todos los puntos de aparición de infectados.
    /// Menú Nexo/Gameplay Pass/Build o -executeMethod Nexo.EditorTools.NexoGameplayPassBuilder.BuildBatch.
    /// </summary>
    public static class NexoGameplayPassBuilder
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Systems/PF_Player.prefab";
        private const string ElenaServicePrefab = "Assets/_Project/Prefabs/Systems/PF_ElenaService.prefab";
        private const string HudPrefab = "Assets/_Project/Prefabs/Systems/PF_HUD.prefab";
        private const string ElenaConfigPath = "Assets/_Project/Data/ElenaConfig.asset";
        private const string MedkitPrefab = "Assets/_Project/Prefabs/Interactables/PF_Medkit.prefab";
        private const string MaterialsFolder = "Assets/_Project/Materials/Gameplay";
        private const string MedkitClip = "Assets/_Project/Audio/SFX/SFX_Medkit_Pickup.wav";
        private const int InteractableLayer = 10;
        private const int IgnoreRaycastLayer = 2;
        private const int EnvironmentLayer = 11;
        private const float TwistExclusion = 10f;

        private static readonly (string path, string prefix)[] Floors =
        {
            ("Assets/_Project/Scenes/Floors/Floor12_Offices.unity", "F12"),
            ("Assets/_Project/Scenes/Floors/Floor06_Labs.unity", "F06"),
            ("Assets/_Project/Scenes/Floors/Floor01_Lobby.unity", "F01")
        };

        [MenuItem("Nexo/Gameplay Pass/Build (botiquines, puntos de control, caídas)")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        public static void BuildBatch()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (System.Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void BuildAll()
        {
            GameObject medkit = BuildMedkitPrefab();
            GameObject knifePickup = BuildKnifePrefab();
            ConfigurePlayerPrefab();
            ConfigureElenaService();
            ConfigureHud();
            foreach ((string path, string prefix) in Floors)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                BuildFloor(scene, prefix, medkit, knifePickup);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[NexoGameplayPassBuilder] Pase de jugabilidad aplicado a PF_Player y a los 3 pisos.");
        }

        // ---------- Prefabs ----------

        private static GameObject BuildMedkitPrefab()
        {
            Directory.CreateDirectory(MaterialsFolder);
            Material white = EmissiveMaterial("MAT_Medkit_White", new Color(0.93f, 0.94f, 0.95f), new Color(0.22f, 0.23f, 0.24f));
            Material red = EmissiveMaterial("MAT_Medkit_Red", new Color(0.85f, 0.06f, 0.05f), new Color(0.9f, 0.05f, 0.04f));

            var root = new GameObject("PF_Medkit") { layer = InteractableLayer };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.16f, 0f);
            trigger.size = new Vector3(0.7f, 0.5f, 0.6f);
            var pickup = root.AddComponent<MedkitPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("_pickupClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MedkitClip);
            so.ApplyModifiedPropertiesWithoutUndo();

            Part(root, "Case", new Vector3(0f, 0.11f, 0f), new Vector3(0.44f, 0.22f, 0.3f), white);
            Part(root, "Lid", new Vector3(0f, 0.225f, 0f), new Vector3(0.45f, 0.012f, 0.31f), white);
            Part(root, "Cross_Top_H", new Vector3(0f, 0.233f, 0f), new Vector3(0.22f, 0.006f, 0.065f), red);
            Part(root, "Cross_Top_V", new Vector3(0f, 0.233f, 0f), new Vector3(0.065f, 0.006f, 0.2f), red);
            Part(root, "Cross_Front_H", new Vector3(0f, 0.11f, 0.152f), new Vector3(0.14f, 0.045f, 0.006f), red);
            Part(root, "Cross_Front_V", new Vector3(0f, 0.11f, 0.152f), new Vector3(0.045f, 0.14f, 0.006f), red);
            Part(root, "Handle", new Vector3(0f, 0.25f, 0f), new Vector3(0.16f, 0.03f, 0.03f), red);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MedkitPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildKnifePrefab()
        {
            var root = new GameObject("PF_KnifePickup") { layer = InteractableLayer };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.08f, 0f);
            box.size = new Vector3(0.35f, 0.2f, 0.5f);
            root.AddComponent<KnifePickup>();
            GameObject model = PlayerKnife.BuildKnifeModel(root.transform,
                EmissiveMaterial("MAT_Knife_Blade", new Color(0.75f, 0.77f, 0.8f), new Color(0.25f, 0.27f, 0.3f)),
                EmissiveMaterial("MAT_Knife_Handle", new Color(0.12f, 0.1f, 0.09f), Color.black));
            model.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Prefabs/Interactables/PF_KnifePickup.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = parent.layer;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static Material EmissiveMaterial(string name, Color albedo, Color emission)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Standard");
            mat.SetColor("_Color", albedo);
            mat.SetFloat("_Glossiness", 0.35f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                if (!contents.TryGetComponent(out PlayerCheckpoints checkpoints)) checkpoints = contents.AddComponent<PlayerCheckpoints>();
                var so = new SerializedObject(checkpoints);
                so.FindProperty("_movement").objectReferenceValue = contents.GetComponent<PlayerMovement>();
                so.FindProperty("_health").objectReferenceValue = contents.GetComponent<PlayerHealth>();
                so.ApplyModifiedPropertiesWithoutUndo();

                // Empujón [G] y cuchillo [X]
                AudioSource audio = contents.GetComponentInChildren<AudioSource>();
                AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/SFX_Infected_HitFlesh.wav");
                if (!contents.TryGetComponent(out PlayerShove shove)) shove = contents.AddComponent<PlayerShove>();
                var sso = new SerializedObject(shove);
                sso.FindProperty("_audio").objectReferenceValue = audio;
                sso.FindProperty("_shoveClip").objectReferenceValue = hitClip;
                sso.ApplyModifiedPropertiesWithoutUndo();
                if (!contents.TryGetComponent(out PlayerKnife knife)) knife = contents.AddComponent<PlayerKnife>();
                var kso = new SerializedObject(knife);
                kso.FindProperty("_audio").objectReferenceValue = audio;
                kso.FindProperty("_hitClip").objectReferenceValue = hitClip;
                kso.FindProperty("_bladeMaterial").objectReferenceValue = EmissiveMaterial("MAT_Knife_Blade", new Color(0.75f, 0.77f, 0.8f), new Color(0.08f, 0.08f, 0.09f));
                kso.FindProperty("_handleMaterial").objectReferenceValue = EmissiveMaterial("MAT_Knife_Handle", new Color(0.12f, 0.1f, 0.09f), Color.black);
                kso.ApplyModifiedPropertiesWithoutUndo();
                if (contents.TryGetComponent(out PlayerStatePersistence persistence))
                {
                    var pso = new SerializedObject(persistence);
                    pso.FindProperty("_knife").objectReferenceValue = knife;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>PF_ElenaService: voz de Elena (ElenaVoicePlayer + AudioSource 2D) junto a ElenaAIService.</summary>
        private static void ConfigureElenaService()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ElenaServicePrefab);
            try
            {
                if (!contents.TryGetComponent(out AudioSource source)) source = contents.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                if (!contents.TryGetComponent(out Nexo.AI.Elena.ElenaVoicePlayer player)) player = contents.AddComponent<Nexo.AI.Elena.ElenaVoicePlayer>();
                var so = new SerializedObject(player);
                so.FindProperty("_config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Nexo.AI.Elena.ElenaConfig>(ElenaConfigPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, ElenaServicePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>PF_HUD: cuadro de texto para escribir a Elena ([T]).</summary>
        private static void ConfigureHud()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(HudPrefab);
            try
            {
                if (!contents.TryGetComponent(out Nexo.UI.ElenaTextInput _)) contents.AddComponent<Nexo.UI.ElenaTextInput>();
                if (!contents.TryGetComponent(out Nexo.UI.NoteReader reader)) reader = contents.AddComponent<Nexo.UI.NoteReader>();
                if (!contents.TryGetComponent(out AudioSource paperAudio)) paperAudio = contents.AddComponent<AudioSource>();
                paperAudio.playOnAwake = false;
                paperAudio.spatialBlend = 0f;
                var rso = new SerializedObject(reader);
                rso.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/UI/Fonts/IBMPlexMono-Medium.ttf");
                rso.FindProperty("_audio").objectReferenceValue = paperAudio;
                rso.FindProperty("_paperClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/SFX_Paper_Rustle.wav");
                rso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, HudPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------- Pisos ----------

        private static void BuildFloor(Scene scene, string prefix, GameObject medkit, GameObject knifePickup)
        {
            string rootName = prefix + "_GameplayPass";
            foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == rootName).ToArray()) Object.DestroyImmediate(old);
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            FloorMetadata meta = Object.FindFirstObjectByType<FloorMetadata>();
            Vector3 spawn = meta != null && meta.PlayerSpawn != null ? meta.PlayerSpawn.position : Vector3.zero;
            List<Bounds> exclusions = TwistExclusions();

            if (prefix == "F12") BuildElevatorPatch(root.transform);

            // Anclas: volúmenes de historia, notas, terminales, munición y escondites
            var anchors = new List<Vector3>();
            foreach (StorySequence s in Object.FindObjectsByType<StorySequence>(FindObjectsSortMode.None))
                if (s.GetComponent<Collider>() != null) anchors.Add(s.transform.position);
            foreach (LoreNote n in Object.FindObjectsByType<LoreNote>(FindObjectsSortMode.None)) anchors.Add(n.transform.position);
            foreach (SecurityTerminal t in Object.FindObjectsByType<SecurityTerminal>(FindObjectsSortMode.None)) anchors.Add(t.transform.position);
            foreach (AmmoPickup a in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None)) anchors.Add(a.transform.position);
            foreach (HidingSpot h in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None)) anchors.Add(h.transform.position);

            List<Vector3> navAnchors = anchors
                .Select(a => Snap(a, spawn.y))
                .Where(p => p.HasValue && !Excluded(p.Value, exclusions))
                .Select(p => p.Value)
                .ToList();

            // 3 puntos de control repartidos (lejos del inicio y entre sí)
            var cpRoot = new GameObject(prefix + "_Checkpoints").transform;
            cpRoot.SetParent(root.transform, false);
            List<Vector3> checkpoints = FarthestPoints(navAnchors, new List<Vector3> { spawn }, 3, 8f);
            for (int i = 0; i < checkpoints.Count; i++)
            {
                Vector3 p = checkpoints[i];
                var go = new GameObject($"{prefix}_Checkpoint_{i + 1:00}") { layer = IgnoreRaycastLayer };
                go.transform.SetParent(cpRoot, false);
                Vector3 dir = p - spawn; dir.y = 0f;
                go.transform.SetPositionAndRotation(p, dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 1.2f, 0f);
                box.size = new Vector3(3.5f, 2.4f, 3.5f);
                var cp = go.AddComponent<CheckpointTrigger>();
                var so = new SerializedObject(cp);
                so.FindProperty("_label").stringValue = $"Piso {(meta != null ? meta.FloorNumber : 0)} · control {i + 1}";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2 botiquines, desplazados de su ancla para no tapar notas ni munición
            var mkRoot = new GameObject(prefix + "_Medkits").transform;
            mkRoot.SetParent(root.transform, false);
            var avoid = new List<Vector3>(checkpoints) { spawn };
            List<Vector3> medkits = FarthestPoints(navAnchors, avoid, 2, 6f);
            for (int i = 0; i < medkits.Count; i++)
            {
                Vector3 p = OffsetOnNavMesh(medkits[i], 1.1f, spawn.y) ?? medkits[i];
                var mk = (GameObject)PrefabUtility.InstantiatePrefab(medkit, scene);
                mk.name = $"{prefix}_Medkit_{i + 1:00}";
                mk.transform.SetParent(mkRoot, true);
                mk.transform.SetPositionAndRotation(p + Vector3.up * 0.02f, Quaternion.Euler(0f, 25f + 70f * i, 0f));
            }

            // Botín en todos los infectados (botiquín al 35 %)
            foreach (InfectedSpawnPoint sp in Object.FindObjectsByType<InfectedSpawnPoint>(FindObjectsSortMode.None))
            {
                if (!sp.TryGetComponent(out InfectedLootDrop loot)) loot = sp.gameObject.AddComponent<InfectedLootDrop>();
                var so = new SerializedObject(loot);
                so.FindProperty("_chanceDrop").objectReferenceValue = medkit;
                so.FindProperty("_chance").floatValue = 0.35f;
                // Cuchillo: lo suelta siempre el patrullero principal del Piso 12
                SerializedProperty guaranteed = so.FindProperty("_guaranteed");
                guaranteed.arraySize = prefix == "F12" && sp.name == "InfectedSpawn" ? 1 : 0;
                if (guaranteed.arraySize == 1) guaranteed.GetArrayElementAtIndex(0).objectReferenceValue = knifePickup;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loot);
            }

            // Notas en papel: resalte al mirarlas
            foreach (LoreNote n in Object.FindObjectsByType<LoreNote>(FindObjectsSortMode.None))
                if (!n.TryGetComponent(out InteractableHighlight _)) n.gameObject.AddComponent<InteractableHighlight>();

            Debug.Log($"[NexoGameplayPassBuilder] {scene.name}: anclas {anchors.Count}, válidas {navAnchors.Count}; exclusiones " +
                      string.Join(" ", exclusions.Select(e => $"[{F(e.min)}..{F(e.max)}]")));
            Debug.Log($"[NexoGameplayPassBuilder] {scene.name}: {checkpoints.Count} puntos de control, {medkits.Count} botiquines, " +
                      $"{Object.FindObjectsByType<InfectedSpawnPoint>(FindObjectsSortMode.None).Length} infectados con botín. " +
                      $"Controles en {string.Join(" ", checkpoints.Select(F))}; botiquines en {string.Join(" ", medkits.Select(F))}");
        }

        /// <summary>
        /// Piso 12: el umbral de los ascensores (x 2,4-5,4; z ≈ -14,1) no tiene collider y deja caer 22,8 m por el hueco.
        /// Se cierra con una losa invisible (capa Environment) al nivel del suelo; NexoFloorHoleScanner lo verifica.
        /// </summary>
        private static void BuildElevatorPatch(Transform root)
        {
            var patch = new GameObject("F12_FallSafety").transform;
            patch.SetParent(root, false);

            var slab = new GameObject("Patch_ElevatorThreshold") { layer = EnvironmentLayer };
            slab.transform.SetParent(patch, false);
            slab.transform.position = new Vector3(4.15f, 23.8f, -13.65f);
            var slabBox = slab.AddComponent<BoxCollider>();
            slabBox.size = new Vector3(5.2f, 0.2f, 2.1f);
        }

        private static List<Bounds> TwistExclusions()
        {
            var list = new List<Bounds>();
            foreach (BioScanner b in Object.FindObjectsByType<BioScanner>(FindObjectsSortMode.None))
                list.Add(new Bounds(b.transform.position, Vector3.one * TwistExclusion * 2f));
            // PlotTwistSafeZone abarca todo el vestíbulo y PlotTwistManager está en el origen: la zona del giro
            // que no se toca es el entorno del BioScanner (escáner, luces del giro y el recorrido final)
            return list;
        }

        private static bool Excluded(Vector3 p, List<Bounds> exclusions)
        {
            foreach (Bounds b in exclusions)
            {
                Vector3 q = new Vector3(p.x, b.center.y, p.z);
                if (b.Contains(q)) return true;
            }
            return false;
        }

        private static Vector3? Snap(Vector3 p, float floorY)
        {
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) return null;
            if (Mathf.Abs(hit.position.y - floorY) > 1.2f) return null;
            return hit.position;
        }

        private static Vector3? OffsetOnNavMesh(Vector3 p, float distance, float floorY)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
                Vector3? s = Snap(q, floorY);
                if (s.HasValue && (s.Value - q).sqrMagnitude < 0.09f) return s;
            }
            return null;
        }

        /// <summary>Muestreo por punto más lejano: elige <paramref name="count"/> puntos lo más separados posible de los ya usados.</summary>
        public static List<Vector3> FarthestPoints(List<Vector3> candidates, List<Vector3> used, int count, float minDistance)
        {
            var chosen = new List<Vector3>();
            var taken = new List<Vector3>(used);
            for (int k = 0; k < count; k++)
            {
                float best = -1f;
                Vector3 bestP = default;
                foreach (Vector3 c in candidates)
                {
                    float d = taken.Count == 0 ? float.MaxValue : taken.Min(t => Vector3.Distance(t, c));
                    if (d > best) { best = d; bestP = c; }
                }
                if (best < minDistance) break;
                chosen.Add(bestP);
                taken.Add(bestP);
            }
            return chosen;
        }

        private static string F(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
    }
}
