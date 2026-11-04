using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Color de los personajes Ch33 (Alex y la figura de Elena). El FBX trae sus texturas incrustadas pero Unity no las
    /// extraía: los materiales salían grises. Esta herramienta:
    /// 1) extrae las texturas del FBX y las reduce (color 2048 JPG, normal 1024 PNG, pelo 1024 PNG con alfa) para no
    ///    subir 50 MB al repositorio;
    /// 2) crea MAT_Alex_Body / MAT_Alex_Hair con esas texturas y los asigna al FBX (remap): Alex deja de ser gris;
    /// 3) da a Elena un material por pieza: piel con la textura real y ropa de niña en colores con el mapa de normales
    ///    (pliegues), pelo castaño, con un leve brillo para que se vea tras el cristal a oscuras.
    /// Idempotente. Menú: Nexo/Characters/Aplicar color (Alex y Elena).
    /// </summary>
    public static class NexoCharacterColors
    {
        private const string FbxPath = "Assets/_Project/Models/Characters/Ch33_nonPBR1.fbx";
        private const string TextureFolder = "Assets/_Project/Models/Characters/Ch33_Textures";
        private const string MaterialFolder = "Assets/_Project/Materials/Characters";
        private const string ElenaPrefab = "Assets/_Project/Prefabs/Characters/PF_ElenaVisual.prefab";
        private const string DiffusePath = TextureFolder + "/Ch33_Diffuse.jpg";
        private const string NormalPath = TextureFolder + "/Ch33_Normal.png";
        private const string HairPath = TextureFolder + "/Ch33_Hair.png";

        [MenuItem("Nexo/Characters/Aplicar color (Alex y Elena)")]
        public static void ApplyAll()
        {
            if (!PrepareTextures()) return;
            ApplyAlex();
            ApplyElenaPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[NexoCharacterColors] Alex y Elena con color.");
        }

        // ---------- Texturas ----------

        private static bool PrepareTextures()
        {
            if (File.Exists(DiffusePath) && File.Exists(NormalPath) && File.Exists(HairPath)) return true;

            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[NexoCharacterColors] No se encontró " + FbxPath);
                return false;
            }

            string temp = Path.Combine(Path.GetTempPath(), "NexoCh33Textures");
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            Directory.CreateDirectory(temp);
            if (!importer.ExtractTextures(temp))
            {
                Debug.LogError("[NexoCharacterColors] El FBX no tiene texturas incrustadas.");
                return false;
            }

            Directory.CreateDirectory(TextureFolder);
            Downscale(Path.Combine(temp, "Ch33_1001_Diffuse.png"), DiffusePath, 2048, false, true);
            Downscale(Path.Combine(temp, "Ch33_1001_Normal.png"), NormalPath, 1024, true, false);
            Downscale(Path.Combine(temp, "Ch33_1002_Diffuse.png"), HairPath, 1024, false, false);
            Directory.Delete(temp, true);
            AssetDatabase.Refresh();

            ConfigureTexture(DiffusePath, TextureImporterType.Default, true, 2048);
            ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false, 1024);
            ConfigureTexture(HairPath, TextureImporterType.Default, true, 1024);
            return true;
        }

        private static void Downscale(string source, string target, int size, bool linear, bool jpg)
        {
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            original.LoadImage(File.ReadAllBytes(source));
            int width = Mathf.Min(size, original.width);
            int height = Mathf.Min(size, original.height);

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            Graphics.Blit(original, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var resized = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            resized.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resized.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(target, jpg ? resized.EncodeToJPG(90) : resized.EncodeToPNG());
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(resized);
        }

        private static void ConfigureTexture(string path, TextureImporterType type, bool srgb, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = maxSize;
            importer.alphaIsTransparency = path == HairPath;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        // ---------- Alex ----------

        private static void ApplyAlex()
        {
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D hairTex = AssetDatabase.LoadAssetAtPath<Texture2D>(HairPath);

            Material body = GetMaterial("MAT_Alex_Body", Color.white, diffuse, normal, 0.25f, Color.black);
            Material hair = GetMaterial("MAT_Alex_Hair", Color.white, hairTex, null, 0.15f, Color.black);
            SetCutout(hair, 0.4f);

            // Remap de los materiales incrustados del FBX: vale para cualquier instancia de Ch33 (Alex y su sombra)
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Ch33_body"), body);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Ch33_hair"), hair);
            importer.SaveAndReimport();

            // Asignación explícita en PF_Player: si el modelo no es un prefab anidado, el remap no le llega
            const string playerPath = "Assets/_Project/Prefabs/Systems/PF_Player.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                foreach (SkinnedMeshRenderer smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.name.StartsWith("Ch33_")) continue;
                    Material mat = smr.name == "Ch33_Hair" || smr.name == "Ch33_Eyelashes" ? hair : body;
                    var mats = new Material[smr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    smr.sharedMaterials = mats;
                }
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        // ---------- Elena ----------

        /// <summary>Aplica los materiales de Elena a una instancia (lo usa también Nexo/Elena/Rebuild Visual).</summary>
        public static bool ApplyElena(GameObject instance)
        {
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DiffusePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (diffuse == null || normal == null) return false;

            Color glow = new Color(0.03f, 0.035f, 0.04f);
            var map = new Dictionary<string, Material>
            {
                // Piel con la textura real (cara y manos), algo más cálida
                ["Ch33_Body"] = GetMaterial("MAT_Elena_Skin", new Color(1f, 0.93f, 0.86f), diffuse, normal, 0.3f, glow),
                // Ropa de niña: color propio + pliegues del mapa de normales
                ["Ch33_Suit"] = GetMaterial("MAT_Elena_Jacket", new Color(0.95f, 0.72f, 0.18f), null, normal, 0.35f, glow),
                ["Ch33_Shirt"] = GetMaterial("MAT_Elena_Shirt", new Color(0.93f, 0.55f, 0.62f), null, normal, 0.2f, glow),
                ["Ch33_Tie"] = GetMaterial("MAT_Elena_Shirt", new Color(0.93f, 0.55f, 0.62f), null, normal, 0.2f, glow),
                ["Ch33_Pants"] = GetMaterial("MAT_Elena_Pants", new Color(0.22f, 0.33f, 0.55f), null, normal, 0.25f, glow),
                ["Ch33_Belt"] = GetMaterial("MAT_Elena_Pants", new Color(0.22f, 0.33f, 0.55f), null, normal, 0.25f, glow),
                ["Ch33_Shoes"] = GetMaterial("MAT_Elena_Shoes", new Color(0.85f, 0.2f, 0.25f), null, normal, 0.45f, glow),
                ["Ch33_Hair"] = GetMaterial("MAT_Elena_HairTone", new Color(0.32f, 0.2f, 0.12f), null, normal, 0.3f, glow),
                ["Ch33_Eyelashes"] = GetMaterial("MAT_Elena_HairTone", new Color(0.32f, 0.2f, 0.12f), null, normal, 0.3f, glow)
            };
            // Melena (primitivas añadidas por el constructor de Gabriel): castaño, no casi negro
            Material hairCap = GetMaterial("MAT_Elena_Hair", new Color(0.3f, 0.19f, 0.11f), null, null, 0.3f, glow);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material mat = map.TryGetValue(r.name, out Material m) ? m : r.name.StartsWith("Hair_") ? hairCap : null;
                if (mat == null) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            return true;
        }

        private static void ApplyElenaPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ElenaPrefab);
            try
            {
                if (ApplyElena(root)) PrefabUtility.SaveAsPrefabAsset(root, ElenaPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- Utilidades ----------

        private static Material GetMaterial(string name, Color color, Texture2D albedo, Texture2D normal, float smoothness, Color emission)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Standard");
            material.color = color;
            material.SetTexture("_MainTex", albedo);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                // Emisión en tiempo real: el Inspector no le quita la palabra clave
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetCutout(Material material, float cutoff)
        {
            material.SetFloat("_Mode", 1f);
            material.SetFloat("_Cutoff", cutoff);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
        }
    }
}
