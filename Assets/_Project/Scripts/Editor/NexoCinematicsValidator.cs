using System.Collections.Generic;
using System.IO;
using System.Text;
using Nexo.Cinematics;
using UnityEditor;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Menú Nexo/Cinematics/Validate: comprueba la carpeta Assets/StreamingAssets/Cinematics (ignorada por Git):
    /// clips esperados (intro, floor_transition, ending) y opcionales, tamaño, resolución, códec y archivos con otra
    /// extensión. Solo lee: no importa ni modifica nada. RunBatch sirve para -executeMethod (sale con 0 si están los obligatorios).
    /// </summary>
    public static class NexoCinematicsValidator
    {
        [MenuItem("Nexo/Cinematics/Validate")]
        public static void ValidateMenu()
        {
            int missing = Validate(out string report);
            Debug.Log(report);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Cinemáticas", missing == 0
                    ? "Están los 3 clips obligatorios. Detalle en la Console."
                    : $"Faltan {missing} clip(s) obligatorio(s): el juego seguirá sin ellos. Detalle en la Console.", "OK");
            }
        }

        [MenuItem("Nexo/Cinematics/Open Folder")]
        public static void OpenFolder()
        {
            string folder = CinematicPaths.Folder(Application.streamingAssetsPath);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>Para -executeMethod: registra el informe y sale con 0 si están los clips obligatorios, 1 si no.</summary>
        public static void RunBatch()
        {
            int missing = Validate(out string report);
            Debug.Log(report);
            EditorApplication.Exit(missing == 0 ? 0 : 1);
        }

        /// <summary>Devuelve cuántos clips obligatorios faltan y el informe completo.</summary>
        public static int Validate(out string report)
        {
            string folder = CinematicPaths.Folder(Application.streamingAssetsPath);
            var sb = new StringBuilder();
            sb.AppendLine("[NexoCinematicsValidator] Carpeta: " + folder);

            int missing = 0;
            var expected = new HashSet<string>();
            foreach (string id in CinematicIds.Required)
            {
                expected.Add(id);
                if (!Describe(id, true, sb)) missing++;
            }
            foreach (string id in CinematicIds.Optional)
            {
                expected.Add(id);
                Describe(id, false, sb);
            }

            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".meta") || name == "README.txt") continue;
                    if (name.EndsWith(CinematicPaths.CaptionsSuffix))
                    {
                        string captionsId = name.Substring(0, name.Length - CinematicPaths.CaptionsSuffix.Length);
                        int lines = CinematicCaptionTrack.Parse(File.ReadAllText(file)).Count;
                        sb.AppendLine($"  · subtítulos {name}: {lines} línea(s) válida(s){(expected.Contains(captionsId) ? string.Empty : " (no corresponde a ningún clip esperado)")}");
                        continue;
                    }
                    if (!name.EndsWith(CinematicPaths.VideoExtension))
                    {
                        sb.AppendLine($"  ! {name}: extensión no válida, el juego solo busca .mp4");
                    }
                    else if (!expected.Contains(Path.GetFileNameWithoutExtension(name)))
                    {
                        sb.AppendLine($"  ! {name}: nombre no esperado, el juego no lo usará");
                    }
                }
            }
            else
            {
                sb.AppendLine("  ! La carpeta no existe (menú Nexo/Cinematics/Open Folder la crea).");
            }

            sb.AppendLine(missing == 0
                ? "  Resultado: los 3 clips obligatorios están presentes."
                : $"  Resultado: faltan {missing} clip(s) obligatorio(s); el juego continúa sin ellos.");
            report = sb.ToString();
            return missing;
        }

        private static bool Describe(string id, bool required, StringBuilder sb)
        {
            string path = CinematicPaths.VideoPath(Application.streamingAssetsPath, id);
            if (!File.Exists(path))
            {
                sb.AppendLine(required ? $"  ✗ {id}.mp4: FALTA (obligatorio)" : $"  - {id}.mp4: no está (opcional; se usa floor_transition)");
                return false;
            }

            byte[] data = File.ReadAllBytes(path);
            CinematicClipProbe.Info info = CinematicClipProbe.Read(data);
            sb.Append($"  ✓ {id}.mp4: {data.Length / 1048576f:0.00} MB, {info.Width}x{info.Height}, {(info.HasH264 ? "H.264" : info.HasHevc ? "HEVC" : "códec ?")}, {(info.HasAac ? "audio AAC" : "sin audio AAC")}");
            List<string> warnings = CinematicClipProbe.Warnings(info);
            if (data.Length == 0) warnings.Add("archivo vacío");
            else if (data.Length > 150L * 1048576) warnings.Add("más de 150 MB: revisa el bitrate");
            sb.AppendLine(warnings.Count == 0 ? string.Empty : " — AVISO: " + string.Join("; ", warnings));
            return true;
        }
    }
}
