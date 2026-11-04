using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Comprobación estática (solo lectura) de las escenas de la build y de los prefabs de Assets/_Project:
    /// scripts que faltan, referencias rotas («Missing»), renderers sin material o con shader de URP / de error
    /// (rosa en Built-in) y AudioSources con clip roto. Menú Nexo/Checks/Integrity o
    /// -executeMethod Nexo.EditorTools.NexoIntegrityCheck.RunBatch (sale con 0 si no hay problemas).
    /// Resultado en Logs/NexoIntegrityCheck.txt. No guarda ni modifica nada.
    /// </summary>
    public static class NexoIntegrityCheck
    {
        private const string ProjectRoot = "Assets/_Project";

        [MenuItem("Nexo/Checks/Integrity (escenas y prefabs)")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int problems = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Integridad", problems == 0 ? "Sin problemas." : $"{problems} problema(s). Detalle en la Console.", "OK");
        }

        public static void RunBatch()
        {
            int problems = Run(out string report);
            Debug.Log(report);
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        public static int Run(out string report)
        {
            var issues = new List<string>();
            var stats = new StringBuilder();

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                int objects = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        Inspect(t.gameObject, $"{scene.name}:{Path(t)}", issues);
                    }
                }
                stats.AppendLine($"  escena {scene.name}: {objects} objetos");
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectRoot });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    issues.Add($"{path}: no se puede cargar el prefab");
                    continue;
                }
                foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Inspect(t.gameObject, $"{System.IO.Path.GetFileName(path)}:{Path(t)}", issues);
                }
            }
            stats.AppendLine($"  prefabs de {ProjectRoot}: {prefabGuids.Length}");

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { ProjectRoot });
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                string problem = ShaderProblem(material);
                if (problem != null) issues.Add($"{path}: {problem}");
            }
            stats.AppendLine($"  materiales de {ProjectRoot}: {materialGuids.Length}");

            var sb = new StringBuilder();
            sb.AppendLine($"[NexoIntegrityCheck] {issues.Count} problema(s)");
            sb.Append(stats);
            foreach (string issue in issues.Distinct()) sb.AppendLine("  ! " + issue);
            report = sb.ToString();

            string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "NexoIntegrityCheck.txt"));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            File.WriteAllText(file, report);
            return issues.Distinct().Count();
        }

        private static void Inspect(GameObject go, string where, List<string> issues)
        {
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingScripts > 0) issues.Add($"{where}: {missingScripts} script(s) que faltan");

            if (PrefabUtility.IsPrefabAssetMissing(go)) issues.Add($"{where}: prefab anidado que falta (Missing Prefab)");

            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                if (component is Renderer renderer && !(component is ParticleSystemRenderer))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                        {
                            if (renderer.enabled) issues.Add($"{where}: {renderer.GetType().Name} sin material en el hueco {i}");
                            continue;
                        }
                        string problem = ShaderProblem(materials[i]);
                        if (problem != null) issues.Add($"{where}: material {materials[i].name}: {problem}");
                    }
                }

                var so = new SerializedObject(component);
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue == null && !it.objectReferenceEntityIdValue.Equals(default(EntityId)))
                    {
                        // ElenaSecrets.asset no se versiona (repo público): sin él Elena usa el fallback; no es un fallo
                        if (it.propertyPath == "_secrets") continue;
                        issues.Add($"{where}: {component.GetType().Name}.{it.propertyPath} referencia rota (Missing)");
                    }
                }
            }
        }

        private static string ShaderProblem(Material material)
        {
            if (material == null) return null;
            Shader shader = material.shader;
            if (shader == null) return "sin shader";
            if (shader.name == "Hidden/InternalErrorShader") return "shader de error (se ve rosa)";
            if (shader.name.StartsWith("Universal Render Pipeline/") || shader.name.StartsWith("Shader Graphs/")) return $"shader {shader.name} (URP: rosa en Built-in)";
            if (!shader.isSupported) return $"shader {shader.name} no soportado";
            return null;
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
