using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nexo.Cinematics;
using Nexo.Core;
using Nexo.Interaction;
using Nexo.Narrative;
using Nexo.Player;
using Nexo.UI;
using Nexo.UI.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Capturas de verificación visual en Play mode (menú Nexo/Checks/Visual Capture o
    /// -executeMethod Nexo.EditorTools.NexoVisualCapture.Run en batchmode SIN -nographics):
    /// recorre menú → Piso 12 → 6 → 1 → giro → créditos y, en cada piso, renderiza la cámara del jugador en el spawn y en
    /// puntos clave (terminales, salidas, BioScanner, diferencial, notas) con la linterna apagada y encendida. Calcula la
    /// luminancia media (luma Rec. 709 sobre sRGB, 0-255), el % de píxeles casi negros (luma &lt; 20) y cuenta las luces y
    /// las que proyectan sombra cerca de la cámara. También captura las cámaras de las terminales y la UI real (HUD con el
    /// retrato de Elena, terminal, red de cámaras y créditos) compuesta sobre la escena.
    /// Resultado: Logs/NexoVisualCapture.txt y PNG en Logs/VisualCapture/ (carpeta ignorada por Git). No modifica assets:
    /// PanelSettings y cámaras se restauran al terminar cada captura.
    /// </summary>
    [InitializeOnLoad]
    public static class NexoVisualCapture
    {
        private const string ActiveKey = "Nexo.VisualCapture.Active";
        private const string ExitKey = "Nexo.VisualCapture.ExitCode";
        private const string PersistentScene = "Assets/_Project/Scenes/Persistent/Systems_Persistent.unity";
        private const int Width = 1280;
        private const int Height = 720;
        private const int UiWidth = 1920;
        private const int UiHeight = 1080;
        private const float DarkLuma = 20f;
        private const float WarnMeanLuma = 30f;
        private const int MaxNotesPerFloor = 4;

        private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        private static readonly List<string> Lines = new List<string>();
        private static double _deadline;
        private static bool _started;
        private static int _warnings;
        private static int _errors;
        private static string _outDir;

        static NexoVisualCapture()
        {
            if (SessionState.GetInt(ExitKey, -1) >= 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int code = SessionState.GetInt(ExitKey, 1);
                SessionState.EraseInt(ExitKey);
                if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(code);
                return;
            }
            if (SessionState.GetBool(ActiveKey, false)) Hook();
        }

        [MenuItem("Nexo/Checks/Visual Capture (Play mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NexoVisualCapture] Sal de Play antes de lanzar la captura.");
                return;
            }
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(PersistentScene, OpenSceneMode.Single);
            Hook();
            EditorApplication.EnterPlaymode();
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceived += HandleLog;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            int code = SessionState.GetInt(ExitKey, -1);
            if (code >= 0 && Application.isBatchMode)
            {
                SessionState.EraseInt(ExitKey);
                EditorApplication.Exit(code);
            }
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (condition.Contains("manager != NULL")) return;
            _errors++;
            if (_errors <= 8) Lines.Add("ERROR consola: " + condition);
        }

        private static double Now => EditorApplication.timeSinceStartup;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (!_started)
            {
                _started = true;
                _deadline = Now + 600d;
                _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "VisualCapture"));
                Directory.CreateDirectory(_outDir);
                Application.runInBackground = true;
                Stack.Clear();
                Stack.Push(Scenario());
            }
            if (Now > _deadline)
            {
                Log("FALLO: tiempo global agotado");
                _warnings++;
                End();
                return;
            }
            try
            {
                while (Stack.Count > 0)
                {
                    IEnumerator top = Stack.Peek();
                    if (!top.MoveNext())
                    {
                        Stack.Pop();
                        continue;
                    }
                    if (top.Current is IEnumerator nested)
                    {
                        Stack.Push(nested);
                        continue;
                    }
                    return;
                }
                End();
            }
            catch (Exception e)
            {
                Log("FALLO: excepción " + e);
                _warnings++;
                Time.timeScale = 1f;
                End();
            }
        }

        // ---------- Escenario ----------

        private static IEnumerator Scenario()
        {
            yield return WaitFor(() => Menu() != null && Menu().Current == ScreenId.MainMenu && FloorLoaded("Floor12_Offices"), 90, "menú principal");
            yield return WaitSeconds(1.0);
            yield return CaptureUi("ui_menu_main");

            MenuEvents.RaisePlayRequested();
            yield return SkipCinematics();
            yield return WaitFor(() => FloorLoaded("Floor12_Offices") && Menu().Current == ScreenId.None, 30, "Piso 12 en juego");
            yield return WaitSeconds(3.0);
            GameEvents.RaiseQuickQuestionSelected(0); // el panel de radio muestra el retrato y un diálogo
            yield return WaitSeconds(1.5);
            yield return CaptureUi("ui_hud_f12_portrait");
            yield return FloorCaptures("Floor12_Offices", "f12");

            for (int step = 0; step < 2; step++)
            {
                FloorProgression.Active.LoadNext();
                yield return null;
                yield return SkipCinematics();
                string scene = step == 0 ? "Floor06_Labs" : "Floor01_Lobby";
                yield return WaitFor(() => FloorLoaded(scene) && !FloorProgression.Active.IsBusy, 60, scene + " cargado");
                yield return WaitSeconds(3.0);
                yield return FloorCaptures(scene, step == 0 ? "f06" : "f01");
            }

            // Giro → créditos (la lógica del giro no se toca: solo se dispara el evento del BioScanner)
            NarrativeEvents.RaiseBioScannerHandPlaced();
            yield return WaitSeconds(4.0);
            yield return CaptureUi("ui_twist_hud");
            double limit = Now + 60;
            while (Menu().Current != ScreenId.Credits && Now < limit)
            {
                if (CinematicService.IsPlaying && CinematicService.Elapsed > 0.6f) CinematicService.RequestSkip();
                yield return null;
            }
            yield return WaitSeconds(1.0);
            yield return CaptureUi("ui_credits");
        }

        private static IEnumerator FloorCaptures(string sceneName, string tag)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
            Camera cam = player != null ? player.GetComponentInChildren<Camera>(true) : null;
            if (player == null || cam == null)
            {
                Log($"FALLO {tag}: sin jugador o cámara");
                _warnings++;
                yield break;
            }
            Light flashlight = FindFlashlight(player);
            Transform rig = CommonRig(cam.transform, flashlight != null ? flashlight.transform : null, player.transform);

            LightCensus(scene, tag);

            // Pose real del spawn (como la ve el jugador al llegar)
            Vector3 spawnPos = player.transform.position;
            Quaternion spawnRot = player.transform.rotation;
            Quaternion rigLocal = rig.localRotation;
            float previousScale = Time.timeScale;
            Time.timeScale = 0f; // IA, físicas y disparadores congelados mientras se mueve la cámara

            CaptureView($"{tag}_spawn", cam, flashlight, scene, null);

            foreach ((string label, Transform target) in KeyPoints(scene))
            {
                if (!PlaceFacing(player, rig, cam, target, spawnPos)) continue;
                CaptureView($"{tag}_{label}", cam, flashlight, scene, target);
            }

            Teleport(player, spawnPos, spawnRot);
            rig.localRotation = rigLocal;
            Time.timeScale = previousScale;
            yield return null;

            yield return TerminalCaptures(scene, tag);
        }

        private static IEnumerator TerminalCaptures(Scene scene, string tag)
        {
            SecurityTerminal terminal = FindInScene<SecurityTerminal>(scene);
            TerminalController ui = Object.FindAnyObjectByType<TerminalController>();
            CameraNetworkController network = Object.FindAnyObjectByType<CameraNetworkController>();
            if (terminal == null || ui == null)
            {
                Log($"{tag}: sin terminal de seguridad");
                yield break;
            }
            CursorLockMode lockBefore = UnityEngine.Cursor.lockState;
            terminal.Interact();
            yield return WaitSeconds(0.6);
            for (int i = 0; i < terminal.FeedCount; i++)
            {
                RenderTexture rt = terminal.GetFeedTexture(i);
                Camera feedCamera = FindCameraFor(rt);
                if (rt == null || feedCamera == null) continue;
                feedCamera.Render();
                Texture2D tex = Read(rt);
                Stats(tex, out float mean, out float dark);
                string name = $"{tag}_terminal_feed{i + 1}";
                Save(tex, name);
                Report(name, $"{terminal.GetFeedLabel(i)} · RT {rt.width}x{rt.height} · luma media {mean:0.0} · casi negro {dark:0}%", mean);
            }
            yield return CaptureUi($"ui_{tag}_terminal");
            if (network != null)
            {
                network.OpenNetwork();
                yield return WaitSeconds(0.8);
                yield return CaptureUi($"ui_{tag}_camera_network");
                network.CloseNetwork();
            }
            ui.Close();
            yield return WaitSeconds(0.3);
            PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
            bool restored = !ui.IsOpen && (network == null || !network.IsNetworkOpen) && player != null && !player.IsInputLocked && UnityEngine.Cursor.lockState == lockBefore;
            Log($"{tag}: terminal cerrada → control y cursor restaurados: {(restored ? "sí" : "NO")} (cursor {UnityEngine.Cursor.lockState})");
            if (!restored) _warnings++;
        }

        // ---------- Puntos clave ----------

        private static IEnumerable<(string, Transform)> KeyPoints(Scene scene)
        {
            var result = new List<(string, Transform)>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SecurityTerminal t in root.GetComponentsInChildren<SecurityTerminal>(false)) result.Add(("terminal", t.transform));
                foreach (StairsExit t in root.GetComponentsInChildren<StairsExit>(false)) result.Add(("exit", t.transform));
                foreach (BioScanner t in root.GetComponentsInChildren<BioScanner>(false)) result.Add(("bioscanner", t.transform));
                foreach (PowerBreaker t in root.GetComponentsInChildren<PowerBreaker>(false)) result.Add(("breaker", t.transform));
            }
            int notes = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (LoreNote t in root.GetComponentsInChildren<LoreNote>(false))
                {
                    if (notes++ >= MaxNotesPerFloor) break;
                    result.Add(("note" + notes, t.transform));
                }
            }
            return result;
        }

        private static bool PlaceFacing(PlayerMovement player, Transform rig, Camera cam, Transform target, Vector3 spawn)
        {
            Vector3 toSpawn = spawn - target.position;
            toSpawn.y = 0f;
            Vector3 baseDir = toSpawn.sqrMagnitude > 0.01f ? toSpawn.normalized : Vector3.forward;
            float eyeOffset = cam.transform.position.y - player.transform.position.y;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * baseDir;
                Vector3 candidate = target.position + dir * 2.6f;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                Vector3 eye = hit.position + Vector3.up * eyeOffset;
                Vector3 focus = target.position + Vector3.up * 0.4f;
                if (Physics.Linecast(eye, focus, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore) && !block.transform.IsChildOf(target) && block.distance < Vector3.Distance(eye, focus) - 0.6f) continue;
                Vector3 flat = focus - eye;
                flat.y = 0f;
                Teleport(player, hit.position, Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat : Vector3.forward));
                rig.rotation = Quaternion.LookRotation(focus - cam.transform.position);
                return true;
            }
            Log($"   (sin punto de vista libre para {target.name})");
            return false;
        }

        private static void Teleport(PlayerMovement player, Vector3 position, Quaternion rotation)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            if (controller != null) controller.enabled = true;
        }

        private static Transform CommonRig(Transform cam, Transform flashlight, Transform player)
        {
            if (flashlight == null) return cam;
            for (Transform t = cam; t != null && t != player; t = t.parent)
            {
                if (flashlight.IsChildOf(t)) return t;
            }
            return cam;
        }

        private static Light FindFlashlight(PlayerMovement player)
        {
            Light best = null;
            foreach (Light l in player.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Spot) continue;
                if (best == null || l.range > best.range) best = l;
            }
            return best;
        }

        // ---------- Captura y métricas ----------

        private static void CaptureView(string name, Camera cam, Light flashlight, Scene scene, Transform target)
        {
            bool flashWasOn = flashlight != null && flashlight.enabled;
            for (int pass = 0; pass < 2; pass++)
            {
                bool on = pass == 1;
                if (flashlight != null) flashlight.enabled = on;
                Texture2D tex = RenderCamera(cam);
                Stats(tex, out float mean, out float dark);
                string file = name + (on ? "_lamp" : string.Empty);
                Save(tex, file);
                string lights = on ? string.Empty : " · " + NearbyLights(scene, cam.transform.position);
                Report(file, $"linterna {(on ? "ON " : "OFF")} · luma media {mean:0.0} · casi negro {dark:0}%{lights}", on ? 999f : mean);
            }
            if (flashlight != null) flashlight.enabled = flashWasOn;
        }

        private static Texture2D RenderCamera(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            Texture2D tex = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static IEnumerator CaptureUi(string name)
        {
            UIDocument doc = null;
            HUDController hud = Object.FindAnyObjectByType<HUDController>();
            if (hud != null) doc = hud.GetComponent<UIDocument>();
            if (doc == null && Menu() != null) doc = Menu().GetComponent<UIDocument>();
            PanelSettings panel = doc != null ? doc.panelSettings : null;
            PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
            Camera cam = player != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
            if (panel == null || cam == null)
            {
                Log($"FALLO {name}: sin PanelSettings o cámara");
                _warnings++;
                yield break;
            }

            var rt = new RenderTexture(UiWidth, UiHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = panel.targetTexture;
            bool previousClear = panel.clearColor;
            RenderTexture previousCamTarget = cam.targetTexture;
            try
            {
                // La cámara pinta la escena en la RT y el panel de UI Toolkit se dibuja encima (sin limpiar)
                cam.targetTexture = rt;
                panel.targetTexture = rt;
                panel.clearColor = false;
                for (int i = 0; i < 4; i++) yield return null;
                Texture2D tex = Read(rt);
                Stats(tex, out float mean, out float dark);
                Save(tex, name);
                Report(name, $"UI {UiWidth}x{UiHeight} sobre la escena · luma media {mean:0.0}", 999f);
            }
            finally
            {
                panel.targetTexture = previousTarget;
                panel.clearColor = previousClear;
                cam.targetTexture = previousCamTarget;
                rt.Release();
                Object.Destroy(rt);
            }
        }

        private static Texture2D Read(RenderTexture rt)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        private static void Stats(Texture2D tex, out float mean, out float darkPercent)
        {
            Color32[] px = tex.GetPixels32();
            double sum = 0;
            int dark = 0;
            foreach (Color32 c in px)
            {
                float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += luma;
                if (luma < DarkLuma) dark++;
            }
            mean = (float)(sum / px.Length);
            darkPercent = 100f * dark / px.Length;
        }

        private static void Save(Texture2D tex, string name)
        {
            File.WriteAllBytes(Path.Combine(_outDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        private static void Report(string name, string text, float mean)
        {
            bool warn = mean < WarnMeanLuma;
            if (warn) _warnings++;
            Log($"{(warn ? "OSCURO " : "OK     ")}{name}: {text}");
        }

        private static void LightCensus(Scene scene, string tag)
        {
            int total = 0, realtime = 0, shadows = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light l in root.GetComponentsInChildren<Light>(false))
                {
                    if (!l.isActiveAndEnabled) continue;
                    total++;
                    if (l.lightmapBakeType != LightmapBakeType.Baked) realtime++;
                    if (l.shadows != LightShadows.None) shadows++;
                }
            }
            Log($"== {tag} ({scene.name}): {total} luces activas, {realtime} en tiempo real, {shadows} con sombras · ambiente {RenderSettings.ambientMode} {RenderSettings.ambientLight} x{RenderSettings.ambientIntensity:0.00} · niebla {(RenderSettings.fog ? $"{RenderSettings.fogMode} densidad {RenderSettings.fogDensity:0.000}" : "no")} · escena activa {SceneManager.GetActiveScene().name}");
        }

        private static string NearbyLights(Scene scene, Vector3 eye)
        {
            int lit = 0, shadows = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light l in root.GetComponentsInChildren<Light>(false))
                {
                    if (!l.isActiveAndEnabled) continue;
                    bool reaches = l.type == LightType.Directional || Vector3.Distance(l.transform.position, eye) < l.range;
                    if (!reaches) continue;
                    lit++;
                    if (l.shadows != LightShadows.None) shadows++;
                }
            }
            return $"luces que alcanzan la cámara {lit} (con sombra {shadows})";
        }

        // ---------- Utilidades ----------

        private static IEnumerator SkipCinematics()
        {
            double limit = Now + 30;
            yield return null;
            while (CinematicService.IsPlaying && Now < limit)
            {
                if (CinematicService.Elapsed > 0.6f) CinematicService.RequestSkip();
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, double seconds, string label)
        {
            double start = Now;
            while (true)
            {
                bool ok;
                try { ok = condition(); }
                catch (Exception) { ok = false; }
                if (ok) yield break;
                if (Now - start > seconds)
                {
                    Log("FALLO: tiempo agotado: " + label);
                    _warnings++;
                    yield break;
                }
                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(double seconds)
        {
            double end = Now + seconds;
            while (Now < end) yield return null;
        }

        private static MenuFlowController Menu() => Object.FindAnyObjectByType<MenuFlowController>();
        private static bool FloorLoaded(string scene) => SceneManager.GetSceneByName(scene).isLoaded;

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(false);
                if (found != null) return found;
            }
            return null;
        }

        private static Camera FindCameraFor(RenderTexture rt)
        {
            if (rt == null) return null;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (c.targetTexture == rt) return c;
            }
            return null;
        }

        private static void Log(string text)
        {
            Lines.Add(text);
            Debug.Log("[NexoVisualCapture] " + text);
        }

        private static void End()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
            Stack.Clear();
            var sb = new StringBuilder();
            sb.AppendLine($"NexoVisualCapture {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {_warnings} avisos, {_errors} errores de consola. PNG en {_outDir}");
            foreach (string line in Lines) sb.AppendLine(line);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NexoVisualCapture.txt"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[NexoVisualCapture] Resultado → " + path);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(ExitKey, 0);
            Application.runInBackground = false;
            _started = false;
            EditorApplication.isPlaying = false;
            if (!Application.isBatchMode) SessionState.EraseInt(ExitKey);
        }
    }
}
