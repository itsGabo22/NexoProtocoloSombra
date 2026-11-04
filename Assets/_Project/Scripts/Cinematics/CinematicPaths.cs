using System.IO;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Rutas de los clips (lógica pura, probada en EditMode): &lt;streamingAssets&gt;/Cinematics/&lt;id&gt;.mp4 y
    /// &lt;id&gt;.captions.json al lado. Los ids solo admiten letras, dígitos, '_' y '-' (nada de rutas ni extensiones).
    /// </summary>
    public static class CinematicPaths
    {
        public const string FolderName = "Cinematics";
        public const string VideoExtension = ".mp4";
        public const string CaptionsSuffix = ".captions.json";

        /// <summary>True si el id es un nombre de archivo simple y seguro.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Carpeta de las cinemáticas dentro de StreamingAssets (vacía si la raíz no es válida).</summary>
        public static string Folder(string streamingAssetsPath)
        {
            return string.IsNullOrEmpty(streamingAssetsPath) ? string.Empty : Path.Combine(streamingAssetsPath, FolderName);
        }

        /// <summary>Ruta del vídeo del id; vacía si el id o la raíz no son válidos.</summary>
        public static string VideoPath(string streamingAssetsPath, string id)
        {
            if (!IsValidId(id) || string.IsNullOrEmpty(streamingAssetsPath)) return string.Empty;
            return Path.Combine(Folder(streamingAssetsPath), id + VideoExtension);
        }

        /// <summary>Ruta de los subtítulos opcionales del id; vacía si el id o la raíz no son válidos.</summary>
        public static string CaptionsPath(string streamingAssetsPath, string id)
        {
            if (!IsValidId(id) || string.IsNullOrEmpty(streamingAssetsPath)) return string.Empty;
            return Path.Combine(Folder(streamingAssetsPath), id + CaptionsSuffix);
        }
    }
}
