using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nexo.AI.Elena;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Build de entrega para Windows con Elena sin conexión y SIN la clave de Gemini dentro del ejecutable:
    /// pone ElenaConfig._forceFallback = true y _secrets = null solo durante el build y lo restaura siempre después
    /// (también si el build falla). Usa la lista de escenas de Build Profiles y comprime la carpeta en un .zip.
    /// Menú: Nexo/Build/Windows (entrega con Elena offline).
    /// </summary>
    public static class NexoReleaseBuilder
    {
        private const string ConfigPath = "Assets/_Project/Data/ElenaConfig.asset";
        private const string ExeName = "NexoProtocoloSombra.exe";

        [MenuItem("Nexo/Build/Windows (entrega con Elena offline)")]
        public static void BuildMenu() => Build("NexoProtocoloSombra_Windows");

        /// <summary>Genera Builds/&lt;folderName&gt;/NexoProtocoloSombra.exe y Builds/&lt;folderName&gt;.zip.</summary>
        public static BuildResult Build(string folderName)
        {
            var config = AssetDatabase.LoadAssetAtPath<ElenaConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[NexoReleaseBuilder] No existe " + ConfigPath);
                return BuildResult.Failed;
            }

            var so = new SerializedObject(config);
            SerializedProperty fallback = so.FindProperty("_forceFallback");
            SerializedProperty secrets = so.FindProperty("_secrets");
            bool previousFallback = fallback.boolValue;
            // El build descarga assets de memoria: se guarda la ruta para recargar la referencia al terminar
            string previousSecretsPath = secrets.objectReferenceValue != null ? AssetDatabase.GetAssetPath(secrets.objectReferenceValue) : string.Empty;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            string folder = Path.Combine(root, folderName);
            string zip = folder + ".zip";
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            try
            {
                fallback.boolValue = true;
                secrets.objectReferenceValue = null; // la clave nunca entra en el build
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(folder, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[NexoReleaseBuilder] {summary.result}: {summary.totalErrors} errores, {summary.totalWarnings} avisos, {summary.totalSize / 1048576f:F1} MB, {summary.totalTime.TotalSeconds:F0} s → {folder}");

                if (summary.result == BuildResult.Succeeded)
                {
                    // Los símbolos de depuración no hacen falta para la entrega
                    foreach (string dir in Directory.GetDirectories(folder, "*_BurstDebugInformation_DoNotShip")) Directory.Delete(dir, true);
                    foreach (string dir in Directory.GetDirectories(folder, "*_BackUpThisFolder_ButDontShipItWithYourGame")) Directory.Delete(dir, true);
                    if (File.Exists(zip)) File.Delete(zip);
                    ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, true);
                    Debug.Log($"[NexoReleaseBuilder] Zip: {zip} ({new FileInfo(zip).Length / 1048576f:F1} MB)");
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
                // Se restaura siempre: en el repo Elena usa Gemini en el Editor
                config = AssetDatabase.LoadAssetAtPath<ElenaConfig>(ConfigPath);
                so = new SerializedObject(config);
                so.FindProperty("_forceFallback").boolValue = previousFallback;
                so.FindProperty("_secrets").objectReferenceValue = string.IsNullOrEmpty(previousSecretsPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ElenaSecrets>(previousSecretsPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!string.IsNullOrEmpty(previousSecretsPath) && so.FindProperty("_secrets").objectReferenceValue == null)
                {
                    Debug.LogWarning("[NexoReleaseBuilder] No se pudo restaurar la referencia a ElenaSecrets: ejecuta git restore " + ConfigPath);
                }
                AssetDatabase.SaveAssets();
            }
        }
    }
}
