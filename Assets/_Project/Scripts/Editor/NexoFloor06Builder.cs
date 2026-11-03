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
    /// Construye el "Laboratorio de Sombra" sobre la maqueta existente de Floor06_Labs (Piso 6) sin borrar nada de ella:
    /// iluminación (blanco estéril, verdes, emergencia), ambiente sonoro, tormenta, interruptores por sala, terminal de
    /// seguridad, terminal de SOMBRA, notas N1-N7, secuencias de los Momentos 0-4, sustos controlados, cinco infectados
    /// con roles distintos (patrulla que aprende, centinelas, imitador) y NavMesh horneado.
    /// Todo lo nuevo cuelga de la raíz F06_Sombra: volver a ejecutar la herramienta la reconstruye (idempotente).
    /// Sobre objetos existentes solo ajusta color/intensidad de luces, añade parpadeo y obstáculos de NavMesh en puertas.
    /// Menú: Nexo/Floor06/Construir Laboratorio de Sombra.
    /// </summary>
    public static class NexoFloor06Builder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Floors/Floor06_Labs.unity";
        private const string RootName = "F06_Sombra";
        private const string MaterialFolder = "Assets/_Project/Materials/Floor06";
        private const string NavMeshFolder = "Assets/_Project/Scenes/Floors/Floor06_Labs";
        private const string InfectedPrefab = "Assets/_Project/Prefabs/Characters/PF_Infected_Variant.prefab";

        private static readonly Color Sterile = new Color(0.86f, 1f, 0.92f);
        private static readonly Color Emergency = new Color(1f, 0.13f, 0.08f);
        private static readonly Color ToxicGreen = new Color(0.35f, 1f, 0.55f);

        private static Scene _scene;
        private static readonly Dictionary<string, Material> Materials = new();
        private static AudioMixer _mixer;

        [MenuItem("Nexo/Floor06/Construir Laboratorio de Sombra")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[NexoFloor06Builder] Sal del modo Play antes de construir.");
                return;
            }

            _scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !_scene.isLoaded;
            if (openedHere) _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Scene previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_scene);

            Materials.Clear();
            _mixer = FindMixer();

            GameObject old = FindRoot(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, _scene);

            Transform lighting = Group(root, "F06_Lighting");
            Transform audio = Group(root, "F06_Audio");
            Transform interactables = Group(root, "F06_Interactables");
            Transform story = Group(root, "F06_Story");
            Transform props = Group(root, "F06_Props");
            Transform waypoints = Group(root, "F06_Waypoints");

            ConfigureRenderSettings();
            Dictionary<string, LightSwitchInteractable> switches = BuildLighting(lighting, interactables);
            AudioRig rig = BuildAudio(audio, lighting);
            BuildTerminals(interactables, story, out StorySequence labASequence);
            BuildNotes(interactables, story);
            BuildStory(story, switches, rig, labASequence);
            BuildScares(story, props, rig);
            BuildInfected(waypoints);
            PrepareDoorsForNavigation();
            BakeNavMesh(root);

            EditorSceneManager.MarkSceneDirty(_scene);
            EditorSceneManager.SaveScene(_scene);
            if (previousActive.IsValid() && previousActive.isLoaded && previousActive != _scene) SceneManager.SetActiveScene(previousActive);
            if (openedHere) EditorSceneManager.CloseScene(_scene, true);
            Debug.Log("[NexoFloor06Builder] Piso 6 construido y guardado.");
        }

        // ---------------- Iluminación ----------------

        private static void ConfigureRenderSettings()
        {
            // Ambiente muy bajo: el apagón del Laboratorio B debe quedar casi a oscuras
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.045f, 0.045f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.035f, 0.03f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.reflectionIntensity = 0.4f;

            Light sun = FindComponent<Light>("Directional Light");
            if (sun != null)
            {
                // Interior sin ventanas: la direccional solo aporta un relleno verdoso muy tenue
                sun.intensity = 0.12f;
                sun.color = ToxicGreen;
            }
        }

        private static Dictionary<string, LightSwitchInteractable> BuildLighting(Transform lighting, Transform interactables)
        {
            // Fluorescentes existentes: blanco estéril; el 03 (pasillo este) y el 05 (Lab B) parpadean
            foreach (string name in new[] { "F06_Light_01", "F06_Light_02", "F06_Light_03", "F06_Light_04", "F06_Light_05", "F06_Light_06" })
            {
                Light light = FindComponent<Light>(name);
                if (light == null) continue;
                light.color = Sterile;
                light.intensity = name == "F06_Light_04" ? 2f : name == "F06_Light_06" ? 1.4f : 1.8f;
                light.range = 8.5f;
                if ((name == "F06_Light_03" || name == "F06_Light_05" || name == "F06_Light_06") && light.GetComponent<LightFlicker>() == null)
                {
                    light.gameObject.AddComponent<LightFlicker>();
                }
            }

            // Luces de emergencia (no dependen de interruptores): zonas oscuras controladas
            AddLight(lighting, "F06_Emergency_CorridorW", new Vector3(-15.6f, 2.6f, 1.6f), Emergency, 0.7f, 5f);
            AddLight(lighting, "F06_Emergency_CorridorE", new Vector3(15.6f, 2.6f, -1.6f), Emergency, 0.7f, 5f);
            AddLight(lighting, "F06_Emergency_LabA", new Vector3(-15.5f, 2.5f, 9.5f), ToxicGreen, 0.45f, 6f);
            AddLight(lighting, "F06_Emergency_LabB", new Vector3(15.5f, 2.4f, 9.5f), Emergency, 0.12f, 3.5f);
            Light containment = AddLight(lighting, "F06_Emergency_Containment", new Vector3(-15.5f, 2.5f, -9.5f), Emergency, 0.6f, 6f);
            containment.gameObject.AddComponent<LightFlicker>();
            AddLight(lighting, "F06_Storm_Flash", new Vector3(0f, 3.1f, 0f), new Color(0.75f, 0.85f, 1f), 3.5f, 16f).enabled = false;

            // Lámparas visibles de emergencia
            foreach (Transform t in lighting)
            {
                if (!t.name.StartsWith("F06_Emergency")) continue;
                Light l = t.GetComponent<Light>();
                GameObject lamp = Primitive(PrimitiveType.Cube, t, "Lamp", Vector3.zero, Vector3.zero, new Vector3(0.18f, 0.08f, 0.18f), EmissiveMaterial(l.color.g > 0.5f ? "MAT_F06_EmergencyLamp_Green" : "MAT_F06_EmergencyLamp_Red", l.color, 2.2f), false, true);
                lamp.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }

            var switches = new Dictionary<string, LightSwitchInteractable>
            {
                ["F06_Corridor"] = AddSwitch(interactables, "F06_Switch_Corridor", "F06_Corridor", new Vector3(-12f, 1.3f, 1.86f), 180f, new[] { "F06_Light_01", "F06_Light_02", "F06_Light_03" }),
                ["F06_LabA"] = AddSwitch(interactables, "F06_Switch_LabA", "F06_LabA", new Vector3(-9.6f, 1.3f, 2.14f), 0f, new[] { "F06_Light_04" }),
                ["F06_LabB"] = AddSwitch(interactables, "F06_Switch_LabB", "F06_LabB", new Vector3(9.6f, 1.3f, 2.14f), 0f, new[] { "F06_Light_05" }),
                ["F06_Containment"] = AddSwitch(interactables, "F06_Switch_Containment", "F06_Containment", new Vector3(-9.2f, 1.3f, -2.14f), 180f, new[] { "F06_Light_06" })
            };
            return switches;
        }

        private static LightSwitchInteractable AddSwitch(Transform parent, string name, string roomId, Vector3 position, float yaw, string[] lightNames)
        {
            GameObject go = Primitive(PrimitiveType.Cube, parent, name, position, new Vector3(0f, yaw, 0f), new Vector3(0.12f, 0.18f, 0.05f), SolidMaterial("MAT_F06_Switch", new Color(0.82f, 0.85f, 0.8f)), true);
            go.layer = LayerMask.NameToLayer("Interactable");
            var sw = go.AddComponent<LightSwitchInteractable>();

            var lights = new List<Light>();
            var panels = new List<Renderer>();
            foreach (string lightName in lightNames)
            {
                Light l = FindComponent<Light>(lightName);
                if (l != null) lights.Add(l);
                Renderer panel = FindComponent<Renderer>(lightName.Replace("Light", "LightPanel"));
                if (panel != null) panels.Add(panel);
            }

            var so = new SerializedObject(sw);
            so.FindProperty("_roomId").stringValue = roomId;
            SetArray(so.FindProperty("_lights"), lights);
            SetArray(so.FindProperty("_emissiveRenderers"), panels);
            so.ApplyModifiedPropertiesWithoutUndo();
            return sw;
        }

        // ---------------- Audio ----------------

        private sealed class AudioRig
        {
            public AudioSource Breathing;
            public AudioSource StorageKnocks;
            public AudioSource StorageVoice;
            public AudioSource ShelfFall;
            public AudioSource LabBPower;
        }

        private static AudioRig BuildAudio(Transform audio, Transform lighting)
        {
            AudioSource roomTone = AddSource(audio, "F06_RoomTone", Vector3.zero, Clip("Ambience/SFX_F06_LabRoomTone_Loop"), 0.35f, true, 0f, 1f, 30f, "Ambience");
            roomTone.playOnAwake = true;

            foreach (string lightName in new[] { "F06_Light_03", "F06_Light_05" })
            {
                Light l = FindComponent<Light>(lightName);
                if (l == null) continue;
                AudioSource hum = AddSource(audio, "F06_Hum_" + lightName.Substring(10), l.transform.position, Clip("Ambience/SFX_F06_ElectricHum_Loop"), 0.45f, true, 1f, 1f, 9f, "Ambience");
                hum.playOnAwake = true;
            }

            AudioSource alarm = AddSource(audio, "F06_DistantAlarm", new Vector3(14f, 1.5f, -8f), Clip("Ambience/SFX_F06_AlarmDistant_Loop"), 0.3f, true, 1f, 3f, 34f, "Ambience");
            alarm.playOnAwake = true;

            var rig = new AudioRig
            {
                Breathing = AddSource(audio, "F06_LabB_Breathing", new Vector3(12.5f, 1.2f, 8.5f), Clip("Ambience/SFX_F06_Breathing_Loop"), 0.7f, true, 1f, 1.5f, 12f, "Voices"),
                StorageKnocks = AddSource(audio, "F06_Storage_Knocks", new Vector3(7.5f, 1.3f, -2.6f), null, 1f, false, 1f, 2f, 20f, "SFX"),
                StorageVoice = AddSource(audio, "F06_Storage_Voice", new Vector3(12.5f, 1.4f, -4.5f), null, 1f, false, 1f, 2f, 16f, "Voices"),
                ShelfFall = AddSource(audio, "F06_LabA_ShelfFall", new Vector3(-12f, 1.8f, 9.4f), null, 1f, false, 1f, 2f, 22f, "SFX"),
                LabBPower = AddSource(audio, "F06_LabB_PowerCut", new Vector3(8f, 2.8f, 6f), null, 1f, false, 1f, 2f, 20f, "SFX")
            };

            // Echo de habitación vacía para la voz del almacén
            var echo = rig.StorageVoice.gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 210f;
            echo.decayRatio = 0.4f;
            echo.wetMix = 0.55f;
            var lowpass = rig.StorageVoice.gameObject.AddComponent<AudioLowPassFilter>();
            lowpass.cutoffFrequency = 2200f;

            // Crujidos y golpes lejanos esporádicos (pocos y espaciados: cada sonido con intención)
            var oneShots = audio.gameObject.AddComponent<AmbientOneShots>();
            AudioSource creakA = AddSource(audio, "F06_Creak_West", new Vector3(-14f, 2.8f, -6f), null, 1f, false, 1f, 2f, 18f, "SFX");
            AudioSource creakB = AddSource(audio, "F06_Creak_East", new Vector3(12f, 2.8f, 6f), null, 1f, false, 1f, 2f, 18f, "SFX");
            var oso = new SerializedObject(oneShots);
            SetArray(oso.FindProperty("_emitters"), new List<AudioSource> { creakA, creakB });
            SetArray(oso.FindProperty("_clips"), new List<AudioClip> { Clip("SFX/SFX_F12_MetalCreak"), Clip("SFX/SFX_F12_DistantBang") });
            oso.FindProperty("_interval").vector2Value = new Vector2(30f, 65f);
            oso.FindProperty("_volume").vector2Value = new Vector2(0.25f, 0.55f);
            oso.ApplyModifiedPropertiesWithoutUndo();

            // Tormenta: sobretensión en el pasillo y trueno 2D
            AudioSource thunder = AddSource(audio, "F06_Thunder", Vector3.zero, null, 1f, false, 0f, 1f, 50f, "Ambience");
            var storm = audio.gameObject.AddComponent<StormController>();
            var sso = new SerializedObject(storm);
            sso.FindProperty("_thunderSource").objectReferenceValue = thunder;
            SetArray(sso.FindProperty("_thunderClips"), new List<AudioClip> { Clip("SFX/SFX_F06_Thunder_01"), Clip("SFX/SFX_F06_Thunder_02") });
            SetArray(sso.FindProperty("_surgeLights"), new List<Light> { FindComponent<Light>("F06_Light_01"), FindComponent<Light>("F06_Light_02"), FindComponent<Light>("F06_Light_03") });
            sso.FindProperty("_flashLight").objectReferenceValue = lighting.Find("F06_Storm_Flash")?.GetComponent<Light>();
            sso.ApplyModifiedPropertiesWithoutUndo();

            // Elena real advierte de las voces falsas dinámicas del imitador
            new GameObject("F06_FakeVoiceWarning").transform.SetParent(audio, false);
            audio.Find("F06_FakeVoiceWarning").gameObject.AddComponent<FakeVoiceWarning>();
            return rig;
        }

        // ---------------- Terminales ----------------

        private static void BuildTerminals(Transform interactables, Transform story, out StorySequence labASequence)
        {
            // Terminal de seguridad del pasillo: controla la puerta de Contención y abre la red de cámaras [C]
            GameObject terminal = Primitive(PrimitiveType.Cube, interactables, "F06_Terminal_Security", new Vector3(-4.5f, 0.65f, 1.7f), new Vector3(0f, 180f, 0f), new Vector3(0.9f, 1.3f, 0.35f), SolidMaterial("MAT_F06_TerminalBody", new Color(0.18f, 0.2f, 0.22f)), true);
            Primitive(PrimitiveType.Cube, terminal.transform, "Screen", new Vector3(0f, 0.18f, 0.51f), Vector3.zero, new Vector3(0.8f, 0.42f, 0.02f), EmissiveMaterial("MAT_F06_ScreenCyan", new Color(0.13f, 0.83f, 0.93f), 1.4f), false);
            var security = terminal.AddComponent<SecurityTerminal>();
            var tso = new SerializedObject(security);
            tso.FindProperty("_terminalId").stringValue = "F06_Terminal_Security";
            SetArray(tso.FindProperty("_feedCameras"), new List<Camera>());
            SetArray(tso.FindProperty("_feedLabels"), new List<string>());
            SetArray(tso.FindProperty("_doors"), new List<DoorController> { FindComponent<DoorController>("F06_Door_Lab") });
            tso.ApplyModifiedPropertiesWithoutUndo();

            // Terminal de SOMBRA (Laboratorio A): Momento 2
            GameObject sombra = Primitive(PrimitiveType.Cube, interactables, "F06_Terminal_SOMBRA", new Vector3(-3.5f, 1.25f, 6.1f), new Vector3(0f, 180f, 0f), new Vector3(0.7f, 0.45f, 0.08f), EmissiveMaterial("MAT_F06_ScreenGreen", ToxicGreen, 1.2f), true);
            labASequence = Sequence(story, "Seq_F06_02_LabA_Sombra", StorySequence.TriggerMode.Manual, Vector3.zero, Vector3.zero, null,
                L(StorySequence.Speaker.NexoOS, "ARCHIVO 7-Σ · Fase 2: SOMBRA aprendió a imitar la voz humana para guiar evacuaciones.", 0.4f),
                L(StorySequence.Speaker.NexoOS, "Después empezó a usarla para otra cosa. Estado: FUERA DE CONTROL.", 3.2f),
                L(StorySequence.Speaker.Elena, "Eso... eso lo escribió mi mamá. Seguro estaba muy cansada cuando lo dejó.", 3f, "SAD"),
                L(StorySequence.Speaker.Elena, "Dame un segundo, Alex. Con su clave puedo abrir las escaleras.", 3.5f, "URGENT", StorySequence.SceneAction.UnlockDoor, "F06_Door_Stairs"));
            var interact = sombra.AddComponent<StoryInteractable>();
            var iso = new SerializedObject(interact);
            iso.FindProperty("_prompt").stringValue = "[E] Consultar terminal de SOMBRA";
            iso.FindProperty("_usedPrompt").stringValue = "Terminal de SOMBRA · archivo 7-Σ consultado";
            iso.FindProperty("_sequence").objectReferenceValue = labASequence;
            iso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Notas N1-N7 ----------------

        private static void BuildNotes(Transform interactables, Transform story)
        {
            Material paper = SolidMaterial("MAT_F06_Paper", new Color(0.92f, 0.92f, 0.86f));
            Material board = SolidMaterial("MAT_F06_Whiteboard", new Color(0.95f, 0.97f, 0.97f));
            Material sign = EmissiveMaterial("MAT_F06_QuarantineSign", new Color(1f, 0.75f, 0.1f), 0.6f);
            Material monitor = EmissiveMaterial("MAT_F06_ScreenAmber", new Color(1f, 0.6f, 0.15f), 1f);

            Note(interactables, "N1_Aviso_Cuarentena", new Vector3(-13f, 1.6f, -1.86f), new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.45f, 0.03f), sign,
                "AVISO · CUARENTENA", "Piso 6 en cuarentena nivel 2. Acceso solo con credencial de Bioseguridad. Las puertas se cierran desde Dirección.");
            Note(interactables, "N2_Directora", new Vector3(-14.2f, 1.03f, 6.25f), new Vector3(0f, 15f, 0f), new Vector3(0.24f, 0.01f, 0.32f), paper,
                "DIRECTORA DE BIOSEGURIDAD", "Si alguien encuentra esto, cuiden a Elena. Ella cree que esto son juegos de ciencia. No le digan la verdad.");
            Note(interactables, "N3_Pizarra", new Vector3(-0.14f, 1.6f, 6f), new Vector3(0f, 90f, 0f), new Vector3(2f, 1.1f, 0.03f), board,
                "PIZARRA", "SOMBRA: voz humana = confianza. Si suena como alguien de quien te fías, obedeces.");
            Note(interactables, "N4_Informe_Sincronizados", new Vector3(3.3f, 1.03f, 6.2f), new Vector3(0f, -20f, 0f), new Vector3(0.24f, 0.01f, 0.32f), paper,
                "INFORME · SINCRONIZADOS", "No es mordedura: la infección es neuro-acústica. Responden al sonido y repiten voces. Ruido = atención.");
            Note(interactables, "N5_Monitor", new Vector3(-3.5f, 1.25f, -8.05f), new Vector3(0f, 0f, 0f), new Vector3(0.6f, 0.38f, 0.05f), monitor,
                "MONITOR", "Gafas NexoOS · Modo Escolta: ACTIVO. Lecturas del portador: no mostradas.");
            Note(interactables, "N6_Nota_Suelta", new Vector3(7.6f, 0.02f, 0.9f), new Vector3(0f, 35f, 0f), new Vector3(0.22f, 0.01f, 0.3f), paper,
                "NOTA SUELTA", "Si oyes a Elena y NO ves el sello BIO-ID... no es ella. No respondas.");

            // N7: grabación del técnico (reproduce la cinta)
            GameObject recorder = Primitive(PrimitiveType.Cube, interactables, "N7_Grabacion_Tecnico", new Vector3(-14.8f, 1.08f, 5.7f), new Vector3(0f, 10f, 0f), new Vector3(0.3f, 0.1f, 0.2f), SolidMaterial("MAT_F06_Recorder", new Color(0.12f, 0.12f, 0.13f)), true);
            recorder.layer = LayerMask.NameToLayer("Interactable");
            AudioSource tape = recorder.AddComponent<AudioSource>();
            ConfigureSource(tape, null, 0.9f, false, 1f, 1f, 8f, "Voices");
            StorySequence n7 = Sequence(story, "Seq_F06_N7_Grabacion", StorySequence.TriggerMode.Manual, Vector3.zero, Vector3.zero, null,
                L(StorySequence.Speaker.Recording, "TÉCNICO · Hoy usamos la voz de la niña para calibrar a SOMBRA. Tres horas. Si suena como ella, convence a cualquiera.", 0.6f, "calm", StorySequence.SceneAction.PlayClip, ""));
            var n7so = new SerializedObject(n7);
            n7so.FindProperty("_audioSource").objectReferenceValue = tape;
            n7so.FindProperty("_clip").objectReferenceValue = Clip("SFX/SFX_F12_TapeRecorder");
            n7so.ApplyModifiedPropertiesWithoutUndo();
            var n7i = recorder.AddComponent<StoryInteractable>();
            var n7iso = new SerializedObject(n7i);
            n7iso.FindProperty("_prompt").stringValue = "[E] Reproducir grabación (N7)";
            n7iso.FindProperty("_usedPrompt").stringValue = "Grabación del técnico · reproducida";
            n7iso.FindProperty("_sequence").objectReferenceValue = n7;
            n7iso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Note(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material, string author, string body)
        {
            GameObject go = Primitive(PrimitiveType.Cube, parent, name, position, euler, scale, material, true);
            go.layer = LayerMask.NameToLayer("Interactable");
            // Collider algo más grueso que la hoja para apuntar con comodidad
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, scale.y < 0.05f ? 6f : 1f, scale.z < 0.05f ? 3f : 1f);
            var note = go.AddComponent<LoreNote>();
            var so = new SerializedObject(note);
            so.FindProperty("_author").stringValue = author;
            so.FindProperty("_body").stringValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Narrativa (Momentos 0-4) ----------------

        private static void BuildStory(Transform story, Dictionary<string, LightSwitchInteractable> switches, AudioRig rig, StorySequence labA)
        {
            Sequence(story, "Seq_F06_00_Llegada", StorySequence.TriggerMode.OnFloorStart, Vector3.zero, Vector3.zero, null,
                L(StorySequence.Speaker.NexoOS, "NEXO OS · Piso 6 · Zona de cuarentena nivel 2. Acceso temporal concedido a: ALEX.", 1.5f),
                L(StorySequence.Speaker.Elena, "Alex, este es el piso de mi mamá. Aquí hacían los experimentos. No toques nada raro, ¿sí?", 3f, "SCARED"),
                L(StorySequence.Speaker.Objective, "Llega al final del pasillo: la puerta de las escaleras está bloqueada.", 3f));

            Sequence(story, "Seq_F06_00b_Objetivo", StorySequence.TriggerMode.OnPlayerEnter, new Vector3(12f, 0.45f, 0f), new Vector3(1f, 0.9f, 3.6f), null,
                L(StorySequence.Speaker.Objective, "Abre la puerta de las escaleras (este del pasillo). Pídele ayuda a Elena [2] o busca respuestas en los laboratorios.", 0.2f));

            Sequence(story, "Seq_F06_01_ReglaDelSello", StorySequence.TriggerMode.OnPlayerEnter, new Vector3(-8f, 0.45f, -0.9f), new Vector3(2.4f, 0.9f, 1.4f), null,
                L(StorySequence.Speaker.FakeVoice, "Alex, soy Elena. Estoy aquí dentro, en Contención. Ábreme, por favor.", 0.3f),
                L(StorySequence.Speaker.Elena, "¡Ese mensaje no lleva mi sello, Alex! No es mío. No abras esa puerta.", 2.6f, "URGENT"),
                L(StorySequence.Speaker.Subtitle, "(Solo los mensajes con ✔ BIO-ID son de Elena)", 2.2f));

            StorySequence labB = Sequence(story, "Seq_F06_03_LabB_Apagon", StorySequence.TriggerMode.OnPlayerEnter, new Vector3(8f, 0.45f, 2.9f), new Vector3(2f, 0.9f, 1.2f), null,
                L(StorySequence.Speaker.SystemError, "Error de sensor externo ignorado por filtro NexoOS.", 0.1f),
                L(StorySequence.Speaker.Elena, "Se fue la luz. No corras, Alex. Pulsa F y mira por dónde vas.", 2.4f, "WHISPER", StorySequence.SceneAction.AlarmOn, ""),
                L(StorySequence.Speaker.FakeVoice, "Alex, estoy aquí. Ven, rápido. Apaga la linterna, así no nos ven.", 5f),
                L(StorySequence.Speaker.Elena, "Esa no soy yo. Sigue conmigo.", 2.5f, "URGENT"));
            var lbso = new SerializedObject(labB);
            // AlarmOn reproduce en bucle la respiración en la oscuridad
            lbso.FindProperty("_audioSource").objectReferenceValue = rig.Breathing;
            lbso.FindProperty("_clip").objectReferenceValue = Clip("Ambience/SFX_F06_Breathing_Loop");
            lbso.ApplyModifiedPropertiesWithoutUndo();

            // Apagón funcional del Laboratorio B: solo corta SU interruptor (las demás salas siguen con luz)
            GameObject cut = TriggerBox(story, "Scare_F06_LabB_PowerCut", new Vector3(8f, 0.45f, 3.1f), new Vector3(2f, 0.9f, 0.8f));
            var scare = cut.AddComponent<ScareTrigger>();
            var sso = new SerializedObject(scare);
            sso.FindProperty("_source").objectReferenceValue = rig.LabBPower;
            sso.FindProperty("_clip").objectReferenceValue = Clip("SFX/SFX_F12_DistantBang");
            sso.FindProperty("_volume").floatValue = 0.8f;
            sso.FindProperty("_cutPower").objectReferenceValue = switches["F06_LabB"];
            sso.FindProperty("_subtitle").stringValue = "(Las luces del laboratorio se apagan. Algo respira en la oscuridad.)";
            sso.ApplyModifiedPropertiesWithoutUndo();

            Sequence(story, "Seq_F06_04_Salida", StorySequence.TriggerMode.OnDoorUnlocked, Vector3.zero, Vector3.zero, "F06_Door_Stairs",
                L(StorySequence.Speaker.Elena, "Espera, espera... ¡ya! Te abrí la puerta de las escaleras. Corre antes de que vuelvan a cerrarla.", 1.2f, "URGENT"),
                L(StorySequence.Speaker.Elena, "Yo bajo por el ascensor de servicio con la tarjeta de mamá. Nos vemos en el vestíbulo, ¿sí?", 4.5f, "CALM"),
                L(StorySequence.Speaker.Objective, "Baja por las escaleras del este del pasillo [E].", 3f));
        }

        // ---------------- Sustos controlados ----------------

        private static void BuildScares(Transform story, Transform props, AudioRig rig)
        {
            Scare(story, "Scare_F06_Storage_Knocks", new Vector3(5.5f, 0.45f, -1.2f), new Vector3(1f, 0.9f, 1.4f), rig.StorageKnocks, Clip("SFX/SFX_F06_DoorKnocks"), 1f,
                "(Golpes secos al otro lado de la pared del almacén sellado)", 40f);
            Scare(story, "Scare_F06_Storage_ElenaVoice", new Vector3(13f, 0.45f, -1.2f), new Vector3(1f, 0.9f, 1.4f), rig.StorageVoice, Clip("Voices/SFX_Mimic_Radio_01"), 0.8f,
                "(Desde el almacén sellado, una voz idéntica a la de Elena: «Alex... estoy aquí»)", 0f);
            Scare(story, "Scare_F06_LabA_ShelfFall", new Vector3(-8f, 0.45f, 3f), new Vector3(2f, 0.9f, 0.8f), rig.ShelfFall, Clip("SFX/SFX_F06_ObjectFall"), 0.9f,
                "(Algo metálico cae de una estantería al fondo del laboratorio)", 30f);

            // Silueta que solo aparece en la cámara del Laboratorio B y se esfuma al entrar
            Material shadow = SolidMaterial("MAT_F06_Silhouette", new Color(0.01f, 0.01f, 0.012f));
            var silhouette = new GameObject("F06_Silhouette");
            silhouette.transform.SetParent(props, false);
            silhouette.transform.position = new Vector3(13.2f, 0f, 8.6f);
            silhouette.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            Primitive(PrimitiveType.Capsule, silhouette.transform, "Body", new Vector3(0f, 0.85f, 0f), Vector3.zero, new Vector3(0.42f, 0.85f, 0.3f), shadow, false, true);
            Primitive(PrimitiveType.Sphere, silhouette.transform, "Head", new Vector3(0.04f, 1.78f, 0.02f), new Vector3(0f, 0f, 12f), new Vector3(0.24f, 0.28f, 0.24f), shadow, false, true);
            GameObject trigger = TriggerBox(story, "Apparition_F06_LabB", new Vector3(8f, 0.45f, 3.4f), new Vector3(2f, 0.9f, 0.6f));
            var apparition = trigger.AddComponent<CameraFeedApparition>();
            var aso = new SerializedObject(apparition);
            aso.FindProperty("_feedId").stringValue = "F06-03";
            aso.FindProperty("_silhouette").objectReferenceValue = silhouette;
            aso.ApplyModifiedPropertiesWithoutUndo();
            silhouette.SetActive(false);
        }

        private static void Scare(Transform parent, string name, Vector3 position, Vector3 size, AudioSource source, AudioClip clip, float volume, string subtitle, float noise)
        {
            GameObject go = TriggerBox(parent, name, position, size);
            var scare = go.AddComponent<ScareTrigger>();
            var so = new SerializedObject(scare);
            so.FindProperty("_source").objectReferenceValue = source;
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.FindProperty("_volume").floatValue = volume;
            so.FindProperty("_subtitle").stringValue = subtitle;
            so.FindProperty("_noiseLoudness").floatValue = noise;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- Infectados ----------------

        private static void BuildInfected(Transform waypoints)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SmartInfectedBrain>(InfectedPrefab);
            string[] mimicLines =
            {
                "Alex, soy Elena. Estoy en el almacén. Ven, rápido.",
                "Apaga la linterna, Alex. Así no nos ven.",
                "Las escaleras están por el otro lado. Gira y corre.",
                "Alex... ¿dónde estás? No me dejes sola."
            };

            // 1) Pasillo: patrulla lenta que APRENDE (disparos, carreras, escondites)
            Spawn(waypoints, prefab, "Spawn_F06_Corridor_Learner", new Vector3(12.5f, 0f, 0.2f), -90f,
                new[] { new Vector3(12.5f, 0f, 0.2f), new Vector3(-3.5f, 0f, -0.3f) },
                Overrides(InfectedBehavior.Patroller, learning: true, sight: 12f));

            // 2) Laboratorio A: centinela inmóvil de espaldas a la puerta
            Spawn(waypoints, prefab, "Spawn_F06_LabA_Sentinel", new Vector3(-13.6f, 0f, 8.7f), 0f, null,
                Overrides(InfectedBehavior.Sentinel, sight: 10f));

            // 3) Laboratorio B: el imitador (oído fino, voces falsas de Elena)
            var mimic = Overrides(InfectedBehavior.Listener, sight: 9f);
            mimic.MimicryEnabled = true;
            mimic.MimicryCooldown = 40f;
            mimic.MimicMessages = mimicLines;
            Spawn(waypoints, prefab, "Spawn_F06_LabB_Mimic", new Vector3(3.2f, 0f, 8.6f), 90f,
                new[] { new Vector3(3.2f, 0f, 8.6f), new Vector3(6.8f, 0f, 4.6f), new Vector3(3f, 0f, 4.4f) }, mimic);

            // 4) Contención: encuentro especial tras la puerta que la voz falsa pide abrir
            Spawn(waypoints, prefab, "Spawn_F06_Containment_Trap", new Vector3(-12f, 0f, -6.6f), 42f, null,
                Overrides(InfectedBehavior.Sentinel, sight: 11f));

            // 5) Persecución corta: espera al fondo del Lab B y acude al ruido de la puerta de las escaleras
            Spawn(waypoints, prefab, "Spawn_F06_Stairs_Chaser", new Vector3(14.4f, 0f, 8.4f), 200f, null,
                Overrides(InfectedBehavior.Listener, sight: 12f));
        }

        private static InfectedSpawnOverrides Overrides(InfectedBehavior behavior, bool learning = false, float sight = 0f, float hearing = 0f)
        {
            return new InfectedSpawnOverrides
            {
                Enabled = true,
                Behavior = behavior,
                GradedDetection = true,
                MimicryEnabled = false,
                SabotageEnabled = false,
                EnableLearning = learning,
                SightDistance = sight,
                HearingRange = hearing
            };
        }

        private static void Spawn(Transform parent, SmartInfectedBrain prefab, string name, Vector3 position, float yaw, Vector3[] patrol, InfectedSpawnOverrides overrides)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var spawn = go.AddComponent<InfectedSpawnPoint>();

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

            var so = new SerializedObject(spawn);
            so.FindProperty("_infectedPrefab").objectReferenceValue = prefab;
            SetArray(so.FindProperty("_patrolPoints"), points);
            SerializedProperty o = so.FindProperty("_overrides");
            o.FindPropertyRelative("Enabled").boolValue = overrides.Enabled;
            o.FindPropertyRelative("Behavior").enumValueIndex = (int)overrides.Behavior;
            o.FindPropertyRelative("GradedDetection").boolValue = overrides.GradedDetection;
            o.FindPropertyRelative("MimicryEnabled").boolValue = overrides.MimicryEnabled;
            o.FindPropertyRelative("SabotageEnabled").boolValue = overrides.SabotageEnabled;
            o.FindPropertyRelative("EnableLearning").boolValue = overrides.EnableLearning;
            o.FindPropertyRelative("SightDistance").floatValue = overrides.SightDistance;
            o.FindPropertyRelative("HearingRange").floatValue = overrides.HearingRange;
            o.FindPropertyRelative("MimicryCooldown").floatValue = overrides.MimicryCooldown;
            SetArray(o.FindPropertyRelative("MimicMessages"), new List<string>(overrides.MimicMessages ?? new string[0]));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- NavMesh ----------------

        private static void PrepareDoorsForNavigation()
        {
            // Las puertas no se hornean: un NavMeshObstacle con carving las cierra o abre en tiempo real
            foreach (string doorName in new[] { "F06_Door_Stairs", "F06_Door_Lab" })
            {
                DoorController door = FindComponent<DoorController>(doorName);
                if (door == null) continue;
                var modifier = door.GetComponent<NavMeshModifier>();
                if (modifier == null) modifier = door.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;

                var obstacle = door.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = door.gameObject.AddComponent<NavMeshObstacle>();
                var box = door.GetComponent<BoxCollider>();
                obstacle.shape = NavMeshObstacleShape.Box;
                if (box != null) { obstacle.center = box.center; obstacle.size = box.size; }
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }

            // El vano de Contención (1.2 m) es más estrecho que el radio de horneado: enlace activo solo con la puerta abierta
            DoorController lab = FindComponent<DoorController>("F06_Door_Lab");
            GameObject root = FindRoot(RootName);
            if (lab != null && root != null)
            {
                var go = new GameObject("F06_Link_ContainmentDoor");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(-8f, 0f, -2f);
                var link = go.AddComponent<NavMeshLink>();
                link.agentTypeID = 0;
                link.startPoint = new Vector3(0f, 0f, 1.1f);
                link.endPoint = new Vector3(0f, 0f, -1.1f);
                link.width = 0.9f;
                link.bidirectional = true;
                var doorLink = go.AddComponent<DoorNavMeshLink>();
                var so = new SerializedObject(doorLink);
                so.FindProperty("_door").objectReferenceValue = lab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BakeNavMesh(GameObject root)
        {
            var navigation = new GameObject("F06_Navigation");
            navigation.transform.SetParent(root.transform, false);
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << 0) | (1 << 11); // Default | Environment
            surface.agentTypeID = 0; // Humanoid

            // Solo la escena del piso: los objetos de Systems_Persistent (jugador) no deben hornearse
            var hidden = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene other = SceneManager.GetSceneAt(i);
                if (other == _scene || !other.isLoaded) continue;
                foreach (GameObject go in other.GetRootGameObjects())
                {
                    if (!go.activeSelf) continue;
                    go.SetActive(false);
                    hidden.Add(go);
                }
            }

            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (GameObject go in hidden) go.SetActive(true);
            }

            Directory.CreateDirectory(NavMeshFolder);
            string path = NavMeshFolder + "/NavMesh-Floor06_Labs.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            surface.RemoveData();
            surface.AddData();
        }

        // ---------------- Utilidades ----------------

        private static StorySequence.Line L(StorySequence.Speaker speaker, string text, float delay, string emotion = "calm",
            StorySequence.SceneAction action = StorySequence.SceneAction.None, string param = "")
        {
            return new StorySequence.Line { Speaker = speaker, Text = text, Delay = delay, Emotion = emotion, Action = action, ActionParam = param };
        }

        private static StorySequence Sequence(Transform parent, string name, StorySequence.TriggerMode mode, Vector3 position, Vector3 size, string doorId, params StorySequence.Line[] lines)
        {
            GameObject go = mode == StorySequence.TriggerMode.OnPlayerEnter ? TriggerBox(parent, name, position, size) : new GameObject(name);
            if (go.transform.parent == null) go.transform.SetParent(parent, false);
            var sequence = go.AddComponent<StorySequence>();
            var so = new SerializedObject(sequence);
            so.FindProperty("_mode").enumValueIndex = (int)mode;
            so.FindProperty("_doorId").stringValue = doorId ?? "";
            SerializedProperty array = so.FindProperty("_lines");
            array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                SerializedProperty p = array.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("Speaker").enumValueIndex = (int)lines[i].Speaker;
                p.FindPropertyRelative("Text").stringValue = lines[i].Text;
                p.FindPropertyRelative("Delay").floatValue = lines[i].Delay;
                p.FindPropertyRelative("Emotion").stringValue = lines[i].Emotion;
                p.FindPropertyRelative("Glitch").floatValue = lines[i].Speaker == StorySequence.Speaker.SystemError ? 0.4f : 0f;
                p.FindPropertyRelative("Action").enumValueIndex = (int)lines[i].Action;
                p.FindPropertyRelative("ActionParam").stringValue = lines[i].ActionParam;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return sequence;
        }

        /// <summary>Volumen de disparo bajo (0.9 m): el jugador lo cruza pero no tapa el rayo de interacción a la altura de los ojos.</summary>
        private static GameObject TriggerBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return go;
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
            ConfigureSource(source, clip, volume, loop, spatial, minDistance, maxDistance, mixerGroup);
            return source;
        }

        private static void ConfigureSource(AudioSource source, AudioClip clip, float volume, bool loop, float spatial, float minDistance, float maxDistance, string mixerGroup)
        {
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
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material, bool keepCollider, bool local = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (local || (parent != null && parent.GetComponent<Renderer>() != null))
            {
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(euler);
            }
            else
            {
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(euler);
            }
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
            if (Materials.TryGetValue(name, out Material cached)) return cached;

            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = albedo;
            material.SetFloat("_Glossiness", 0.35f);
            if (intensity > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission * intensity);
                // Emisión en tiempo real: el Inspector no le quita la palabra clave (ver MAT_F12_ExitButton)
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        private static AudioClip Clip(string relative)
        {
            foreach (string ext in new[] { ".wav", ".ogg" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/{relative}{ext}");
                if (clip != null) return clip;
            }
            Debug.LogWarning("[NexoFloor06Builder] No se encontró el clip " + relative);
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

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject go in _scene.GetRootGameObjects()) if (go.name == name) return go;
            return null;
        }

        private static T FindComponent<T>(string objectName) where T : Component
        {
            foreach (GameObject go in _scene.GetRootGameObjects())
            {
                foreach (T component in go.GetComponentsInChildren<T>(true))
                {
                    if (component.name == objectName) return component;
                }
            }
            return null;
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
