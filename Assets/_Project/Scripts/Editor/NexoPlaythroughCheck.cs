using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Nexo.AI.Elena;
using Nexo.AI.Infected;
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Recorrido automático en Play mode (menú Nexo/Checks/Playthrough o -executeMethod Nexo.EditorTools.NexoPlaythroughCheck.Run):
    /// arranque → JUGAR (intro) → Piso 12 → LoadNext → Piso 6 → LoadNext → Piso 1 → giro → final → créditos → menú →
    /// JUGAR de nuevo, más atajos de demo, red de cámaras, escondite y forcejeo durante una cinemática.
    /// Comprueba escenas, jugador único, HUD, infectados sobre el NavMesh, textos sin ids ni spoilers, que el juego
    /// se congela y se restaura con cada cinemática y que no hay excepciones en la consola. No modifica assets:
    /// los cambios (atajos de demo, clip temporal floor_transition_to_01) son en memoria o se deshacen al terminar.
    /// Resultado en Logs/NexoPlaythroughCheck.txt; en batchmode sale con 0 (todo OK) o 1.
    /// Las cinemáticas se prueban si hay clips en StreamingAssets/Cinematics (si no, se comprueba que no bloquean).
    /// </summary>
    [InitializeOnLoad]
    public static class NexoPlaythroughCheck
    {
        private const string ActiveKey = "Nexo.PlaythroughCheck.Active";
        private const string ExitKey = "Nexo.PlaythroughCheck.ExitCode";
        private const string PersistentScene = "Assets/_Project/Scenes/Persistent/Systems_Persistent.unity";
        private const double GlobalTimeoutSeconds = 900d;
        private const string TempSpecificClip = "floor_transition_to_01";

        private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> ErrorSamples = new List<string>();
        private static int _passed;
        private static int _failed;
        private static int _errors;
        private static double _deadline;
        private static bool _started;
        private static Keyboard _keyboard;
        private static bool _keySimulationWorks = true;
        private static bool _tempClipCreated;
        private static string _tempCaptionsPath;
        private static string _tempCorruptPath;
        private static string _createdCinematicsFolder;
        private static int _errorsBeforeCorrupt;
        private static bool _twistStarted;

        // Eventos observados
        private static readonly List<string> CinematicLog = new List<string>();
        private static int _twistCount;
        private static bool _missingCallback;
        private static bool _adhocFinished;
        private static State _beforeCinematic;
        private static State _afterCinematic;
        private static float _lastCinematicElapsed;
        private static bool _hudHiddenBeforeTrigger;
        private static string _escLabel;
        private static bool _escResult;
        private static InputSettings.EditorInputBehaviorInPlayMode _savedEditorInput;
        private static InputSettings.BackgroundBehavior _savedBackground;
        private static string _lastNotificationKey;
        private static int _lastNotificationFrame = -1;
        private static int _duplicateNotifications;
        private static int _elenaResponses;
        private static ElenaResponse _lastElenaResponse;

        private struct State
        {
            public float TimeScale;
            public bool AudioPaused;
            public bool InputLocked;
            public CursorLockMode CursorLock;
            public bool CursorVisible;
            public bool HudHidden;

            public override string ToString() =>
                $"timeScale={TimeScale}, audioPause={AudioPaused}, inputLocked={InputLocked}, cursor={CursorLock}/{CursorVisible}, hudHidden={HudHidden}";

            public bool SameAs(State o) => Mathf.Approximately(TimeScale, o.TimeScale) && AudioPaused == o.AudioPaused &&
                                           InputLocked == o.InputLocked && CursorLock == o.CursorLock &&
                                           CursorVisible == o.CursorVisible && HudHidden == o.HudHidden;
        }

        static NexoPlaythroughCheck()
        {
            if (SessionState.GetInt(ExitKey, -1) >= 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int code = SessionState.GetInt(ExitKey, 1);
                SessionState.EraseInt(ExitKey);
                if (Application.isBatchMode) EditorApplication.delayCall += ExitWithStoredCode(code);
                return;
            }
            if (SessionState.GetBool(ActiveKey, false)) Hook();
        }

        [MenuItem("Nexo/Checks/Playthrough (Play mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NexoPlaythroughCheck] Sal de Play antes de lanzar la comprobación.");
                return;
            }
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(PersistentScene, OpenSceneMode.Single);
            Hook();
            EditorApplication.EnterPlaymode();
        }

        private static EditorApplication.CallbackFunction ExitWithStoredCode(int code)
        {
            return () => EditorApplication.Exit(code);
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

        private static void Unhook()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;
            GameEvents.OnCinematicFinished -= HandleCinematicFinished;
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwist;
            GameEvents.OnNotification -= HandleNotification;
            GameEvents.OnSubtitle -= HandleSubtitle;
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
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

        private static double Now => EditorApplication.timeSinceStartup;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;

            if (!_started)
            {
                _started = true;
                _deadline = Now + GlobalTimeoutSeconds;
                Stack.Clear();
                Stack.Push(Scenario());
                GameEvents.OnCinematicStarted += HandleCinematicStarted;
                GameEvents.OnCinematicFinished += HandleCinematicFinished;
                GameEvents.OnPlotTwistTriggered += HandlePlotTwist;
                GameEvents.OnNotification += HandleNotification;
                GameEvents.OnSubtitle += HandleSubtitle;
                GameEvents.OnElenaResponse += HandleElenaResponse;
                GameEvents.OnPlayerCaught += HandlePlayerCaught;
                Application.runInBackground = true;
            }

            if (Now > _deadline)
            {
                Fail("tiempo global agotado");
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
                    return; // espera al siguiente fotograma
                }
                End();
            }
            catch (Exception e)
            {
                Fail("excepción del escenario: " + e);
                End();
            }
        }

        // ---------- Escenario ----------

        private static IEnumerator Scenario()
        {
            Log("== Arranque ==");
            yield return WaitFor(() => Menu() != null && Loader() != null, 30, "Systems_Persistent cargada (menú y SceneLoader)");
            yield return WaitFor(() => Menu() != null && Menu().Current == ScreenId.MainMenu, 60, "secuencia de arranque → menú principal");
            yield return WaitFor(() => FloorLoaded("Floor12_Offices") && !Loader().IsLoading, 60, "Piso 12 cargado detrás del menú");
            Check(Count<CinematicService>() == 1, "un único CinematicService (DontDestroyOnLoad)");
            Check(!NarrativeEvents.TwistHasPlayed, "TwistHasPlayed limpio al arrancar");
            _savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            _savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>("NexoCheckKeyboard");

            bool hasIntro = CinematicService.HasClip(CinematicIds.Intro);
            bool hasTransition = CinematicService.HasClip(CinematicIds.FloorTransition);
            bool hasEnding = CinematicService.HasClip(CinematicIds.Ending);
            Log($"clips: intro={hasIntro}, floor_transition={hasTransition}, ending={hasEnding}");

            // Id ausente / inválido: onFinished inmediato, nunca bloquea
            _missingCallback = false;
            CinematicService.Play("nexo_check_missing_clip", HandleMissingFinished);
            Check(_missingCallback && !CinematicService.IsPlaying, "Play de un id sin clip llama a onFinished al instante");
            _missingCallback = false;
            CinematicService.Play("../hack", HandleMissingFinished);
            Check(_missingCallback && !CinematicService.IsPlaying, "Play de un id inválido llama a onFinished al instante");

            // ---- JUGAR → intro ----
            Log("== JUGAR (intro) ==");
            CinematicLog.Clear();
            _hudHiddenBeforeTrigger = false; // MenuGameFlowBridge muestra el HUD (ShowScreen(None)) antes de la intro
            MenuEvents.RaisePlayRequested();
            if (hasIntro)
            {
                Check(CinematicService.IsPlaying && CinematicService.CurrentId == CinematicIds.Intro, "JUGAR inicia la cinemática intro");
                yield return CinematicFullCheck(CinematicIds.Intro, new Color32(0x10, 0x30, 0x70, 255), true);
            }
            else
            {
                Check(!CinematicService.IsPlaying, "sin intro.mp4, JUGAR no espera");
            }

            yield return WaitFor(() => FloorLoaded("Floor12_Offices") && !Loader().IsLoading && Menu().Current == ScreenId.None, 30, "Piso 12 cargado tras JUGAR");
            yield return WaitSeconds(2.5);
            CheckFloor("Floor12_Offices", 12);
            CheckNewGame("JUGAR (1.ª partida)");
            yield return ElenaQuickQuestions(12);

            // ---- Clip corrupto: error o tiempo de preparación agotado, nunca bloquea ----
            yield return CorruptClipCheck();

            // ---- Forcejeo activo cuando empieza una cinemática ----
            if (hasIntro) yield return StruggleDuringCinematic();

            // ---- 12 → 6 ----
            Log("== Piso 12 → Piso 6 (LoadNext) ==");
            CinematicLog.Clear();
            MarkTrigger();
            Check(FloorProgression.Active != null && FloorProgression.Active.LoadNext(), "FloorProgression.LoadNext acepta 12 → 6");
            if (hasTransition)
            {
                Check(CinematicService.IsPlaying && CinematicService.CurrentId == CinematicIds.FloorTransition, "LoadNext reproduce floor_transition (no hay floor_transition_to_06)");
                Check(FloorLoaded("Floor12_Offices") && !FloorLoaded("Floor06_Labs"), "el piso se carga DESPUÉS del clip (12 sigue cargado)");
                yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "floor_transition visible");
                yield return CheckFrozen("floor_transition (clip sin pista de audio)");
                yield return WaitFor(() => !CinematicService.IsPlaying, 10, "floor_transition termina sola (sin saltar)");
                Check(_lastCinematicElapsed > 2.5f, $"floor_transition llegó al final ({_lastCinematicElapsed:0.00} s de 3 s)");
                CheckRestored("floor_transition");
            }
            yield return WaitFor(() => FloorLoaded("Floor06_Labs") && !FloorLoaded("Floor12_Offices") && !FloorProgression.Active.IsBusy, 40, "Piso 6 cargado y Piso 12 descargado");
            yield return WaitSeconds(2.5);
            CheckFloor("Floor06_Labs", 6);
            yield return Floor06Checks();
            yield return ElenaQuickQuestions(6);

            // ---- 6 → 1 con clip específico temporal y jugador escondido ----
            Log("== Piso 6 → Piso 1 (clip específico + escondido) ==");
            if (hasIntro) CreateTempSpecificClip();
            HidingSpot hiding = FindInScene<HidingSpot>("Floor06_Labs");
            if (hiding != null)
            {
                hiding.Interact();
                Check(PlayerHiding.IsHidden, "HidingSpot.Interact esconde al jugador");
            }
            MarkTrigger();
            Check(FloorProgression.Active.LoadNext(), "FloorProgression.LoadNext acepta 6 → 1");
            if (_tempClipCreated)
            {
                Check(CinematicService.CurrentId == TempSpecificClip, "si existe floor_transition_to_01.mp4 se usa en lugar del genérico");
                yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "floor_transition_to_01 visible");
                if (hiding != null)
                {
                    yield return PressKey(Key.E);
                    yield return null;
                    Check(PlayerHiding.IsHidden, "[E] durante el clip no saca al jugador del escondite");
                }
                yield return SkipAfterDelay();
            }
            yield return WaitFor(() => FloorLoaded("Floor01_Lobby") && !FloorLoaded("Floor06_Labs") && !FloorProgression.Active.IsBusy, 40, "Piso 1 cargado y Piso 6 descargado");
            DeleteTempSpecificClip();
            Check(!PlayerHiding.IsHidden, "al descargar el piso con el jugador escondido se libera el escondite");
            yield return WaitSeconds(2.5);
            CheckFloor("Floor01_Lobby", 1);
            Check(!Movement().IsInputLocked, "control devuelto tras bajar escondido");

            yield return ElenaQuickQuestions(1);

            // ---- Giro final ----
            yield return TwistAndEnding(hasEnding);

            // ---- Volver al menú y JUGAR de nuevo ----
            Log("== Créditos → menú → JUGAR (sin fugas de estado) ==");
            MenuEvents.RaiseReturnToMainMenuRequested();
            yield return WaitFor(() => Menu() != null && Menu().Current == ScreenId.MainMenu && Loader() != null && !Loader().IsLoading && FloorLoaded("Floor12_Offices"), 60, "Systems_Persistent recargada: menú principal y Piso 12 detrás");
            Check(!NarrativeEvents.TwistHasPlayed, "TwistHasPlayed limpio tras volver al menú");
            Check(Count<PlayerMovement>() == 1 && Count<HUDController>() == 1 && Count<MenuFlowController>() == 1 && Count<CinematicService>() == 1,
                $"sin duplicados: jugador {Count<PlayerMovement>()}, HUD {Count<HUDController>()}, menú {Count<MenuFlowController>()}, cinemáticas {Count<CinematicService>()}");
            Check(Time.timeScale == 1f && !AudioListener.pause, "tiempo y audio normales en el menú");

            CinematicLog.Clear();
            string captionsPath = CinematicPaths.CaptionsPath(Application.streamingAssetsPath, CinematicIds.Intro);
            if (hasIntro && !File.Exists(captionsPath))
            {
                File.WriteAllText(captionsPath, "[{\"start\":0.0,\"end\":2.5,\"speaker\":\"ELENA\",\"text\":\"Prueba de subtítulo\"}]");
                _tempCaptionsPath = captionsPath;
            }
            _hudHiddenBeforeTrigger = false;
            MenuEvents.RaisePlayRequested();
            if (hasIntro)
            {
                Check(CinematicService.CurrentId == CinematicIds.Intro, "la intro vuelve a sonar en una partida nueva");
                yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "intro visible (2.ª partida)");
                if (_tempCaptionsPath != null)
                {
                    yield return WaitFor(() => OverlayQ<Label>("cinematic-caption") != null && OverlayQ<Label>("cinematic-caption").text == "ELENA: Prueba de subtítulo"
                                               && OverlayQ<Label>("cinematic-caption").style.display == DisplayStyle.Flex, 2, "intro.captions.json se muestra sobre el vídeo vía NarrativeSubtitles");
                }
                yield return SkipAfterDelay();
                DeleteTempCaptions();
                CheckRestored("intro (2.ª partida)");
            }
            yield return WaitFor(() => FloorLoaded("Floor12_Offices") && !Loader().IsLoading && Menu().Current == ScreenId.None, 30, "Piso 12 cargado (2.ª partida)");
            yield return WaitSeconds(2f);
            CheckFloor("Floor12_Offices", 12);
            CheckNewGame("JUGAR (2.ª partida)");
            Check(FloorProgression.Active != null && FloorProgression.Active.CurrentIndex == 0, "índice de piso reiniciado a 0");

            // ---- Atajos de demo ----
            yield return DemoShortcutsCheck(hasTransition);

            // ---- Final ----
            Check(_duplicateNotifications == 0, $"sin notificaciones duplicadas en el mismo fotograma ({_duplicateNotifications})");
            Check(_errors == 0, $"consola sin errores ni excepciones ({_errors})" + (ErrorSamples.Count > 0 ? ": " + string.Join(" || ", ErrorSamples) : string.Empty));
            if (!_keySimulationWorks) Log("NOTA: la simulación de teclado no llegó al juego en este modo; los saltos se hicieron con CinematicService.RequestSkip y los atajos por reflexión.");
        }

        private static IEnumerator CinematicFullCheck(string id, Color32 expectedColour, bool testEarlySkip)
        {
            yield return WaitFor(() => CinematicService.IsShowingVideo, 6, $"{id} preparada y visible");
            if (testEarlySkip)
            {
                if (CinematicService.Elapsed < 0.45f) Check(!CinematicService.RequestSkip(), $"salto rechazado antes de 0,5 s ({CinematicService.Elapsed:0.00} s)");
                else Log("NOTA: el primer fotograma llegó tarde; no se pudo probar el salto antes de 0,5 s");
            }
            yield return CheckFrozen(id);
            yield return WaitFor(() => CinematicService.Elapsed >= 1.1f, 3, "pasa 1 s de vídeo");
            Label hint = OverlayQ<Label>("cinematic-skip-hint");
            Check(hint != null && hint.style.opacity.value > 0.99f && hint.text == "Esc / Espacio: saltar", "aviso «Esc / Espacio: saltar» visible tras 1 s");
            CheckVideoPixel(id, expectedColour);
            yield return SkipAfterDelay();
            Check(_lastCinematicElapsed < 2.5f, $"{id} saltada antes del final ({_lastCinematicElapsed:0.00} s)");
            CheckRestored(id);
        }

        private static IEnumerator CheckFrozen(string id)
        {
            var brains = Object.FindObjectsByType<SmartInfectedBrain>(FindObjectsInactive.Exclude);
            var positions = new Vector3[brains.Length];
            for (int i = 0; i < brains.Length; i++) positions[i] = brains[i].transform.position;

            Check(Time.timeScale == 0f, $"{id}: juego congelado (timeScale 0)");
            Check(AudioListener.pause, $"{id}: audio del juego en pausa");
            Check(Movement() == null || Movement().IsInputLocked, $"{id}: jugador bloqueado");
            Check(HudHidden(), $"{id}: HUD oculto");
            Check(!PauseOpen(), $"{id}: la pausa no se abre");

            // Esc antes de 0,5 s: ni salta ni abre la pausa
            if (CinematicService.Elapsed < 0.3f)
            {
                yield return PressKey(Key.Escape);
                yield return null;
                _escResult = CinematicService.IsPlaying && !PauseOpen();
                _escLabel = $"{id}: Esc antes de 0,5 s ni salta el clip ni abre la pausa";
            }
            yield return WaitSeconds(0.7);
            float moved = 0f;
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] != null) moved = Mathf.Max(moved, Vector3.Distance(positions[i], brains[i].transform.position));
            }
            Check(moved < 0.01f, $"{id}: infectados quietos durante el clip ({brains.Length} infectados, desplazamiento máx. {moved:0.000} m)");
        }

        private static IEnumerator SkipAfterDelay()
        {
            yield return WaitFor(() => CinematicService.Elapsed >= 0.6f || !CinematicService.IsPlaying, 4, "0,6 s de vídeo antes de saltar");
            if (!CinematicService.IsPlaying) yield break;

            yield return PressKey(Key.Space);
            double until = Now + 0.6;
            while (CinematicService.IsPlaying && Now < until) yield return null;
            if (CinematicService.IsPlaying)
            {
                _keySimulationWorks = false;
                Check(CinematicService.RequestSkip(), "RequestSkip aceptado tras 0,5 s");
            }
            else
            {
                Pass("Espacio salta la cinemática tras 0,5 s");
            }
            if (_escLabel != null)
            {
                if (_keySimulationWorks) Check(_escResult, _escLabel);
                else Log("NOTA (no verificable sin teclado simulado): " + _escLabel);
                _escLabel = null;
            }
            yield return WaitFor(() => !CinematicService.IsPlaying, 2, "la cinemática termina tras el salto");
            yield return null;
            Check(!PauseOpen(), "la tecla de salto no abre la pausa");
        }

        private static IEnumerator CorruptClipCheck()
        {
            Log("== Clip corrupto ==");
            _errorsBeforeCorrupt = _errors;
            int samplesBefore = ErrorSamples.Count;
            string path = CinematicPaths.VideoPath(Application.streamingAssetsPath, "nexo_check_corrupt");
            var garbage = new byte[64 * 1024];
            new System.Random(7).NextBytes(garbage);
            // Sin clips reales la carpeta StreamingAssets/Cinematics puede no existir (está en .gitignore)
            string folder = Path.GetDirectoryName(path);
            _createdCinematicsFolder = !Directory.Exists(folder) ? folder : null;
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, garbage);
            _tempCorruptPath = path;

            _adhocFinished = false;
            MarkTrigger();
            double start = Now;
            CinematicService.Play("nexo_check_corrupt", HandleAdhocFinished);
            yield return WaitFor(() => !CinematicService.IsPlaying, 8, "un clip corrupto termina solo (error o 5 s de preparación)");
            Check(_adhocFinished, $"onFinished llamado con el clip corrupto ({Now - start:0.0} s)");
            CheckRestored("clip corrupto");
            Check(!Movement().IsInputLocked && Time.timeScale == 1f, "tras el clip corrupto el juego sigue normal");
            DeleteTempCorrupt();
            // El VideoPlayer avisa del error por consola: es lo esperado aquí, no cuenta como fallo
            if (_errors > _errorsBeforeCorrupt) Log($"errores de consola esperados por el clip corrupto: {_errors - _errorsBeforeCorrupt} (no cuentan)");
            _errors = _errorsBeforeCorrupt;
            if (ErrorSamples.Count > samplesBefore) ErrorSamples.RemoveRange(samplesBefore, ErrorSamples.Count - samplesBefore);
        }

        private static void DeleteTempCorrupt()
        {
            if (_tempCorruptPath != null && File.Exists(_tempCorruptPath)) File.Delete(_tempCorruptPath);
            _tempCorruptPath = null;

            // Si la carpeta la creó esta comprobación y quedó vacía, se elimina (con StreamingAssets si también quedó vacía)
            if (_createdCinematicsFolder != null && Directory.Exists(_createdCinematicsFolder) && Directory.GetFileSystemEntries(_createdCinematicsFolder).Length == 0)
            {
                Directory.Delete(_createdCinematicsFolder);
                if (File.Exists(_createdCinematicsFolder + ".meta")) File.Delete(_createdCinematicsFolder + ".meta");
                string parent = Path.GetDirectoryName(_createdCinematicsFolder);
                if (Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0)
                {
                    Directory.Delete(parent);
                    if (File.Exists(parent + ".meta")) File.Delete(parent + ".meta");
                }
            }
            _createdCinematicsFolder = null;
        }

        private static IEnumerator StruggleDuringCinematic()
        {
            Log("== Forcejeo + cinemática ==");
            PlayerMovement movement = Movement();
            StruggleQTE struggle = movement != null ? movement.GetComponent<StruggleQTE>() : null;
            PlayerHealth health = movement != null ? movement.GetComponent<PlayerHealth>() : null;
            SmartInfectedBrain brain = Object.FindAnyObjectByType<SmartInfectedBrain>();
            if (struggle == null || brain == null || health == null)
            {
                Fail("no hay StruggleQTE/infectado para probar el forcejeo");
                yield break;
            }

            struggle.Begin(brain);
            Check(struggle.IsActive, "forcejeo iniciado");
            int hits = health.Hits;
            _adhocFinished = false;
            MarkTrigger();
            CinematicService.Play(CinematicIds.Intro, HandleAdhocFinished);
            Check(!struggle.IsActive, "la cinemática cancela el forcejeo al empezar");
            yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "intro visible (forcejeo)");
            yield return SkipAfterDelay();
            Check(_adhocFinished, "onFinished llamado tras el salto");
            Check(health.Hits == hits, "Espacio para saltar no cuenta como empujón fallido (sin golpe)");
            Check(!Movement().IsInputLocked, "control devuelto tras la cinemática");
            yield return WaitSeconds(1.0);
        }

        private static IEnumerator Floor06Checks()
        {
            Log("== Piso 6: contenido ==");
            Check(CountInScene<LoreNote>("Floor06_Labs") >= 6, $"notas N1-N6 (LoreNote): {CountInScene<LoreNote>("Floor06_Labs")}");
            Check(CountInScene<HidingSpot>("Floor06_Labs") == 3, $"3 escondites: {CountInScene<HidingSpot>("Floor06_Labs")}");
            Check(CountInScene<PowerBreaker>("Floor06_Labs") >= 1, "diferencial (PowerBreaker) presente");
            int crawlers = 0;
            foreach (SmartInfectedBrain brain in Object.FindObjectsByType<SmartInfectedBrain>(FindObjectsInactive.Include))
            {
                if (brain.gameObject.scene.name == "Floor06_Labs" && brain.Behavior == InfectedBehavior.Crawler) crawlers++;
            }
            Check(crawlers == 2, $"2 reptantes: {crawlers}");

            DoorController stairs = FindDoor("Floor06_Labs", "F06_Door_Stairs");
            Check(stairs != null && stairs.IsLocked, "F06_Door_Stairs bloqueada al llegar (la terminal de SOMBRA no la abre)");

            // Terminal de seguridad + red de cámaras abiertas cuando empieza una cinemática
            SecurityTerminal terminal = FindInScene<SecurityTerminal>("Floor06_Labs");
            TerminalController terminalUi = Object.FindAnyObjectByType<TerminalController>();
            CameraNetworkController network = Object.FindAnyObjectByType<CameraNetworkController>();
            if (terminal != null && terminalUi != null && network != null)
            {
                terminal.Interact();
                Check(terminalUi.IsOpen && Movement().IsInputLocked, "terminal de seguridad abierta (jugador bloqueado)");
                network.OpenNetwork();
                Check(network.IsNetworkOpen, "red de cámaras abierta desde la terminal");
                yield return WaitSeconds(0.5);
                network.CloseNetwork();
                Check(!network.IsNetworkOpen && terminalUi.IsOpen, "cerrar la red deja la terminal abierta");
                network.OpenNetwork();
                if (CinematicService.HasClip(CinematicIds.Intro))
                {
                    MarkTrigger();
                    CinematicService.Play(CinematicIds.Intro, HandleAdhocFinished);
                    Check(!terminalUi.IsOpen && !network.IsNetworkOpen, "la cinemática cierra terminal y red de cámaras");
                    yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "intro visible (terminal)");
                    yield return SkipAfterDelay();
                    Check(!Movement().IsInputLocked, "tras la cinemática el jugador recupera el control (no queda bloqueado por la terminal)");
                    CheckRestored("intro (terminal abierta)");
                }
                else
                {
                    network.CloseNetwork();
                    terminalUi.Close();
                    Check(!Movement().IsInputLocked, "cerrar la terminal devuelve el control");
                }
            }
            else
            {
                Fail("Piso 6 sin SecurityTerminal/TerminalController/CameraNetworkController");
            }

            // Pregunta rápida [2]: Elena (fallback) abre la puerta de las escaleras
            GameEvents.RaiseQuickQuestionSelected(1);
            yield return WaitFor(() => stairs != null && !stairs.IsLocked, 20, "[2] de Elena (fallback) desbloquea F06_Door_Stairs");
        }

        /// <summary>
        /// Preguntas rápidas [1]-[3] en el piso actual. Sin ElenaSecrets (o con _forceFallback) responde el fallback:
        /// cada diálogo debe existir en el fallback_floorNN.json de ESE piso (coherencia de piso) y pasar el filtro de
        /// textos (sin ids técnicos ni spoilers del giro).
        /// </summary>
        private static IEnumerator ElenaQuickQuestions(int floor)
        {
            Log($"== Elena: preguntas rápidas [1]-[3] en el Piso {floor} ==");
            ElenaConfig config = AssetDatabase.LoadAssetAtPath<ElenaConfig>("Assets/_Project/Data/ElenaConfig.asset");
            bool offline = config == null || config.ForceFallback || config.Secrets == null || !config.Secrets.HasApiKey;
            Log(offline
                ? "Elena en modo sin conexión (sin ElenaSecrets o _forceFallback): se prueba el fallback"
                : "AVISO: hay API key; las respuestas pueden venir de Gemini y no se comparan con el fallback");
            string json = File.ReadAllText($"Assets/_Project/Data/ElenaFallback/fallback_floor{floor:00}.json");
            for (int i = 0; i < 3; i++)
            {
                yield return WaitSeconds(0.3);
                int before = _elenaResponses;
                GameEvents.RaiseQuickQuestionSelected(i);
                yield return WaitFor(() => _elenaResponses > before, 15, $"Piso {floor}: Elena responde a [{i + 1}]");
                if (_elenaResponses == before || _lastElenaResponse == null) continue;
                string dialogue = _lastElenaResponse.dialogue ?? string.Empty;
                Log($"   [{i + 1}] «{dialogue}» ({_lastElenaResponse.ui_action})");
                Check(dialogue.Trim().Length > 0, $"Piso {floor} [{i + 1}]: diálogo no vacío");
                if (offline)
                {
                    string escaped = dialogue.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    Check(json.Contains(dialogue) || json.Contains(escaped), $"Piso {floor} [{i + 1}]: la respuesta sale del fallback de ese piso");
                }
            }
        }

        private static IEnumerator TwistAndEnding(bool hasEnding)
        {
            Log("== Piso 1: BioScanner y giro ==");
            GameObject ambience = FindRoot("Floor01_Lobby", "F01_Ambience");
            GameObject expansion = FindRoot("Floor01_Lobby", "F01_Expansion");
            Check(ambience != null && ambience.activeInHierarchy, "F01_Ambience activa antes del giro");
            Check(expansion != null && expansion.activeInHierarchy, "F01_Expansion activa antes del giro");

            BioScanner scanner = FindInScene<BioScanner>("Floor01_Lobby");
            Check(scanner != null, "BioScanner presente");
            if (scanner != null && FloorMetadata.Active != null && FloorMetadata.Active.PlayerSpawn != null)
            {
                var path = new NavMeshPath();
                bool found = NavMesh.SamplePosition(FloorMetadata.Active.PlayerSpawn.position, out NavMeshHit from, 2f, NavMesh.AllAreas) &&
                             NavMesh.SamplePosition(scanner.transform.position, out NavMeshHit to, 3f, NavMesh.AllAreas) &&
                             NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                Check(found && path.status == NavMeshPathStatus.PathComplete, $"camino completo del spawn al BioScanner (NavMesh, {PathLength(path):0.0} m)");
            }

            _twistCount = 0;
            _twistStarted = true;
            MarkTrigger();
            NarrativeEvents.RaiseBioScannerHandPlaced();
            Check(_twistCount == 1 && NarrativeEvents.TwistHasPlayed, "el giro arranca una vez");
            yield return WaitSeconds(0.5);
            Check(ambience != null && AllChildrenInactive(ambience.transform), "PlotTwistSafeZone apaga todo lo que cuelga de F01_Ambience al empezar el giro");
            Check(expansion != null && ActiveThreats(expansion.transform) == 0, $"F01_Expansion (geometría de las alas) sin infectados, sustos ni audio activos tras el giro ({ActiveThreats(expansion.transform)})");
            Check(ActiveBrains("Floor01_Lobby") == 0, $"ningún infectado activo en el Piso 1 durante el giro ({ActiveBrains("Floor01_Lobby")})");
            NarrativeEvents.RaiseBioScannerHandPlaced();
            Check(_twistCount == 1, "un segundo BioScannerHandPlaced no repite el giro");

            if (hasEnding)
            {
                yield return WaitFor(() => CinematicService.CurrentId == CinematicIds.Ending, 30, "la cinemática ending empieza tras la última línea del giro");
                Check(Menu().Current != ScreenId.Credits, "los créditos NO aparecen antes del final");
                yield return CinematicFullCheck(CinematicIds.Ending, new Color32(0x70, 0x10, 0x10, 255), false);
                yield return WaitFor(() => Menu().Current == ScreenId.Credits, 3, "créditos tras saltar el final");
            }
            else
            {
                yield return WaitFor(() => Menu().Current == ScreenId.Credits, 30, "créditos tras el giro (sin ending.mp4)");
            }

            Label cinematics = Menu().GetComponent<UIDocument>().rootVisualElement.Q<Label>("cr-cinematics-value");
            Check(cinematics != null && cinematics.text == "Generadas con IA (Gemini y Veo, Google)", "créditos con el bloque CINEMÁTICAS");
            _twistStarted = false;
        }

        private static IEnumerator DemoShortcutsCheck(bool hasTransition)
        {
            Log("== Atajos de demo (EnableDemoShortcuts = true solo en memoria) ==");
            PlayerMovement movement = Movement();
            DemoShortcuts demo = movement != null ? movement.GetComponent<DemoShortcuts>() : null;
            PlayerWeapon weapon = movement != null ? movement.GetComponent<PlayerWeapon>() : null;
            PlayerHealth health = movement != null ? movement.GetComponent<PlayerHealth>() : null;
            if (demo == null || weapon == null || health == null)
            {
                Fail("PF_Player sin DemoShortcuts/PlayerWeapon/PlayerHealth");
                yield break;
            }
            Check(!demo.EnableDemoShortcuts, "EnableDemoShortcuts = false en PF_Player");

            demo.EnableDemoShortcuts = true;
            demo.enabled = false;
            demo.enabled = true;

            yield return Shortcut(demo, Key.F7, "HandleAmmo");
            Check(weapon.Magazine == 7 && weapon.Reserve == 35, $"[F7] munición llena ({weapon.Magazine}/{weapon.Reserve})");

            health.RegisterHit();
            Check(!health.IsUnharmed, "golpe de prueba registrado");
            yield return Shortcut(demo, Key.F6, "HandleHeal");
            Check(health.IsUnharmed, "[F6] vida completa");

            CinematicLog.Clear();
            MarkTrigger();
            yield return Shortcut(demo, Key.F5, "HandleNextFloor");
            if (hasTransition)
            {
                Check(CinematicService.CurrentId == CinematicIds.FloorTransition, "[F5] pasa por LoadNext y reproduce floor_transition");
                yield return WaitFor(() => CinematicService.IsShowingVideo, 6, "floor_transition visible ([F5])");
                yield return SkipAfterDelay();
            }
            yield return WaitFor(() => FloorLoaded("Floor06_Labs") && !FloorProgression.Active.IsBusy, 40, "[F5] Piso 6 cargado");

            yield return Shortcut(demo, Key.F8, "HandleScanner");
            double end = Now + 60;
            while (Now < end && !(FloorLoaded("Floor01_Lobby") && !FloorProgression.Active.IsBusy && !CinematicService.IsPlaying))
            {
                if (CinematicService.IsShowingVideo && CinematicService.Elapsed >= 0.6f) CinematicService.RequestSkip();
                yield return null;
            }
            yield return WaitSeconds(1.0);
            BioScanner scanner = FindInScene<BioScanner>("Floor01_Lobby");
            float distance = scanner != null ? Vector3.Distance(Flat(scanner.transform.position), Flat(Movement().transform.position)) : -1f;
            Check(scanner != null && distance < 3f, $"[F8] lleva al jugador frente al BioScanner (distancia {distance:0.0} m)");

            demo.EnableDemoShortcuts = false;
            demo.enabled = false;
            demo.enabled = true;
        }

        private static IEnumerator Shortcut(DemoShortcuts demo, Key key, string handler)
        {
            if (_keySimulationWorks)
            {
                yield return PressKey(key);
                yield return null;
                yield return null;
            }
            if (!_keySimulationWorks)
            {
                MethodInfo method = typeof(DemoShortcuts).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic);
                method?.Invoke(demo, new object[] { default(InputAction.CallbackContext) });
                yield return null;
            }
        }

        private static IEnumerator PressKey(Key key)
        {
            if (_keyboard == null || !_keySimulationWorks) yield break;

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            bool seen = _keyboard[key].wasPressedThisFrame || _keyboard[key].isPressed;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            if (!seen) _keySimulationWorks = false;
        }

        // ---------- Comprobaciones ----------

        private static void CheckFloor(string sceneName, int floorNumber)
        {
            Check(SceneManager.GetSceneByName(sceneName).isLoaded, $"[{sceneName}] escena cargada");
            Check(SceneManager.GetActiveScene().name == sceneName, $"[{sceneName}] escena activa (iluminación y NavMesh)");
            int loadedFloors = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name.StartsWith("Floor")) loadedFloors++;
            }
            Check(loadedFloors == 1, $"[{sceneName}] solo un piso cargado ({loadedFloors})");
            Check(FloorMetadata.Active != null && FloorMetadata.Active.FloorNumber == floorNumber, $"[{sceneName}] FloorMetadata publica el piso {floorNumber}");
            Check(Count<PlayerMovement>() == 1, $"[{sceneName}] un único jugador ({Count<PlayerMovement>()})");
            Check(Count<HUDController>() == 1, $"[{sceneName}] un único HUD ({Count<HUDController>()})");
            Check(!HudHidden(), $"[{sceneName}] HUD visible");
            Check(Time.timeScale == 1f && !AudioListener.pause, $"[{sceneName}] tiempo y audio normales");
            Check(!Movement().IsInputLocked, $"[{sceneName}] jugador con control");

            if (FloorMetadata.Active != null && FloorMetadata.Active.PlayerSpawn != null)
            {
                float d = Vector3.Distance(Flat(Movement().transform.position), Flat(FloorMetadata.Active.PlayerSpawn.position));
                Check(d < 3f, $"[{sceneName}] jugador en el punto de aparición ({d:0.0} m)");
            }

            int total = 0;
            int onMesh = 0;
            var offMesh = new List<string>();
            foreach (SmartInfectedBrain brain in Object.FindObjectsByType<SmartInfectedBrain>(FindObjectsInactive.Exclude))
            {
                if (brain.gameObject.scene.name != sceneName || brain.IsDead) continue;
                total++;
                NavMeshAgent agent = brain.Agent;
                if (agent != null && agent.enabled && agent.isOnNavMesh) onMesh++;
                else offMesh.Add(brain.name);
            }
            Check(total > 0 && onMesh == total, $"[{sceneName}] infectados sobre el NavMesh: {onMesh}/{total}" + (offMesh.Count > 0 ? " fuera: " + string.Join(", ", offMesh) : string.Empty));

            PlayerWeapon weapon = Movement().GetComponent<PlayerWeapon>();
            Transform viewmodel = weapon != null ? FindChild(weapon.transform, "Viewmodel_M1911") : null;
            Log($"[{sceneName}] viewmodel: {(viewmodel != null ? (viewmodel.gameObject.activeInHierarchy ? "activo" : "INACTIVO") : "no encontrado por nombre")}");
        }

        private static void CheckNewGame(string label)
        {
            PlayerMovement movement = Movement();
            PlayerWeapon weapon = movement.GetComponent<PlayerWeapon>();
            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            Check(weapon != null && weapon.Magazine == 2 && weapon.Reserve == 0, $"{label}: munición inicial 2/0 ({weapon?.Magazine}/{weapon?.Reserve})");
            Check(health != null && health.IsUnharmed, $"{label}: vida completa");
            Check(!PlayerHiding.IsHidden, $"{label}: no escondido");
        }

        private static void CheckRestored(string id)
        {
            // El HUD se lee ahora (un fotograma después): tras recargar Systems_Persistent el HUD nuevo se suscribe
            // después que esta comprobación y restaura su display después de que se tome la instantánea
            // (si hay un menú abierto, p. ej. los créditos tras el final, el HUD oculto es correcto: vale la instantánea)
            if (Menu() != null && Menu().Current == ScreenId.None) _afterCinematic.HudHidden = HudHidden();
            Check(_beforeCinematic.SameAs(_afterCinematic), $"{id}: estado restaurado exactamente (antes: {_beforeCinematic} | después: {_afterCinematic})");
            Check(CinematicService.CurrentTexture == null && !CinematicService.IsPlaying, $"{id}: RenderTexture liberada y servicio libre");
        }

        private static void CheckVideoPixel(string id, Color32 expected)
        {
            RenderTexture rt = CinematicService.CurrentTexture;
            if (rt == null)
            {
                Fail($"{id}: no hay RenderTexture del vídeo");
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            // Esquina inferior izquierda (lejos del texto centrado del marcador)
            tex.ReadPixels(new Rect(8, 8, 1, 1), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color32 c = tex.GetPixel(0, 0);
            Object.DestroyImmediate(tex);

            int diff = Mathf.Abs(c.r - expected.r) + Mathf.Abs(c.g - expected.g) + Mathf.Abs(c.b - expected.b);
            Check(diff < 60, $"{id}: la RenderTexture muestra el clip ({rt.width}x{rt.height}, píxel {c.r},{c.g},{c.b} ≈ {expected.r},{expected.g},{expected.b})");
        }

        // ---------- Eventos ----------

        private static void HandleCinematicStarted(string id)
        {
            CinematicLog.Add("start:" + id);
            _beforeCinematic = Capture();
            // El HUD ya se ocultó en su propio handler (suscrito antes): vale el estado previo al disparador
            _beforeCinematic.HudHidden = _hudHiddenBeforeTrigger;
        }

        /// <summary>Guarda el estado del HUD justo antes de algo que puede lanzar una cinemática.</summary>
        private static void MarkTrigger() => _hudHiddenBeforeTrigger = HudHidden();

        private static void HandleCinematicFinished(string id)
        {
            CinematicLog.Add("end:" + id);
            _afterCinematic = Capture();
            _lastCinematicElapsed = _lastElapsedProbe;
        }

        private static float _lastElapsedProbe;

        private static void HandlePlotTwist() => _twistCount++;

        private static void HandlePlayerCaught() => Log("AVISO: el jugador fue capturado durante la comprobación (el piso se reinicia)");

        private static void HandleMissingFinished() => _missingCallback = true;

        private static void HandleAdhocFinished() => _adhocFinished = true;

        private static void HandleNotification(HudNotification notification)
        {
            string key = notification.Sender + "|" + notification.Body;
            if (key == _lastNotificationKey && Time.frameCount == _lastNotificationFrame)
            {
                _duplicateNotifications++;
                Log("NOTIFICACIÓN DUPLICADA: " + key);
            }
            _lastNotificationKey = key;
            _lastNotificationFrame = Time.frameCount;
            CheckPlayerText("notificación", notification.Body);
        }

        private static void HandleSubtitle(string text) => CheckPlayerText("subtítulo", text);

        private static void HandleElenaResponse(ElenaResponse response)
        {
            if (response == null) return;
            _elenaResponses++;
            _lastElenaResponse = response;
            CheckPlayerText("Elena", response.dialogue);
        }

        private static void CheckPlayerText(string kind, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string upper = text.ToUpperInvariant();
            if (upper.Contains("_DOOR_") || upper.Contains("F12_") || upper.Contains("F06_") || upper.Contains("F01_"))
            {
                Fail($"{kind} con id técnico: «{text}»");
            }
            if (!_twistStarted && !NarrativeEvents.TwistHasPlayed && (upper.Contains("98.4") || (upper.Contains("INFECTAD") && !upper.Contains("NO INFECTAD"))))
            {
                Fail($"{kind} revela la infección antes del giro: «{text}»");
            }
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (CinematicService.IsPlaying) _lastElapsedProbe = CinematicService.Elapsed;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.Contains("manager != NULL")) return; // bug conocido del Hierarchy de Unity 6.6
            if (condition.StartsWith("[NexoPlaythroughCheck]")) return;
            _errors++;
            if (ErrorSamples.Count < 12) ErrorSamples.Add(type + ": " + condition);
        }

        private static State Capture()
        {
            if (CinematicService.IsPlaying) _lastElapsedProbe = CinematicService.Elapsed;
            PlayerMovement movement = Movement();
            return new State
            {
                TimeScale = Time.timeScale,
                AudioPaused = AudioListener.pause,
                InputLocked = movement != null && movement.IsInputLocked,
                CursorLock = UnityEngine.Cursor.lockState,
                CursorVisible = UnityEngine.Cursor.visible,
                HudHidden = HudHidden()
            };
        }

        // ---------- Utilidades ----------

        private static IEnumerator WaitFor(Func<bool> condition, double seconds, string label)
        {
            double start = Now;
            while (true)
            {
                bool ok;
                try { ok = condition(); }
                catch (Exception) { ok = false; }
                if (ok) break;
                if (Now - start > seconds)
                {
                    Fail($"tiempo agotado ({seconds:0} s): {label}");
                    yield break;
                }
                if (CinematicService.IsPlaying) _lastElapsedProbe = CinematicService.Elapsed;
                yield return null;
            }
            Pass($"{label} ({Now - start:0.0} s)");
        }

        private static IEnumerator WaitSeconds(double seconds)
        {
            double end = Now + seconds;
            while (Now < end)
            {
                if (CinematicService.IsPlaying) _lastElapsedProbe = CinematicService.Elapsed;
                yield return null;
            }
        }

        private static MenuFlowController Menu() => Object.FindAnyObjectByType<MenuFlowController>();
        private static SceneLoader Loader() => Object.FindAnyObjectByType<SceneLoader>();
        private static PlayerMovement Movement() => Object.FindAnyObjectByType<PlayerMovement>();
        private static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include).Length;
        private static bool FloorLoaded(string scene) => SceneManager.GetSceneByName(scene).isLoaded;
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static bool PauseOpen() => Menu() != null && Menu().Current == ScreenId.Pause;

        private static bool HudHidden()
        {
            HUDController hud = Object.FindAnyObjectByType<HUDController>();
            VisualElement root = hud != null ? hud.GetComponent<UIDocument>().rootVisualElement : null;
            VisualElement hudRoot = root?.Q<VisualElement>("hud-root");
            if (root == null || hudRoot == null) return true;
            return root.style.display == DisplayStyle.None || hudRoot.style.display == DisplayStyle.None;
        }

        private static T OverlayQ<T>(string name) where T : VisualElement
        {
            foreach (UIDocument doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
            {
                if (doc.name != "CinematicOverlay" || doc.rootVisualElement == null) continue;
                return doc.rootVisualElement.Q<T>(name);
            }
            return null;
        }

        private static T FindInScene<T>(string sceneName) where T : Component
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static int CountInScene<T>(string sceneName) where T : Component
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return 0;
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<T>(true).Length;
            return count;
        }

        private static DoorController FindDoor(string sceneName, string doorId)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (DoorController door in root.GetComponentsInChildren<DoorController>(true))
                {
                    if (new SerializedObject(door).FindProperty("_doorId").stringValue == doorId) return door;
                }
            }
            return null;
        }

        private static GameObject FindRoot(string sceneName, string name)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform child = FindChild(root.transform, name);
                if (child != null) return child.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static bool AllChildrenInactive(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static int ActiveThreats(Transform root)
        {
            int count = 0;
            foreach (SmartInfectedBrain brain in root.GetComponentsInChildren<SmartInfectedBrain>(false)) if (brain.isActiveAndEnabled) count++;
            foreach (ScareTrigger scare in root.GetComponentsInChildren<ScareTrigger>(false)) if (scare.isActiveAndEnabled) count++;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(false)) if (source.isActiveAndEnabled && source.isPlaying) count++;
            return count;
        }

        private static int ActiveBrains(string sceneName)
        {
            int count = 0;
            foreach (SmartInfectedBrain brain in Object.FindObjectsByType<SmartInfectedBrain>(FindObjectsInactive.Exclude))
            {
                if (brain.gameObject.scene.name == sceneName && brain.isActiveAndEnabled) count++;
            }
            return count;
        }

        private static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        private static void CreateTempSpecificClip()
        {
            string source = CinematicPaths.VideoPath(Application.streamingAssetsPath, CinematicIds.Intro);
            string target = CinematicPaths.VideoPath(Application.streamingAssetsPath, TempSpecificClip);
            if (File.Exists(target)) return; // ya existe uno real: no se toca
            File.Copy(source, target);
            _tempClipCreated = true;
        }

        private static void DeleteTempCaptions()
        {
            if (_tempCaptionsPath != null && File.Exists(_tempCaptionsPath)) File.Delete(_tempCaptionsPath);
            _tempCaptionsPath = null;
        }

        private static void DeleteTempSpecificClip()
        {
            if (!_tempClipCreated) return;
            string target = CinematicPaths.VideoPath(Application.streamingAssetsPath, TempSpecificClip);
            if (File.Exists(target)) File.Delete(target);
            _tempClipCreated = false;
        }

        private static void Check(bool condition, string label)
        {
            if (condition) Pass(label);
            else Fail(label);
        }

        private static void Pass(string label)
        {
            _passed++;
            Lines.Add("PASS " + label);
            Debug.Log("[NexoPlaythroughCheck] PASS " + label);
        }

        private static void Fail(string label)
        {
            _failed++;
            Lines.Add("FAIL " + label);
            Debug.LogWarning("[NexoPlaythroughCheck] FAIL " + label);
        }

        private static void Log(string text)
        {
            Lines.Add(text);
            Debug.Log("[NexoPlaythroughCheck] " + text);
        }

        private static void End()
        {
            DeleteTempSpecificClip();
            DeleteTempCaptions();
            DeleteTempCorrupt();
            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInput;
                InputSystem.settings.backgroundBehavior = _savedBackground;
            }
            _keyboard = null;
            Unhook();
            Stack.Clear();

            var sb = new StringBuilder();
            sb.AppendLine($"NexoPlaythroughCheck {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {_passed} OK, {_failed} FALLOS, {_errors} errores de consola");
            foreach (string line in Lines) sb.AppendLine(line);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NexoPlaythroughCheck.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[NexoPlaythroughCheck] RESULTADO: {_passed} OK, {_failed} FALLOS → {path}");

            int code = _failed == 0 ? 0 : 1;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(ExitKey, code);
            Application.runInBackground = false;
            _started = false;
            EditorApplication.isPlaying = false;
            if (!Application.isBatchMode) SessionState.EraseInt(ExitKey);
        }
    }
}
