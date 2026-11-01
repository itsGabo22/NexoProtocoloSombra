using System.Collections.Generic;
using System.IO;
using Nexo.AI.Elena;
using Nexo.Accessibility;
using Nexo.Core;
using Nexo.Player;
using Nexo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Asistente de la Fase D: crea en disco los assets base (PanelSettings, RenderTexture, perfiles de Volume,
    /// ElenaConfig/Secrets), la escena Systems_Persistent con PF_Player, PF_HUD y PF_ElenaService,
    /// y enlaza por código todas las referencias entre componentes.
    /// </summary>
    public static class NexoSceneSetupWizard
    {
        private const string Root = "Assets/_Project";
        private const string PanelSettingsPath = Root + "/UI/Panel/PS_Nexo.asset";
        private const string ThemePath = Root + "/UI/Panel/NexoRuntimeTheme.tss";
        private const string RenderTexturePath = Root + "/Materials/RT_Cam_F12_01.renderTexture";
        private const string ThermalProfilePath = Root + "/Lighting/VolumeProfiles/VP_Thermal.asset";
        private const string Floor12ProfilePath = Root + "/Lighting/VolumeProfiles/VP_Floor12.asset";
        private const string ElenaConfigPath = Root + "/Data/ElenaConfig.asset";
        private const string ElenaSecretsPath = Root + "/Data/Secrets/ElenaSecrets.asset";
        private const string InputActionsPath = Root + "/Input/NexoControls.inputactions";
        private const string ScenePath = Root + "/Scenes/Persistent/Systems_Persistent.unity";
        private const string FloorScenePath = Root + "/Scenes/Floors/Floor12_Offices.unity";
        private const string PrefabFolder = Root + "/Prefabs/Systems";
        private const string PlayerModelPath = Root + "/Models/Characters/Ch33_nonPBR1.fbx";
        private const string PlayerAnimatorPath = Root + "/Animations/Controllers/AC_Alex.controller";
        private const string HudLayoutPath = Root + "/UI/Layouts/HUD_Biometric.uxml";
        private const string TerminalLayoutPath = Root + "/UI/Layouts/SecurityTerminal.uxml";
        private const string OrderWheelLayoutPath = Root + "/UI/Layouts/OrderWheel.uxml";
        private const string SystemPromptPath = Root + "/Data/ElenaSystemPrompt.txt";
        private const string FallbackPath = Root + "/Data/ElenaFallback/fallback_floor12.json";

        [MenuItem("Nexo/Setup Systems_Persistent & Assets")]
        public static void Run()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Nexo Setup",
                    "Systems_Persistent.unity ya existe. ¿Regenerar la escena y los prefabs PF_Player, PF_HUD y PF_ElenaService?\n(Los assets existentes se reutilizan, no se sobrescriben.)",
                    "Regenerar", "Cancelar"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                // Abrir la escena vacía ANTES de cargar assets: NewScene(Single) descarga los assets
                // sin referencias y dejaría destruidos los que ya tuviéramos en memoria
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EditorUtility.DisplayProgressBar("Nexo Setup", "Creando assets base...", 0.2f);
                PanelSettings panel = GetOrCreatePanelSettings();
                GetOrCreateRenderTexture();
                VolumeProfile thermalProfile = GetOrCreateThermalProfile();
                GetOrCreateFloor12Profile();
                ElenaSecrets secrets = GetOrCreateSecrets();
                ElenaConfig config = GetOrCreateElenaConfig(secrets);

                EditorUtility.DisplayProgressBar("Nexo Setup", "Construyendo Systems_Persistent...", 0.6f);
                BuildPersistentScene(scene, panel, thermalProfile, config, secrets);

                EditorUtility.DisplayProgressBar("Nexo Setup", "Registrando escenas en Build Profiles...", 0.9f);
                RegisterBuildScenes();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[NexoSetup] Systems_Persistent y assets base listos. Siguiente paso: configurar Floor12_Offices.unity.");
        }

        // ---------- Escena persistente ----------

        private static void BuildPersistentScene(Scene scene, PanelSettings panel, VolumeProfile thermalProfile, ElenaConfig config, ElenaSecrets secrets)
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EnsureFolder(PrefabFolder);

            // --- Systems ---
            var systems = new GameObject("Systems");
            var tracker = systems.AddComponent<GameStateTracker>();
            var loader = systems.AddComponent<SceneLoader>();
            var capture = systems.AddComponent<PlayerCaptureHandler>();

            // --- PF_Player ---
            GameObject player = BuildPlayer(thermalProfile, out PlayerMovement movement, out PlayerInteractor interactor, out ThermalVisionController thermal);

            // --- PF_HUD ---
            GameObject hud = BuildHud(panel, out HUDController hudController, out TerminalController terminal, out OrderWheelController wheel);

            // --- PF_ElenaService ---
            var elenaGo = new GameObject("PF_ElenaService");
            var elena = elenaGo.AddComponent<ElenaAIService>();
            SetRefs(elena, ("_config", config), ("_secrets", secrets));

            // Guardar prefabs (solo referencias internas) y conectar las instancias de la escena
            SavePrefab(player, "PF_Player");
            SavePrefab(hud, "PF_HUD");
            SavePrefab(elenaGo, "PF_ElenaService");

            // Referencias cruzadas entre prefabs: quedan como overrides de escena
            SetString(loader, "_floorSceneName", "Floor12_Offices");
            SetRefs(loader, ("_player", player.transform));
            SetRefs(capture, ("_sceneLoader", loader), ("_playerMovement", movement));
            SetRefs(interactor, ("_hudController", hudController));
            SetRefs(thermal, ("_stateTracker", tracker));
            SetRefs(terminal, ("_playerMovement", movement));
            SetRefs(wheel, ("_playerMovement", movement));
            SetRefs(elena, ("_stateTracker", tracker));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject BuildPlayer(VolumeProfile thermalProfile, out PlayerMovement movement, out PlayerInteractor interactor, out ThermalVisionController thermal)
        {
            var player = new GameObject("PF_Player");

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            // Modelo Mixamo con AC_Alex (opcional si los assets existen)
            Animator animator = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model != null)
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, player.transform);
                modelInstance.transform.localPosition = Vector3.zero;
                animator = modelInstance.GetComponent<Animator>();
                if (animator == null) animator = modelInstance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
                if (animatorController != null) animator.runtimeAnimatorController = animatorController;
            }
            else
            {
                Debug.LogWarning($"[NexoSetup] No se encontró {PlayerModelPath}: PF_Player se crea sin modelo.");
            }

            // CameraHolder estable a la altura de los ojos (desacoplado del hueso de la cabeza)
            var cameraHolder = new GameObject("CameraHolder");
            cameraHolder.transform.SetParent(player.transform, false);
            cameraHolder.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraHolder.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;
            cameraGo.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var spotGo = new GameObject("Flashlight");
            spotGo.transform.SetParent(cameraGo.transform, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 18f;
            spot.spotAngle = 55f;
            spot.intensity = 6f;
            spot.shadows = LightShadows.Soft;
            spot.enabled = false;

            // Volume térmico local al jugador: las referencias no cruzan escenas
            var thermalGo = new GameObject("ThermalVolume");
            thermalGo.transform.SetParent(player.transform, false);
            var volume = thermalGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = thermalProfile;

            movement = player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerStamina>();
            player.AddComponent<NoiseEmitter>();
            var flashlight = player.AddComponent<FlashlightSystem>();
            thermal = player.AddComponent<ThermalVisionController>();
            interactor = player.AddComponent<PlayerInteractor>();

            SetRefs(movement, ("_cameraPivot", cameraHolder.transform), ("_characterAnimator", animator));
            SetRefs(flashlight, ("_spotlight", spot));
            SetRefs(thermal, ("_thermalVolume", volume));
            SetRefs(interactor, ("_cameraTransform", cameraGo.transform), ("_interactAction", FindActionReference("Player", "Interact")));
            SetLayerMask(interactor, "_interactableMask", LayerMask.GetMask("Default", "Interactable"));

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) SetLayerRecursively(player, playerLayer);

            return player;
        }

        private static GameObject BuildHud(PanelSettings panel, out HUDController hudController, out TerminalController terminal, out OrderWheelController wheel)
        {
            var hud = new GameObject("PF_HUD");

            GameObject hudLayer = CreateDocument(hud.transform, "HUD", panel, HudLayoutPath, 0f);
            hudController = hudLayer.AddComponent<HUDController>();
            hudLayer.AddComponent<SubtitleController>();
            hudLayer.AddComponent<AccessibilityApplier>();
            var quick = hudLayer.AddComponent<QuickQuestionInput>();
            SetRefs(quick,
                ("_quickQuestion1", FindActionReference("UI", "QuickQuestion1")),
                ("_quickQuestion2", FindActionReference("UI", "QuickQuestion2")),
                ("_quickQuestion3", FindActionReference("UI", "QuickQuestion3")));

            GameObject terminalLayer = CreateDocument(hud.transform, "Terminal", panel, TerminalLayoutPath, 10f);
            terminal = terminalLayer.AddComponent<TerminalController>();
            SetRefs(terminal, ("_closeAction", FindActionReference("Player", "Pause")));

            GameObject wheelLayer = CreateDocument(hud.transform, "OrderWheel", panel, OrderWheelLayoutPath, 20f);
            wheel = wheelLayer.AddComponent<OrderWheelController>();
            SetRefs(wheel,
                ("_wheelAction", FindActionReference("Player", "OrderWheel")),
                ("_cancelAction", FindActionReference("Player", "Pause")),
                ("_terminalController", terminal));

            return hud;
        }

        private static GameObject CreateDocument(Transform parent, string name, PanelSettings panel, string layoutPath, float sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var document = go.AddComponent<UIDocument>();
            // El setter de panelSettings no persiste en modo edición: se asigna el campo serializado
            SetRefs(document, ("m_PanelSettings", panel));
            document.sortingOrder = sortingOrder;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(layoutPath);
            if (document.visualTreeAsset == null) Debug.LogWarning($"[NexoSetup] No se encontró el layout {layoutPath}.");

            return go;
        }

        private static void SavePrefab(GameObject instance, string prefabName)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
        }

        private static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath || s.path == FloorScenePath);

            // Systems_Persistent primero (escena de arranque hasta que exista MainMenu); luego el piso
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (File.Exists(FloorScenePath)) scenes.Insert(1, new EditorBuildSettingsScene(FloorScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Assets base ----------

        private static PanelSettings GetOrCreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null) return panel;

            EnsureFolder(Path.GetDirectoryName(PanelSettingsPath));
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.themeStyleSheet = GetOrCreateTheme();

            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            return panel;
        }

        private static ThemeStyleSheet GetOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null) return theme;

            // Tema runtime por defecto de Unity (botones y controles básicos)
            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        }

        private static RenderTexture GetOrCreateRenderTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (texture != null) return texture;

            EnsureFolder(Path.GetDirectoryName(RenderTexturePath));
            texture = new RenderTexture(256, 144, 24) { name = "RT_Cam_F12_01" };
            AssetDatabase.CreateAsset(texture, RenderTexturePath);
            return texture;
        }

        private static VolumeProfile GetOrCreateThermalProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ThermalProfilePath);
            if (profile != null) return profile;

            profile = CreateProfile(ThermalProfilePath);

            var color = AddOverride<ColorAdjustments>(profile);
            color.colorFilter.Override(new Color(0.55f, 0.95f, 1f));
            color.saturation.Override(-40f);
            color.contrast.Override(15f);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.35f);

            var aberration = AddOverride<ChromaticAberration>(profile);
            aberration.intensity.Override(0.2f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile GetOrCreateFloor12Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Floor12ProfilePath);
            if (profile != null) return profile;

            profile = CreateProfile(Floor12ProfilePath);

            // Oficina fría y limpia: bloom suave y grano de película
            var bloom = AddOverride<Bloom>(profile);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);

            var grain = AddOverride<FilmGrain>(profile);
            grain.intensity.Override(0.2f);

            var color = AddOverride<ColorAdjustments>(profile);
            color.colorFilter.Override(new Color(0.92f, 0.97f, 1f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile CreateProfile(string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static ElenaSecrets GetOrCreateSecrets()
        {
            var secrets = AssetDatabase.LoadAssetAtPath<ElenaSecrets>(ElenaSecretsPath);
            if (secrets != null) return secrets;

            // Data/Secrets/ está en .gitignore: la API key nunca se versiona
            EnsureFolder(Path.GetDirectoryName(ElenaSecretsPath));
            secrets = ScriptableObject.CreateInstance<ElenaSecrets>();
            AssetDatabase.CreateAsset(secrets, ElenaSecretsPath);
            return secrets;
        }

        private static ElenaConfig GetOrCreateElenaConfig(ElenaSecrets secrets)
        {
            var config = AssetDatabase.LoadAssetAtPath<ElenaConfig>(ElenaConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ElenaConfig>();
                AssetDatabase.CreateAsset(config, ElenaConfigPath);
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("_systemPrompt").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SystemPromptPath);
            serialized.FindProperty("_secrets").objectReferenceValue = secrets;

            SerializedProperty fallbacks = serialized.FindProperty("_fallbackFiles");
            fallbacks.arraySize = 1;
            fallbacks.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(FallbackPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return config;
        }

        // ---------- Utilidades ----------

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
            {
                if (asset is InputActionReference reference && reference.action != null &&
                    reference.action.name == actionName && reference.action.actionMap?.name == mapName)
                {
                    return reference;
                }
            }

            Debug.LogWarning($"[NexoSetup] No se encontró la acción {mapName}/{actionName} en {InputActionsPath}.");
            return null;
        }

        private static void SetRefs(Object target, params (string property, Object value)[] refs)
        {
            var serialized = new SerializedObject(target);
            foreach ((string property, Object value) in refs)
            {
                SerializedProperty prop = serialized.FindProperty(property);
                if (prop == null)
                {
                    Debug.LogWarning($"[NexoSetup] {target.GetType().Name} no tiene el campo '{property}'.");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string property, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerMask(Object target, string property, int mask)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
