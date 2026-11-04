using System;
using System.Collections.Generic;
using System.IO;
using Nexo.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Genera el "enlace diferido" de la red de cámaras: abre cada escena de piso de forma aditiva (sin modificarla),
    /// renderiza el encuadre de cada cámara definida en SecurityCameraNetwork y guarda un PNG de 640x360 en
    /// UI/Textures/CameraArchive. Asigna el PNG y la hora a la definición. Las escenas se cierran sin guardar.
    /// Menú: Nexo/Cámaras/Capturar archivo de la red.
    /// </summary>
    public static class NexoCameraArchiveCapture
    {
        private const string NetworkPath = "Assets/_Project/Data/SecurityCameraNetwork.asset";
        private const string OutputFolder = "Assets/_Project/UI/Textures/CameraArchive";
        private const int Width = 640;
        private const int Height = 360;

        [MenuItem("Nexo/Cámaras/Capturar archivo de la red")]
        public static void CaptureAll()
        {
            var network = AssetDatabase.LoadAssetAtPath<SecurityCameraNetwork>(NetworkPath);
            if (network == null)
            {
                Debug.LogError("[NexoCameraArchiveCapture] No existe " + NetworkPath);
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[NexoCameraArchiveCapture] Sal del modo Play antes de capturar.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            var captured = new List<string>();
            var rng = new System.Random(612);

            foreach (int floor in network.Floors)
            {
                string scenePath = network.GetScenePath(floor);
                if (string.IsNullOrEmpty(scenePath)) continue;

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool openedHere = !scene.isLoaded;
                if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                Scene previousActive = SceneManager.GetActiveScene();
                // La escena activa define el ambiente y la niebla: debe ser la del piso que se captura
                SceneManager.SetActiveScene(scene);

                try
                {
                    foreach (CameraFeedDefinition feed in network.Feeds)
                    {
                        if (feed == null || feed.Floor != floor) continue;
                        string file = $"{OutputFolder}/CAM_{feed.Id.Replace(' ', '_')}.png";
                        if (Render(scene, feed, file))
                        {
                            captured.Add(file);
                            // Hora de "última sincronía" verosímil dentro de la noche del incidente
                            feed.ArchiveTimestamp = $"0{rng.Next(1, 4)}:{rng.Next(0, 60):00}:{rng.Next(0, 60):00}";
                        }
                    }
                }
                finally
                {
                    if (previousActive.IsValid() && previousActive.isLoaded && previousActive != scene) SceneManager.SetActiveScene(previousActive);
                    if (openedHere) EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.Refresh();
            foreach (CameraFeedDefinition feed in network.Feeds)
            {
                string file = $"{OutputFolder}/CAM_{feed.Id.Replace(' ', '_')}.png";
                if (!captured.Contains(file)) continue;
                // Un PNG recién creado puede no estar importado aún tras el Refresh: se fuerza su importación
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(file);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 1024;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }
                feed.ArchiveFrame = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            }

            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NexoCameraArchiveCapture] {captured.Count} capturas guardadas en {OutputFolder}.");
        }

        private static bool Render(Scene scene, CameraFeedDefinition feed, string file)
        {
            Camera sceneCamera = FindCamera(scene, feed.SceneCameraName);
            var go = new GameObject("TempArchiveCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var camera = go.AddComponent<Camera>();
                if (sceneCamera != null)
                {
                    camera.CopyFrom(sceneCamera);
                    go.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
                }
                else
                {
                    go.transform.SetPositionAndRotation(feed.Position, Quaternion.Euler(feed.EulerAngles));
                    camera.fieldOfView = feed.FieldOfView;
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 60f;
                }
                camera.scene = scene;
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(file, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NexoCameraArchiveCapture] {feed.Id}: {e.Message}");
                return false;
            }
            finally
            {
                Camera temp = go.GetComponent<Camera>();
                if (temp != null) temp.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Camera FindCamera(Scene scene, string cameraName)
        {
            if (string.IsNullOrEmpty(cameraName)) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.name == cameraName) return camera;
                }
            }
            return null;
        }
    }
}
