using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nexo.Player;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Build de entrega para Windows (Builds/NexoProtocoloSombra_Windows + .zip, carpeta ignorada por Git).
    /// SOLO durante el build:
    ///   - ElenaConfig._forceFallback = true y _secrets = null: Elena sin conexión y la clave nunca entra en el ejecutable;
    ///   - (opcional, por defecto sí) DemoShortcuts.EnableDemoShortcuts = true en PF_Player: F5 piso siguiente, F6 vida,
    ///     F7 munición, F8 al BioScanner, como salida de emergencia en la presentación.
    /// En el finally se reescriben los BYTES originales de ElenaConfig.asset y PF_Player.prefab (también si el build
    /// falla), así `git status` queda limpio y se conserva la referencia a ElenaSecrets. Copia de seguridad en
    /// Temp/NexoReleaseBackup por si el Editor se cierra a mitad de build (se restaura al abrir el proyecto).
    /// Las cinemáticas de Assets/StreamingAssets/Cinematics se copian solas (StreamingAssets); al terminar se listan.
    /// Menús: Nexo/Build/Windows (entrega: Elena offline + atajos de demo) y Nexo/Build/Windows (sin atajos de demo).
    /// Batchmode: -executeMethod Nexo.EditorTools.NexoReleaseBuilder.BuildBatch [-nexoNoShortcuts] [-nexoNoZip] [-nexoFolder Nombre].
    /// </summary>
    [InitializeOnLoad]
    public static class NexoReleaseBuilder
    {
        public const string ConfigPath = "Assets/_Project/Data/ElenaConfig.asset";
        public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Systems/PF_Player.prefab";
        public const string DefaultFolder = "NexoProtocoloSombra_Windows";
        private const string ExeName = "NexoProtocoloSombra.exe";
        private const string BackupDir = "Temp/NexoReleaseBackup";

        static NexoReleaseBuilder()
        {
            // Si un build anterior se interrumpió (Editor cerrado o colgado), se devuelven los archivos a su estado
            EditorApplication.delayCall += RestoreLeftoverBackup;
        }

        [MenuItem("Nexo/Build/Windows (entrega: Elena offline + atajos de demo)")]
        public static void BuildMenu() => Build(DefaultFolder, true, true);

        [MenuItem("Nexo/Build/Windows (sin atajos de demo)")]
        public static void BuildMenuNoShortcuts() => Build(DefaultFolder, false, true);

        /// <summary>Entrada para batchmode: sale con 0 si el build termina bien.</summary>
        public static void BuildBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool shortcuts = !args.Contains("-nexoNoShortcuts");
            bool zip = !args.Contains("-nexoNoZip");
            int folderArg = Array.IndexOf(args, "-nexoFolder");
            string folder = folderArg >= 0 && folderArg + 1 < args.Length ? args[folderArg + 1] : DefaultFolder;
            BuildResult result = Build(folder, shortcuts, zip);
            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Compatibilidad con llamadas anteriores: entrega con atajos de demo y zip.</summary>
        public static BuildResult Build(string folderName) => Build(folderName, true, true);

        /// <summary>Genera Builds/&lt;folderName&gt;/NexoProtocoloSombra.exe y, si se pide, Builds/&lt;folderName&gt;.zip.</summary>
        public static BuildResult Build(string folderName, bool demoShortcuts, bool zip)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError($"[NexoReleaseBuilder] Falta {ConfigPath} o {PlayerPrefabPath}");
                return BuildResult.Failed;
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            string folder = Path.Combine(root, folderName);
            string zipPath = folder + ".zip";
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            var originals = new Dictionary<string, byte[]>
            {
                { ConfigPath, File.ReadAllBytes(ConfigPath) },
                { PlayerPrefabPath, File.ReadAllBytes(PlayerPrefabPath) }
            };
            WriteBackup(originals);

            try
            {
                SetBuildOnlyValues(demoShortcuts);

                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(folder, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None // sin Development Build
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[NexoReleaseBuilder] {summary.result}: {summary.totalErrors} errores, {summary.totalWarnings} avisos, {summary.totalSize / 1048576f:F1} MB, {summary.totalTime.TotalSeconds:F0} s → {folder} (Elena offline, atajos de demo: {(demoShortcuts ? "sí" : "no")})");

                if (summary.result == BuildResult.Succeeded)
                {
                    LogCinematics(folder);
                    // Los símbolos de depuración no hacen falta para la entrega
                    foreach (string dir in Directory.GetDirectories(folder, "*_BurstDebugInformation_DoNotShip")) Directory.Delete(dir, true);
                    foreach (string dir in Directory.GetDirectories(folder, "*_BackUpThisFolder_ButDontShipItWithYourGame")) Directory.Delete(dir, true);
                    if (zip)
                    {
                        if (File.Exists(zipPath)) File.Delete(zipPath);
                        ZipFile.CreateFromDirectory(folder, zipPath, System.IO.Compression.CompressionLevel.Optimal, true);
                        Debug.Log($"[NexoReleaseBuilder] Zip: {zipPath} ({new FileInfo(zipPath).Length / 1048576f:F1} MB)");
                    }
                }
                return summary.result;
            }
            catch (Exception e)
            {
                Debug.LogError("[NexoReleaseBuilder] " + e);
                return BuildResult.Failed;
            }
            finally
            {
                // Se restaura siempre y byte a byte: en el repo Elena usa Gemini en el Editor y los atajos quedan apagados
                RestoreFiles(originals);
                DeleteBackup();
            }
        }

        private static void SetBuildOnlyValues(bool demoShortcuts)
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("_forceFallback").boolValue = true;
            so.FindProperty("_secrets").objectReferenceValue = null; // la clave nunca entra en el build
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            if (demoShortcuts)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                DemoShortcuts shortcuts = prefab.GetComponentInChildren<DemoShortcuts>(true);
                if (shortcuts == null)
                {
                    Debug.LogWarning("[NexoReleaseBuilder] PF_Player no tiene DemoShortcuts: el build sale sin atajos.");
                }
                else
                {
                    var sso = new SerializedObject(shortcuts);
                    sso.FindProperty("EnableDemoShortcuts").boolValue = true;
                    sso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(prefab);
                }
            }
            AssetDatabase.SaveAssets();

            bool fallback = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath)).FindProperty("_forceFallback").boolValue;
            DemoShortcuts check = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponentInChildren<DemoShortcuts>(true);
            Debug.Log($"[NexoReleaseBuilder] Valores del build: _forceFallback={fallback}, EnableDemoShortcuts={(check != null && check.EnableDemoShortcuts)}");
        }

        private static void RestoreFiles(Dictionary<string, byte[]> originals)
        {
            foreach (KeyValuePair<string, byte[]> pair in originals)
            {
                File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[NexoReleaseBuilder] ElenaConfig.asset y PF_Player.prefab restaurados a su contenido original (_forceFallback y atajos como en el repo).");
        }

        private static void LogCinematics(string folder)
        {
            string target = Path.Combine(folder, Path.GetFileNameWithoutExtension(ExeName) + "_Data", "StreamingAssets", "Cinematics");
            string[] clips = Directory.Exists(target) ? Directory.GetFiles(target, "*.mp4") : Array.Empty<string>();
            if (clips.Length == 0)
            {
                Debug.LogWarning($"[NexoReleaseBuilder] El build NO lleva cinemáticas (no hay .mp4 en Assets/StreamingAssets/Cinematics). El juego funciona sin ellas; se pueden copiar después en {target}");
                return;
            }
            Debug.Log($"[NexoReleaseBuilder] Cinemáticas en el build: {string.Join(", ", clips.Select(Path.GetFileName))}");
        }

        // ---------- Copia de seguridad ante un cierre a mitad de build ----------

        private static void WriteBackup(Dictionary<string, byte[]> originals)
        {
            Directory.CreateDirectory(BackupDir);
            foreach (KeyValuePair<string, byte[]> pair in originals)
            {
                File.WriteAllBytes(Path.Combine(BackupDir, Path.GetFileName(pair.Key)), pair.Value);
            }
        }

        private static void DeleteBackup()
        {
            if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, true);
        }

        private static void RestoreLeftoverBackup()
        {
            if (!Directory.Exists(BackupDir) || BuildPipeline.isBuildingPlayer) return;
            var originals = new Dictionary<string, byte[]>();
            foreach (string path in new[] { ConfigPath, PlayerPrefabPath })
            {
                string backup = Path.Combine(BackupDir, Path.GetFileName(path));
                if (File.Exists(backup)) originals[path] = File.ReadAllBytes(backup);
            }
            if (originals.Count == 0) return;
            Debug.LogWarning("[NexoReleaseBuilder] Se encontró una copia de un build interrumpido: se restauran ElenaConfig.asset y PF_Player.prefab.");
            RestoreFiles(originals);
            DeleteBackup();
        }
    }
}
