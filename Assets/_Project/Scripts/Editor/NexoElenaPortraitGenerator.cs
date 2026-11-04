using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Genera el retrato holográfico de Elena (Assets/_Project/Resources/Elena/portrait.png) que HUDController carga con
    /// Resources.Load("Elena/portrait"). Es un avatar estilizado, sin rostro realista: busto infantil abstracto con
    /// pelo largo, anillo con marcas y líneas de barrido. Se dibuja en blanco sobre fondo transparente para que el USS lo
    /// tiña con var(--c-accent) (azul NexoOS, rojo colmena, alto contraste y daltonismo sin texturas extra).
    /// Determinista: ejecutarlo de nuevo produce el mismo PNG. Menú: Nexo/Elena/Generate Portrait.
    /// </summary>
    public static class NexoElenaPortraitGenerator
    {
        public const string AssetPath = "Assets/_Project/Resources/Elena/portrait.png";
        private const int Size = 256;
        private const int Supersample = 4;

        [MenuItem("Nexo/Elena/Generate Portrait")]
        public static void GenerateMenu()
        {
            Generate();
            EditorUtility.DisplayDialog("Retrato de Elena", "Generado en " + AssetPath, "OK");
        }

        /// <summary>Entrada para -batchmode -executeMethod Nexo.EditorTools.NexoElenaPortraitGenerator.GenerateBatch.</summary>
        public static void GenerateBatch()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        public static void Generate()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Supermuestreo 4x4 para bordes suaves
                    float alpha = 0f;
                    float light = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float px = x + (sx + 0.5f) / Supersample;
                            float py = y + (sy + 0.5f) / Supersample;
                            // Coordenadas en [-1, 1] con +v hacia arriba (Texture2D empieza abajo a la izquierda)
                            float u = px / Size * 2f - 1f;
                            float v = py / Size * 2f - 1f;
                            Sample(u, v, y, out float a, out float l);
                            alpha += a;
                            light += l * a;
                        }
                    }
                    int samples = Supersample * Supersample;
                    float meanAlpha = alpha / samples;
                    float meanLight = alpha > 0f ? light / alpha : 1f;
                    byte grey = (byte)Mathf.RoundToInt(Mathf.Clamp01(meanLight) * 255f);
                    pixels[y * Size + x] = new Color32(grey, grey, grey, (byte)Mathf.RoundToInt(Mathf.Clamp01(meanAlpha) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            string fullPath = Path.GetFullPath(AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(AssetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log("[NexoElenaPortraitGenerator] Retrato generado: " + AssetPath);
        }

        /// <summary>Devuelve opacidad y brillo (0-1) de un punto del retrato.</summary>
        private static void Sample(float u, float v, int row, out float alpha, out float light)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            alpha = 0f;
            light = 1f;

            // Anillo interior con 12 marcas (estilo HUD); el borde exterior lo pone el USS según la emoción
            float ring = Band(r, 0.86f, 0.035f);
            float angle = Mathf.Atan2(v, u);
            float tick = Mathf.Abs(Mathf.Sin(angle * 6f)) > 0.985f ? Band(r, 0.78f, 0.05f) : 0f;
            float ringAlpha = Mathf.Max(ring * 0.75f, tick * 0.9f);

            if (r > 0.82f)
            {
                alpha = ringAlpha;
                return;
            }

            // Halo holográfico de fondo (más intenso en el centro)
            float glow = 0.16f * (1f - r / 0.82f);

            // Silueta del busto: cabeza, pelo largo, cuello y hombros (tamaño infantil, sin rasgos)
            float head = Circle(u, v, 0f, 0.20f, 0.25f);
            // Pelo largo: se abre hacia abajo (melena) y cae sobre los hombros
            float flare = Mathf.Clamp01((0.20f - v) / 0.50f) * 0.07f;
            float hair = Mathf.Min(Circle(u, v, 0f, 0.23f, 0.30f), RoundedBox(u, v, 0f, -0.04f, 0.29f + flare, 0.30f, 0.12f));
            float neck = RoundedBox(u, v, 0f, -0.12f, 0.09f, 0.12f, 0.04f);
            float shoulders = Ellipse(u, v, 0f, -0.62f, 0.58f, 0.36f);
            float body = Mathf.Min(Mathf.Min(head, hair), Mathf.Min(neck, shoulders));

            float fill = Smooth(body);
            // Pelo un poco más tenue que el rostro y el torso para que se lea la forma sin dibujar una cara
            bool isHairOnly = hair < 0f && head > 0f && neck > 0f && shoulders > 0f;
            float fillAlpha = isHairOnly ? 0.38f : (head < 0f ? 0.92f : 0.7f);
            // Degradado vertical: más luz arriba, como un holograma proyectado desde abajo
            float vertical = Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(-0.8f, 0.5f, v));
            // Contorno exterior y contorno del rostro (separa cara y melena sin dibujar rasgos)
            float rim = Mathf.Max(Band(body, 0f, 0.022f), Band(head, 0f, 0.016f) * 0.9f);

            float silhouette = Mathf.Max(fill * fillAlpha * vertical, rim);

            // Líneas de barrido: bandas de 8 px (2 px a 64 px de altura en el HUD)
            float scan = (row / 4) % 2 == 0 ? 1f : 0.62f;

            alpha = Mathf.Max(glow, silhouette * scan);
            alpha = Mathf.Max(alpha, ringAlpha);
            light = 1f;
        }

        // Distancia con signo (negativa dentro)
        private static float Circle(float u, float v, float cx, float cy, float radius)
        {
            float dx = u - cx, dy = v - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
        }

        private static float RoundedBox(float u, float v, float cx, float cy, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(u - cx) - halfWidth + radius;
            float qy = Mathf.Abs(v - cy) - halfHeight + radius;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float Smooth(float distance) => distance < 0f ? 1f : 0f;

        private static float Band(float value, float center, float halfWidth) => Mathf.Abs(value - center) <= halfWidth ? 1f : 0f;
    }
}
