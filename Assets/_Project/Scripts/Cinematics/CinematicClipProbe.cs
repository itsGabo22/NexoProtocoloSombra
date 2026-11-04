using System.Collections.Generic;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Lectura mínima de la cabecera de un MP4 (lógica pura, probada en EditMode) para el validador del Editor:
    /// resolución de la primera pista de vídeo (caja tkhd), códecs presentes (avc1 = H.264, hvc1/hev1 = HEVC,
    /// mp4a = AAC) y avisos de formato. No decodifica nada.
    /// </summary>
    public static class CinematicClipProbe
    {
        /// <summary>Resolución (0 si no se encuentra) y códecs detectados en los bytes del archivo.</summary>
        public readonly struct Info
        {
            public Info(int width, int height, bool hasH264, bool hasHevc, bool hasAac)
            {
                Width = width;
                Height = height;
                HasH264 = hasH264;
                HasHevc = hasHevc;
                HasAac = hasAac;
            }

            public int Width { get; }
            public int Height { get; }
            public bool HasH264 { get; }
            public bool HasHevc { get; }
            public bool HasAac { get; }
        }

        public static Info Read(byte[] data)
        {
            if (data == null || data.Length < 8) return new Info(0, 0, false, false, false);

            int width = 0;
            int height = 0;
            int index = IndexOf(data, "tkhd", 0);
            while (index >= 4 && width == 0)
            {
                // index apunta al tipo; la caja empieza 4 bytes antes (tamaño)
                int box = index - 4;
                int version = box + 8 < data.Length ? data[box + 8] : 0;
                int widthOffset = box + (version == 1 ? 96 : 84);
                if (widthOffset + 8 <= data.Length)
                {
                    // Ancho y alto en punto fijo 16.16
                    width = ReadUInt16(data, widthOffset);
                    height = ReadUInt16(data, widthOffset + 4);
                }
                if (width == 0) index = IndexOf(data, "tkhd", index + 4); // pista de audio: ancho 0
            }

            return new Info(width, height,
                IndexOf(data, "avc1", 0) >= 0,
                IndexOf(data, "hvc1", 0) >= 0 || IndexOf(data, "hev1", 0) >= 0,
                IndexOf(data, "mp4a", 0) >= 0);
        }

        /// <summary>Avisos para el validador (vacío si el clip cumple el formato pedido).</summary>
        public static List<string> Warnings(Info info)
        {
            var warnings = new List<string>();
            if (info.Width == 0 || info.Height == 0) warnings.Add("no se pudo leer la resolución (¿es un MP4?)");
            else if (!((info.Width == 1920 && info.Height == 1080) || (info.Width == 1280 && info.Height == 720)))
            {
                warnings.Add($"resolución {info.Width}x{info.Height}: se recomienda 1920x1080 o 1280x720");
            }
            if (info.Width > 0 && info.Height > 0 && System.Math.Abs(info.Width * 9 - info.Height * 16) > info.Height / 10)
            {
                warnings.Add("no es 16:9: se verá con bandas negras");
            }
            if (info.HasHevc) warnings.Add("vídeo HEVC/H.265: puede no reproducirse en todos los Windows; reexporta en H.264");
            else if (!info.HasH264) warnings.Add("no se detectó H.264 (avc1)");
            return warnings;
        }

        private static int ReadUInt16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

        private static int IndexOf(byte[] data, string tag, int start)
        {
            byte a = (byte)tag[0], b = (byte)tag[1], c = (byte)tag[2], d = (byte)tag[3];
            for (int i = start < 0 ? 0 : start; i + 3 < data.Length; i++)
            {
                if (data[i] == a && data[i + 1] == b && data[i + 2] == c && data[i + 3] == d) return i;
            }
            return -1;
        }
    }
}
