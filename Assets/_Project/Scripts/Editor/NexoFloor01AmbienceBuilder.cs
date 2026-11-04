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
            BuildExpansion(audio, story, waypoints);
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

        // ---------------- Ampliación: alas de servicio ----------------

        private const string ExpansionRoot = "F01_Expansion";
        private const string LevelMaterials = "Assets/_Project/Materials/Levels/";

        /// <summary>
        /// Amplía el vestíbulo con un circuito de servicio: pasillo norte (correo) → Sala de seguridad → pasillo este
        /// (salida de emergencia sellada) → vuelta al vestíbulo, más la sala de descanso a oscuras.
        /// La geometría va en su propia raíz F01_Expansion (no se apaga con el giro). Las paredes originales
        /// Wall_North y Wall_East solo se DESACTIVAN y se sustituyen por versiones con hueco de puerta.
        /// Infectados, sustos y audio de las alas cuelgan de F01_Ambience (se apagan al empezar el giro).
        /// </summary>
        private static void BuildExpansion(Transform audio, Transform story, Transform waypoints)
        {
            foreach (GameObject go in _scene.GetRootGameObjects()) if (go.name == ExpansionRoot) Object.DestroyImmediate(go);
            var root = new GameObject(ExpansionRoot);
            SceneManager.MoveGameObjectToScene(root, _scene);
            Transform geo = Group(root, "Geometry");
            Transform props = Group(root, "Props");
            Transform lights = Group(root, "Lights");
            Transform items = Group(root, "Interactables");

            Material wall = LevelMaterial("M_Lvl_F01_Wall");
            Material floor = LevelMaterial("M_Lvl_F01_Floor");
            Material ceiling = LevelMaterial("M_Lvl_F01_Ceiling");
            Material accent = LevelMaterial("M_Lvl_F01_Accent");
            Material stone = LevelMaterial("M_Lvl_F01_Stone");

            // Paredes originales: se ocultan (no se borran) para abrir las dos puertas nuevas
            SetOriginalWallActive("Wall_East", false);
            SetOriginalWallActive("Wall_North", false);

            // Vestíbulo: pared este con puerta (z -1.2..1.2) y pared norte con puerta (x 10.8..13.2)
            Box(geo, "Lobby_Wall_East_S", new Vector3(18f, 0f, -14f), new Vector3(18.3f, 6f, -1.2f), wall);
            Box(geo, "Lobby_Wall_East_N", new Vector3(18f, 0f, 1.2f), new Vector3(18.3f, 6f, 14f), wall);
            Box(geo, "Lobby_Wall_East_Lintel", new Vector3(18f, 3f, -1.2f), new Vector3(18.3f, 6f, 1.2f), accent);
            Box(geo, "Lobby_Wall_North_W", new Vector3(-18.3f, 0f, 14f), new Vector3(10.8f, 6f, 14.3f), wall);
            Box(geo, "Lobby_Wall_North_E", new Vector3(13.2f, 0f, 14f), new Vector3(18.3f, 6f, 14.3f), wall);
            Box(geo, "Lobby_Wall_North_Lintel", new Vector3(10.8f, 3f, 14f), new Vector3(13.2f, 6f, 14.3f), accent);

            // Pasillo este (x 18.3..34, z -2..2)
            Box(geo, "EastCorr_Floor", new Vector3(18.3f, -0.3f, -2.3f), new Vector3(34.3f, 0f, 2.3f), floor);
            Box(geo, "EastCorr_Ceiling", new Vector3(18.3f, 3.2f, -2.3f), new Vector3(34.3f, 3.5f, 2.3f), ceiling);
            Box(geo, "EastCorr_Wall_N_A", new Vector3(18.3f, 0f, 2f), new Vector3(23f, 3.2f, 2.3f), wall);
            Box(geo, "EastCorr_Wall_N_B", new Vector3(25f, 0f, 2f), new Vector3(34.3f, 3.2f, 2.3f), wall);
            Box(geo, "EastCorr_Wall_N_Lintel", new Vector3(23f, 2.4f, 2f), new Vector3(25f, 3.2f, 2.3f), accent);
            Box(geo, "EastCorr_Wall_S_A", new Vector3(18.3f, 0f, -2.3f), new Vector3(27f, 3.2f, -2f), wall);
            Box(geo, "EastCorr_Wall_S_B", new Vector3(29f, 0f, -2.3f), new Vector3(34.3f, 3.2f, -2f), wall);
            Box(geo, "EastCorr_Wall_S_Lintel", new Vector3(27f, 2.4f, -2.3f), new Vector3(29f, 3.2f, -2f), accent);
            Box(geo, "EastCorr_Wall_End", new Vector3(34f, 0f, -2f), new Vector3(34.3f, 3.2f, 2f), wall);

            // Sala de seguridad (x 18.3..30, z 2.3..14)
            Box(geo, "Security_Floor", new Vector3(18.3f, -0.3f, 2.3f), new Vector3(30.3f, 0f, 14.3f), floor);
            Box(geo, "Security_Ceiling", new Vector3(18.3f, 3.2f, 2.3f), new Vector3(30.3f, 3.5f, 14.3f), ceiling);
            Box(geo, "Security_Wall_E", new Vector3(30f, 0f, 2.3f), new Vector3(30.3f, 3.2f, 14f), wall);
            Box(geo, "Security_Wall_N_A", new Vector3(18.3f, 0f, 14f), new Vector3(24f, 3.2f, 14.3f), wall);
            Box(geo, "Security_Wall_N_B", new Vector3(26f, 0f, 14f), new Vector3(30.3f, 3.2f, 14.3f), wall);
            Box(geo, "Security_Wall_N_Lintel", new Vector3(24f, 2.4f, 14f), new Vector3(26f, 3.2f, 14.3f), accent);

            // Pasillo norte de servicio (x 10.3..30, z 14.3..18)
            Box(geo, "NorthCorr_Floor", new Vector3(10f, -0.3f, 14.3f), new Vector3(30.3f, 0f, 18.3f), floor);
            Box(geo, "NorthCorr_Ceiling", new Vector3(10f, 3.2f, 14.3f), new Vector3(30.3f, 3.5f, 18.3f), ceiling);
            Box(geo, "NorthCorr_Wall_N", new Vector3(10f, 0f, 18f), new Vector3(30.3f, 3.2f, 18.3f), wall);
            Box(geo, "NorthCorr_Wall_W", new Vector3(10f, 0f, 14.3f), new Vector3(10.3f, 3.2f, 18f), wall);
            Box(geo, "NorthCorr_Wall_E", new Vector3(30f, 0f, 14.3f), new Vector3(30.3f, 3.2f, 18f), wall);

            // Sala de descanso (x 18.3..30, z -12..-2.3)
            Box(geo, "Lounge_Floor", new Vector3(18.3f, -0.3f, -12.3f), new Vector3(30.3f, 0f, -2.3f), floor);
            Box(geo, "Lounge_Ceiling", new Vector3(18.3f, 3.2f, -12.3f), new Vector3(30.3f, 3.5f, -2.3f), ceiling);
            Box(geo, "Lounge_Wall_E", new Vector3(30f, 0f, -12f), new Vector3(30.3f, 3.2f, -2.3f), wall);
            Box(geo, "Lounge_Wall_S", new Vector3(18.3f, 0f, -12.3f), new Vector3(30.3f, 3.2f, -12f), wall);

            // ---- Mobiliario y cobertura ----
            Material metal = SolidMaterial("MAT_F01_Metal", new Color(0.35f, 0.37f, 0.4f));
            Material wood = SolidMaterial("MAT_F01_Wood", new Color(0.36f, 0.26f, 0.18f));
            Material fabric = SolidMaterial("MAT_F01_Fabric", new Color(0.2f, 0.24f, 0.3f));
            Material screen = EmissiveMaterial("MAT_F01_SecurityScreen", new Color(0.2f, 0.75f, 0.9f), 1.1f);
            Material red = EmissiveMaterial("MAT_F01_ExitRed", new Color(1f, 0.1f, 0.05f), 1.6f);
            Material vend = EmissiveMaterial("MAT_F01_Vending", new Color(0.35f, 0.9f, 1f), 0.9f);

            // Correo: estanterías bajas contra la pared norte y cajas en el suelo (para esconderse tras ellas)
            Box(props, "Mail_Shelf_A", new Vector3(13.5f, 0f, 17.4f), new Vector3(17.5f, 1.6f, 18f), metal);
            Box(props, "Mail_Shelf_B", new Vector3(21f, 0f, 17.4f), new Vector3(25f, 1.6f, 18f), metal);
            Box(props, "Mail_Crates_A", new Vector3(19.2f, 0f, 14.4f), new Vector3(20.2f, 1.1f, 15.3f), wood);
            Box(props, "Mail_Crates_B", new Vector3(27f, 0f, 14.4f), new Vector3(28f, 0.9f, 15.2f), wood);

            // Seguridad: mostrador, monitores en la pared este, archivadores
            Box(props, "Security_Desk", new Vector3(21f, 0f, 7.5f), new Vector3(24.5f, 0.78f, 8.6f), metal);
            Box(props, "Security_Cabinets", new Vector3(18.4f, 0f, 11f), new Vector3(19f, 1.9f, 13.6f), metal);
            for (int i = 0; i < 3; i++)
            {
                Box(props, "Security_Monitor_" + i, new Vector3(29.92f, 1.3f, 5.2f + i * 2.1f), new Vector3(30f, 2.3f, 6.9f + i * 2.1f), screen);
            }

            // Descanso: sofá, mesa, máquina expendedora
            Box(props, "Lounge_Couch", new Vector3(20f, 0f, -11.8f), new Vector3(23.5f, 0.75f, -10.8f), fabric);
            Box(props, "Lounge_Table", new Vector3(21f, 0f, -9f), new Vector3(22.6f, 0.5f, -8f), wood);
            Box(props, "Lounge_Vending", new Vector3(18.4f, 0f, -11.9f), new Vector3(19.4f, 1.95f, -10.9f), vend);

            // Pasillo este: salida de emergencia sellada al fondo
            Box(props, "EastCorr_EmergencyDoor", new Vector3(33.9f, 0f, -0.7f), new Vector3(34f, 2.2f, 0.7f), metal);
            Box(props, "EastCorr_ExitSign", new Vector3(33.88f, 2.35f, -0.45f), new Vector3(33.95f, 2.65f, 0.45f), red);
            Box(props, "EastCorr_Bench", new Vector3(20.5f, 0f, -1.95f), new Vector3(23f, 0.45f, -1.45f), wood);

            // ---- Luces (máx. 4 por sala, tiempo real) ----
            AddLight(lights, "EastCorr_Light_A", new Vector3(21.5f, 2.9f, 0f), new Color(0.95f, 0.92f, 0.85f), 1.3f, 7f);
            AddLight(lights, "EastCorr_Light_B", new Vector3(31f, 2.9f, 0f), new Color(0.95f, 0.92f, 0.85f), 1.1f, 7f).gameObject.AddComponent<LightFlicker>();
            AddLight(lights, "EastCorr_ExitGlow", new Vector3(33.4f, 2.4f, 0f), new Color(1f, 0.12f, 0.06f), 0.8f, 4f);
            AddLight(lights, "NorthCorr_Light_A", new Vector3(13.5f, 2.9f, 16.2f), new Color(0.95f, 0.92f, 0.85f), 1.1f, 7f);
            AddLight(lights, "NorthCorr_Light_B", new Vector3(26f, 2.9f, 16.2f), new Color(0.95f, 0.92f, 0.85f), 0.9f, 7f).gameObject.AddComponent<LightFlicker>();
            AddLight(lights, "Security_DeskLamp", new Vector3(22.5f, 1.5f, 8.1f), new Color(1f, 0.85f, 0.6f), 1.2f, 6f);
            AddLight(lights, "Security_MonitorGlow", new Vector3(29.2f, 1.8f, 7.3f), new Color(0.3f, 0.8f, 1f), 0.9f, 6f);
            AddLight(lights, "Lounge_VendingGlow", new Vector3(19.8f, 1.2f, -11f), new Color(0.35f, 0.9f, 1f), 0.6f, 4f);
            AddLight(lights, "Lounge_Emergency", new Vector3(29.6f, 2.7f, -11.6f), Emergency, 0.35f, 5f);

            // ---- Notas, terminal, munición y escondite ----
            Material paper = SolidMaterial("MAT_F01_Paper", new Color(0.92f, 0.92f, 0.86f));
            Material board = SolidMaterial("MAT_F01_Board", new Color(0.93f, 0.95f, 0.95f));
            Note(items, "F01_N4_Registro_Seguridad", new Vector3(22.2f, 0.8f, 8f), new Vector3(0f, 20f, 0f), new Vector3(0.24f, 0.01f, 0.32f), paper,
                "REGISTRO DE SEGURIDAD · 02:14", "Dirección ordena mantener el vestíbulo sellado hasta validar a todo el personal en el escáner maestro.");
            Note(items, "F01_N5_Pizarra_Personal", new Vector3(29.85f, 1.6f, -6.5f), new Vector3(0f, -90f, 0f), new Vector3(1.6f, 1f, 0.03f), board,
                "PIZARRA DEL PERSONAL", "Simulacro de las 21:00 cancelado. NO uséis los ascensores. Si alguien os llama desde el pasillo, no contestéis.");
            Note(items, "F01_N6_Salida_Sellada", new Vector3(33.85f, 1.5f, 1.2f), new Vector3(0f, -90f, 0f), new Vector3(0.6f, 0.4f, 0.03f), red,
                "SALIDA DE EMERGENCIA", "BLOQUEADA POR PROTOCOLO SOMBRA. Validación biométrica obligatoria en el escáner maestro del vestíbulo.");

            GameObject terminal = Primitive(PrimitiveType.Cube, items, "F01_Terminal_SecurityRoom", new Vector3(23.6f, 1.05f, 8.1f), new Vector3(0f, 180f, 0f), new Vector3(0.6f, 0.4f, 0.06f), screen, true);
            var security = terminal.AddComponent<SecurityTerminal>();
            var tso = new SerializedObject(security);
            tso.FindProperty("_terminalId").stringValue = "F01_Terminal_SecurityRoom";
            SetArray(tso.FindProperty("_feedCameras"), new List<Camera>());
            SetArray(tso.FindProperty("_feedLabels"), new List<string>());
            SetArray(tso.FindProperty("_doors"), new List<DoorController>());
            tso.ApplyModifiedPropertiesWithoutUndo();

            Ammo(items, "F01_Ammo_Security", new Vector3(21.6f, 0.86f, 7.9f), 7);
            Ammo(items, "F01_Ammo_Lounge", new Vector3(21.8f, 0.56f, -8.5f), 7);
            Locker(items, "F01_Locker_Lounge", new Vector3(29.4f, 0f, -4f), -90f, metal);

            // ---- Sustos e infectados de las alas (cuelgan de F01_Ambience: se apagan con el giro) ----
            AudioSource phone = AddSource(audio, "F01_Security_Phone", new Vector3(23f, 1f, 8f), null, 1f, false, 1f, 2f, 24f, "SFX");
            Scare(story, "Scare_F01_Security_Phone", new Vector3(12f, 0.45f, 15.2f), new Vector3(2.4f, 0.9f, 1f), phone, Clip("SFX/SFX_F12_PhoneRing"), 0.8f,
                "(Un teléfono suena en la sala de seguridad, al fondo del pasillo)", 30f);
            AudioSource vending = AddSource(audio, "F01_Lounge_Vending", new Vector3(18.9f, 1f, -11.4f), null, 1f, false, 1f, 2f, 18f, "SFX");
            Scare(story, "Scare_F01_Lounge_Vending", new Vector3(28f, 0.45f, -2.9f), new Vector3(2f, 0.9f, 0.8f), vending, Clip("SFX/SFX_F12_DistantBang"), 0.9f,
                "(Algo golpea la máquina expendedora en la sala de descanso a oscuras)", 25f);
            AudioSource corrHum = AddSource(audio, "F01_EastCorr_Hum", new Vector3(31f, 2.8f, 0f), Clip("Ambience/SFX_F06_ElectricHum_Loop"), 0.35f, true, 1f, 1f, 8f, "Ambience");
            corrHum.playOnAwake = true;

            SpawnInfected(waypoints, "Spawn_F01_NorthCorr_Patroller", new Vector3(28.5f, 0f, 16.2f), -90f, InfectedBehavior.Patroller,
                new[] { new Vector3(28.5f, 0f, 16.2f), new Vector3(12.2f, 0f, 16.2f) });
            SpawnInfected(waypoints, "Spawn_F01_Lounge_Crawler", new Vector3(25f, 0f, -8.5f), -90f, InfectedBehavior.Crawler, null);
        }

        private static void SetOriginalWallActive(string wallName, bool active)
        {
            foreach (GameObject go in _scene.GetRootGameObjects())
            {
                if (go.name != "Level_Geometry") continue;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == wallName) t.gameObject.SetActive(active);
                }
            }
        }

        /// <summary>Caja estática definida por sus esquinas mínima y máxima en el mundo.</summary>
        private static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material material)
        {
            GameObject go = Primitive(PrimitiveType.Cube, parent, name, (min + max) * 0.5f, Vector3.zero, max - min, material, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static Material LevelMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LevelMaterials + name + ".mat");
            return material != null ? material : SolidMaterial("MAT_F01_Fallback", new Color(0.7f, 0.7f, 0.7f));
        }

        private static void Ammo(Transform parent, string name, Vector3 position, int rounds)
        {
            GameObject box = Primitive(PrimitiveType.Cube, parent, name, position, new Vector3(0f, 25f, 0f), new Vector3(0.18f, 0.1f, 0.12f), SolidMaterial("MAT_F01_AmmoBox", new Color(0.3f, 0.35f, 0.2f)), true);
            box.layer = LayerMask.NameToLayer("Interactable");
            box.GetComponent<BoxCollider>().size = new Vector3(2.5f, 3f, 3f);
            var pickup = box.AddComponent<AmmoPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("_rounds").intValue = rounds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Armario hueco para esconderse (interior 0.95 x 0.9 m; ver HidingSpot).</summary>
        private static void Locker(Transform parent, string name, Vector3 position, float yaw, Material metal)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            LocalBox(root.transform, "Back", new Vector3(0f, 1.1f, -0.475f), new Vector3(1.05f, 2.2f, 0.05f), metal);
            LocalBox(root.transform, "Left", new Vector3(-0.5f, 1.1f, 0f), new Vector3(0.05f, 2.2f, 1f), metal);
            LocalBox(root.transform, "Right", new Vector3(0.5f, 1.1f, 0f), new Vector3(0.05f, 2.2f, 1f), metal);
            LocalBox(root.transform, "Top", new Vector3(0f, 2.175f, 0f), new Vector3(1.05f, 0.05f, 1f), metal);
            LocalBox(root.transform, "Door", new Vector3(0f, 1.1f, 0.475f), new Vector3(0.95f, 2.15f, 0.04f), metal);

            var inside = new GameObject("InsidePoint");
            inside.transform.SetParent(root.transform, false);
            inside.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var exit = new GameObject("ExitPoint");
            exit.transform.SetParent(root.transform, false);
            exit.transform.localPosition = new Vector3(0f, 0.05f, 1.1f);

            AudioSource door = AddSource(root.transform, "DoorAudio", root.transform.TransformPoint(new Vector3(0f, 1.2f, 0.5f)), null, 1f, false, 1f, 1.5f, 14f, "SFX");
            AudioSource breathing = AddSource(root.transform, "PlayerBreathing", root.transform.position, Clip("Ambience/SFX_F06_Breathing_Loop"), 0.35f, true, 0f, 1f, 5f, "Voices");
            var spot = root.AddComponent<HidingSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("_insidePoint").objectReferenceValue = inside.transform;
            so.FindProperty("_exitPoint").objectReferenceValue = exit.transform;
            so.FindProperty("_breathing").objectReferenceValue = breathing;
            so.FindProperty("_doorSource").objectReferenceValue = door;
            so.FindProperty("_doorClip").objectReferenceValue = Clip("SFX/SFX_F12_MetalCreak");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LocalBox(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void SpawnInfected(Transform parent, string name, Vector3 position, float yaw, InfectedBehavior behavior, Vector3[] patrol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var points = new List<Transform>();
            if (patrol != null)
            {
                for (int i = 0; i < patrol.Length; i++)
                {
                    var p = new GameObject($"{name}_P{i}");
                    p.transform.SetParent(go.transform, false);
                    p.transform.position = patrol[i];
                    points.Add(p.transform);
                }
            }
            var spawn = go.AddComponent<InfectedSpawnPoint>();
            var so = new SerializedObject(spawn);
            so.FindProperty("_infectedPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SmartInfectedBrain>(InfectedPrefab);
            SetArray(so.FindProperty("_patrolPoints"), points);
            SerializedProperty o = so.FindProperty("_overrides");
            o.FindPropertyRelative("Enabled").boolValue = true;
            o.FindPropertyRelative("Behavior").enumValueIndex = (int)behavior;
            o.FindPropertyRelative("GradedDetection").boolValue = true;
            o.FindPropertyRelative("MimicryEnabled").boolValue = false;
            o.FindPropertyRelative("SabotageEnabled").boolValue = false;
            o.FindPropertyRelative("SightDistance").floatValue = 11f;
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
