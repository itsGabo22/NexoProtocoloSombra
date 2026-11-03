using System.Collections.Generic;
using System.IO;
using Nexo.AI.Infected;
using Nexo.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Ambientación del vestíbulo (Piso 1) SIN tocar el giro final: no modifica BioScanner, PlotTwistManager, sus luces,
    /// la iluminación de la escena ni ningún objeto existente. Todo cuelga de la raíz F01_Ambience, que lleva
    /// PlotTwistSafeZone y se desactiva entera al empezar el giro. Volver a ejecutarla la reconstruye.
    /// Menú: Nexo/Floor01/Construir ambientación del vestíbulo (sin tocar el giro).
    /// </summary>
    public static class NexoFloor01AmbienceBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Floors/Floor01_Lobby.unity";
        private const string RootName = "F01_Ambience";
        private const string MaterialFolder = "Assets/_Project/Materials/Floor01";
        private const string NavMeshFolder = "Assets/_Project/Scenes/Floors/Floor01_Lobby";
        private const string InfectedPrefab = "Assets/_Project/Prefabs/Characters/PF_Infected_Variant.prefab";

        private static readonly Color Emergency = new Color(1f, 0.13f, 0.08f);
        private static Scene _scene;
        private static AudioMixer _mixer;

        [MenuItem("Nexo/Floor01/Construir ambientación del vestíbulo (sin tocar el giro)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[NexoFloor01AmbienceBuilder] Sal del modo Play antes de construir.");
                return;
            }

            _scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !_scene.isLoaded;
            if (openedHere) _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Scene previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_scene);
            _mixer = FindMixer();

            foreach (GameObject go in _scene.GetRootGameObjects()) if (go.name == RootName) Object.DestroyImmediate(go);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, _scene);
            root.AddComponent<PlotTwistSafeZone>();

            Transform lighting = Group(root, "F01_Lighting");
            Transform audio = Group(root, "F01_Audio");
            Transform interactables = Group(root, "F01_Interactables");
            Transform story = Group(root, "F01_Story");
            Transform waypoints = Group(root, "F01_Waypoints");

            BuildLightingAndStorm(lighting, audio);
            BuildNotesAndTerminal(interactables);
            BuildScares(story, audio);
            BuildInfected(waypoints);
            BakeNavMesh(root);

            EditorSceneManager.MarkSceneDirty(_scene);
            EditorSceneManager.SaveScene(_scene);
            if (previousActive.IsValid() && previousActive.isLoaded && previousActive != _scene) SceneManager.SetActiveScene(previousActive);
            if (openedHere) EditorSceneManager.CloseScene(_scene, true);
            Debug.Log("[NexoFloor01AmbienceBuilder] Vestíbulo ambientado y guardado (giro final intacto).");
        }

        // ---------------- Luz y tormenta ----------------

        private static void BuildLightingAndStorm(Transform lighting, Transform audio)
        {
            // Emergencias rojas sobre los ascensores bloqueados (luces nuevas: las del giro no se tocan)
            var surge = new List<Light>();
            foreach (float z in new[] { -5f, 0f, 5f })
            {
                Light l = AddLight(lighting, $"F01_Emergency_Elevator_{z:0}", new Vector3(-17.4f, 3.9f, z), Emergency, 0.8f, 5f);
                Lamp(l.transform, new Color(1f, 0.13f, 0.08f));
                if (z == 0f) l.gameObject.AddComponent<LightFlicker>();
                surge.Add(l);
            }
            Light reception = AddLight(lighting, "F01_Reception_DeskLamp", new Vector3(-8f, 1.6f, 2.4f), new Color(1f, 0.85f, 0.6f), 0.9f, 4f);
            reception.gameObject.AddComponent<LightFlicker>();
            surge.Add(reception);

            // Relámpago: entra por la cristalera de la fachada (z = -14)
            GameObject flashGo = new GameObject("F01_Lightning_Flash");
            flashGo.transform.SetParent(lighting, false);
            flashGo.transform.SetPositionAndRotation(new Vector3(0f, 4.5f, -21f), Quaternion.Euler(18f, 0f, 0f));
            Light flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.spotAngle = 75f;
            flash.range = 40f;
            flash.intensity = 4.5f;
            flash.color = new Color(0.75f, 0.85f, 1f);
            flash.shadows = LightShadows.Soft;
            flash.lightmapBakeType = LightmapBakeType.Realtime;
            flash.enabled = false;

            AudioSource roomTone = AddSource(audio, "F01_RoomTone", Vector3.zero, Clip("Ambience/SFX_F06_LabRoomTone_Loop"), 0.25f, true, 0f, 1f, 40f, "Ambience");
            roomTone.playOnAwake = true;
            AudioSource hum = AddSource(audio, "F01_Elevator_Hum", new Vector3(-17f, 2.5f, 0f), Clip("Ambience/SFX_F06_ElectricHum_Loop"), 0.4f, true, 1f, 1.5f, 12f, "Ambience");
            hum.playOnAwake = true;

            AudioSource thunder = AddSource(audio, "F01_Thunder", Vector3.zero, null, 1f, false, 0f, 1f, 60f, "Ambience");
            var storm = audio.gameObject.AddComponent<StormController>();
            var so = new SerializedObject(storm);
            so.FindProperty("_thunderSource").objectReferenceValue = thunder;
            SetArray(so.FindProperty("_thunderClips"), new List<AudioClip> { Clip("SFX/SFX_F06_Thunder_01"), Clip("SFX/SFX_F06_Thunder_02") });
            SetArray(so.FindProperty("_surgeLights"), surge);
            so.FindProperty("_flashLight").objectReferenceValue = flash;
            so.FindProperty("_firstDelay").floatValue = 8f;
            so.FindProperty("_interval").vector2Value = new Vector2(25f, 50f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Notas y terminal ----------------

        private static void BuildNotesAndTerminal(Transform interactables)
        {
            Material paper = SolidMaterial("MAT_F01_Paper", new Color(0.92f, 0.92f, 0.86f));
            Material sign = EmissiveMaterial("MAT_F01_EvacSign", new Color(0.2f, 1f, 0.4f), 0.8f);

            Note(interactables, "F01_N1_Registro_Visitas", new Vector3(-8.3f, 1.13f, 1.7f), new Vector3(0f, 12f, 0f), new Vector3(0.24f, 0.01f, 0.32f), paper,
                "RECEPCIÓN · REGISTRO DE VISITAS", "Visita autorizada: Directora de Bioseguridad + 1 menor (Elena). Acceso al Piso 6: permanente. Firmado: seguridad, turno de noche.");
            Note(interactables, "F01_N2_Aviso_Evacuacion", new Vector3(-17.85f, 2.2f, -2.5f), new Vector3(0f, 90f, 0f), new Vector3(0.9f, 0.55f, 0.03f), sign,
                "AVISO DE EVACUACIÓN", "Ascensores bloqueados por protocolo SOMBRA. Nadie abandona el edificio sin validar su estado en el escáner maestro.");
            Note(interactables, "F01_N3_Nota_Vigilante", new Vector3(12.4f, 0.43f, -2.1f), new Vector3(0f, -25f, 0f), new Vector3(0.22f, 0.01f, 0.3f), paper,
                "NOTA DEL VIGILANTE", "Las gafas NexoOS filtran lo que ves. Si algo en el vestíbulo te parece demasiado tranquilo, desconfía.");

            // Terminal de seguridad en la recepción: da acceso a la red de cámaras [C]
            GameObject terminal = Primitive(PrimitiveType.Cube, interactables, "F01_Terminal_Security", new Vector3(-7.4f, 1.36f, 1.9f), new Vector3(0f, 200f, 0f), new Vector3(0.6f, 0.4f, 0.06f), EmissiveMaterial("MAT_F01_ScreenCyan", new Color(0.13f, 0.83f, 0.93f), 1.2f), true);
            var security = terminal.AddComponent<SecurityTerminal>();
            var so = new SerializedObject(security);
            so.FindProperty("_terminalId").stringValue = "F01_Terminal_Security";
            SetArray(so.FindProperty("_feedCameras"), new List<Camera>());
            SetArray(so.FindProperty("_feedLabels"), new List<string>());
            SetArray(so.FindProperty("_doors"), new List<DoorController>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Note(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material, string author, string body)
        {
            GameObject go = Primitive(PrimitiveType.Cube, parent, name, position, euler, scale, material, true);
            go.layer = LayerMask.NameToLayer("Interactable");
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, scale.y < 0.05f ? 6f : 1f, scale.z < 0.05f ? 3f : 1f);
            var note = go.AddComponent<LoreNote>();
            var so = new SerializedObject(note);
            so.FindProperty("_author").stringValue = author;
            so.FindProperty("_body").stringValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Sustos ----------------

        private static void BuildScares(Transform story, Transform audio)
        {
            AudioSource elevator = AddSource(audio, "F01_Elevator_Knocks", new Vector3(-18f, 1.5f, 0f), null, 1f, false, 1f, 2f, 22f, "SFX");
            Scare(story, "Scare_F01_Elevator_Knocks", new Vector3(-12f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 8f), elevator, Clip("SFX/SFX_F06_DoorKnocks"), 1f,
                "(Golpes desde dentro de un ascensor parado entre plantas)", 35f);

            AudioSource reception = AddSource(audio, "F01_Reception_Fall", new Vector3(-8f, 1f, 3.6f), null, 1f, false, 1f, 2f, 20f, "SFX");
            Scare(story, "Scare_F01_Reception_Fall", new Vector3(-4f, 0.45f, 0f), new Vector3(1f, 0.9f, 6f), reception, Clip("SFX/SFX_F06_ObjectFall"), 0.8f,
                "(Algo cae detrás del mostrador de recepción)", 25f);
        }

        private static void Scare(Transform parent, string name, Vector3 position, Vector3 size, AudioSource source, AudioClip clip, float volume, string subtitle, float noise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var scare = go.AddComponent<ScareTrigger>();
            var so = new SerializedObject(scare);
            so.FindProperty("_source").objectReferenceValue = source;
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.FindProperty("_volume").floatValue = volume;
            so.FindProperty("_subtitle").stringValue = subtitle;
            so.FindProperty("_noiseLoudness").floatValue = noise;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Infectado ----------------

        private static void BuildInfected(Transform waypoints)
        {
            // Un único reptante junto a los ascensores, lejos del camino al escáner (x = 0)
            var go = new GameObject("Spawn_F01_Elevator_Crawler");
            go.transform.SetParent(waypoints, false);
            go.transform.SetPositionAndRotation(new Vector3(-15.5f, 0f, -3f), Quaternion.Euler(0f, 90f, 0f));
            var spawn = go.AddComponent<InfectedSpawnPoint>();
            var so = new SerializedObject(spawn);
            so.FindProperty("_infectedPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SmartInfectedBrain>(InfectedPrefab);
            SerializedProperty o = so.FindProperty("_overrides");
            o.FindPropertyRelative("Enabled").boolValue = true;
            o.FindPropertyRelative("Behavior").enumValueIndex = (int)InfectedBehavior.Crawler;
            o.FindPropertyRelative("GradedDetection").boolValue = true;
            o.FindPropertyRelative("MimicryEnabled").boolValue = false;
            o.FindPropertyRelative("SabotageEnabled").boolValue = false;
            o.FindPropertyRelative("HearingRange").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- NavMesh ----------------

        private static void BakeNavMesh(GameObject root)
        {
            var navigation = new GameObject("F01_Navigation");
            navigation.transform.SetParent(root.transform, false);
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << 0) | (1 << 11);
            surface.agentTypeID = 0;

            var hidden = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene other = SceneManager.GetSceneAt(i);
                if (other == _scene || !other.isLoaded) continue;
                foreach (GameObject go in other.GetRootGameObjects()) { if (!go.activeSelf) continue; go.SetActive(false); hidden.Add(go); }
            }
            try { surface.BuildNavMesh(); }
            finally { foreach (GameObject go in hidden) go.SetActive(true); }

            Directory.CreateDirectory(NavMeshFolder);
            string path = NavMeshFolder + "/NavMesh-Floor01_Lobby.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            surface.RemoveData();
            surface.AddData();
        }

        // ---------------- Utilidades ----------------

        private static void Lamp(Transform light, Color color)
        {
            GameObject lamp = Primitive(PrimitiveType.Cube, light, "Lamp", Vector3.zero, Vector3.zero, new Vector3(0.25f, 0.1f, 0.25f), EmissiveMaterial("MAT_F01_EmergencyLamp", color, 2.2f), false);
            lamp.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        }

        private static Light AddLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        private static AudioSource AddSource(Transform parent, string name, Vector3 position, AudioClip clip, float volume, bool loop, float spatial, float minDistance, float maxDistance, string mixerGroup)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            if (_mixer != null)
            {
                AudioMixerGroup[] groups = _mixer.FindMatchingGroups(mixerGroup);
                if (groups != null && groups.Length > 0) source.outputAudioMixerGroup = groups[0];
            }
            return source;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material SolidMaterial(string name, Color color) => GetMaterial(name, color, Color.black, 0f);

        private static Material EmissiveMaterial(string name, Color color, float intensity) => GetMaterial(name, color * 0.4f, color, intensity);

        private static Material GetMaterial(string name, Color albedo, Color emission, float intensity)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = albedo;
            if (intensity > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission * intensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AudioClip Clip(string relative)
        {
            foreach (string ext in new[] { ".wav", ".ogg" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/{relative}{ext}");
                if (clip != null) return clip;
            }
            Debug.LogWarning("[NexoFloor01AmbienceBuilder] No se encontró el clip " + relative);
            return null;
        }

        private static AudioMixer FindMixer()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioMixer NexoMixer"))
            {
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(guid));
                if (mixer != null) return mixer;
            }
            return null;
        }

        private static Transform Group(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        private static void SetArray<T>(SerializedProperty property, IList<T> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (values[i] is Object obj) element.objectReferenceValue = obj;
                else if (values[i] is string s) element.stringValue = s;
            }
        }
    }
}
