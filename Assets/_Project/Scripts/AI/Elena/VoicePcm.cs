using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Audio PCM de 16 bits de la síntesis de voz de Gemini (lógica pura, testeada): decodifica WAV (RIFF) o L16 crudo,
    /// acorta silencios largos, recorta los extremos con fundidos y vuelve a codificar a WAV.
    /// </summary>
    public static class VoicePcm
    {
        /// <summary>Decodifica a float mono. <paramref name="mimeType"/> «audio/L16;...;rate=24000» = PCM crudo.</summary>
        public static bool TryDecode(byte[] data, string mimeType, out float[] samples, out int sampleRate)
        {
            samples = null;
            sampleRate = 24000;
            if (data == null || data.Length < 4) return false;

            int offset = 0, length = data.Length, channels = 1;
            if (data.Length >= 12 && Encoding.ASCII.GetString(data, 0, 4) == "RIFF")
            {
                int p = 12;
                int bits = 16;
                bool found = false;
                while (p + 8 <= data.Length)
                {
                    string id = Encoding.ASCII.GetString(data, p, 4);
                    int size = BitConverter.ToInt32(data, p + 4);
                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToInt16(data, p + 10);
                        sampleRate = BitConverter.ToInt32(data, p + 12);
                        bits = BitConverter.ToInt16(data, p + 22);
                    }
                    else if (id == "data")
                    {
                        offset = p + 8;
                        length = Math.Min(size < 0 ? int.MaxValue : size, data.Length - offset);
                        found = true;
                        break;
                    }
                    p += 8 + size + (size & 1);
                }
                if (!found || bits != 16) return false;
            }
            else if (mimeType != null)
            {
                int idx = mimeType.IndexOf("rate=", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && int.TryParse(new string(TakeDigits(mimeType, idx + 5)), out int r)) sampleRate = r;
            }

            channels = Math.Max(1, channels);
            int frames = length / (2 * channels);
            samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++) sum += BitConverter.ToInt16(data, offset + (i * channels + c) * 2);
                samples[i] = sum / (32768f * channels);
            }
            return frames > 0;
        }

        private static char[] TakeDigits(string s, int start)
        {
            var list = new List<char>();
            for (int i = start; i < s.Length && char.IsDigit(s[i]); i++) list.Add(s[i]);
            return list.ToArray();
        }

        /// <summary>
        /// Recorta silencio inicial/final y reduce a <paramref name="keepSeconds"/> los silencios internos mayores que
        /// <paramref name="maxGapSeconds"/> (umbral relativo al pico). Añade fundidos de 10 ms en los extremos.
        /// </summary>
        public static float[] CompressSilence(float[] samples, int sampleRate, float maxGapSeconds = 0.45f, float keepSeconds = 0.28f, float threshold = 0.02f)
        {
            if (samples == null || samples.Length == 0) return samples ?? Array.Empty<float>();
            float peak = 0f;
            foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
            if (peak <= 0f) return Array.Empty<float>();

            int window = Math.Max(1, sampleRate / 100); // 10 ms
            int windows = (samples.Length + window - 1) / window;
            var loud = new bool[windows];
            float limit = peak * threshold;
            for (int w = 0; w < windows; w++)
            {
                double acc = 0;
                int start = w * window, end = Math.Min(samples.Length, start + window);
                for (int i = start; i < end; i++) acc += samples[i] * samples[i];
                loud[w] = Math.Sqrt(acc / Math.Max(1, end - start)) > limit;
            }

            int first = Array.IndexOf(loud, true);
            int last = Array.LastIndexOf(loud, true);
            if (first < 0) return Array.Empty<float>();

            int maxGap = (int)(maxGapSeconds * 100f), keep = (int)(keepSeconds * 100f);
            var output = new List<float>(samples.Length);
            int pad = 3; // 30 ms antes y después de la voz
            int w0 = Math.Max(0, first - pad), w1 = Math.Min(windows - 1, last + pad);
            int gapStart = -1;
            for (int w = w0; w <= w1; w++)
            {
                if (!loud[w])
                {
                    if (gapStart < 0) gapStart = w;
                    continue;
                }
                if (gapStart >= 0)
                {
                    int gap = w - gapStart;
                    int copy = gap > maxGap ? keep : gap;
                    // Se conserva la mitad del silencio al principio y la mitad al final del hueco
                    AppendWindows(samples, window, gapStart, gapStart + copy / 2, output);
                    AppendWindows(samples, window, w - (copy - copy / 2), w, output);
                    gapStart = -1;
                }
                AppendWindows(samples, window, w, w + 1, output);
            }
            if (gapStart >= 0) AppendWindows(samples, window, gapStart, w1 + 1, output);

            float[] result = output.ToArray();
            int fade = Math.Min(result.Length / 2, sampleRate / 100);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                result[i] *= k;
                result[result.Length - 1 - i] *= k;
            }
            return result;
        }

        private static void AppendWindows(float[] samples, int window, int fromWindow, int toWindow, List<float> output)
        {
            int start = Math.Max(0, fromWindow * window), end = Math.Min(samples.Length, toWindow * window);
            for (int i = start; i < end; i++) output.Add(samples[i]);
        }

        /// <summary>
        /// Corta un audio con varias líneas habladas en <paramref name="parts"/> trozos por sus silencios más largos
        /// (al menos <paramref name="minGapSeconds"/>). Devuelve null si no hay suficientes silencios o algún trozo
        /// queda demasiado corto para ser una línea.
        /// </summary>
        public static List<float[]> SplitOnLongestGaps(float[] samples, int sampleRate, int parts, float minGapSeconds = 0.3f, float threshold = 0.02f)
        {
            if (samples == null || samples.Length == 0 || parts < 1) return null;
            if (parts == 1) return new List<float[]> { samples };

            float peak = 0f;
            foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
            if (peak <= 0f) return null;
            int window = Math.Max(1, sampleRate / 100);
            int windows = (samples.Length + window - 1) / window;
            var loud = new bool[windows];
            for (int w = 0; w < windows; w++)
            {
                double acc = 0;
                int start = w * window, end = Math.Min(samples.Length, start + window);
                for (int i = start; i < end; i++) acc += samples[i] * samples[i];
                loud[w] = Math.Sqrt(acc / Math.Max(1, end - start)) > peak * threshold;
            }
            int first = Array.IndexOf(loud, true), last = Array.LastIndexOf(loud, true);
            if (first < 0) return null;

            var gaps = new List<(int start, int length)>();
            int gapStart = -1;
            for (int w = first; w <= last; w++)
            {
                if (!loud[w]) { if (gapStart < 0) gapStart = w; continue; }
                if (gapStart >= 0) { gaps.Add((gapStart, w - gapStart)); gapStart = -1; }
            }
            int minGap = (int)(minGapSeconds * 100f);
            List<(int start, int length)> cuts = gaps.Where(g => g.length >= minGap).OrderByDescending(g => g.length).Take(parts - 1).OrderBy(g => g.start).ToList();
            if (cuts.Count < parts - 1) return null;

            var result = new List<float[]>();
            int from = 0;
            foreach ((int start, int length) in cuts)
            {
                int cut = (start + length / 2) * window;
                result.Add(Slice(samples, from, cut));
                from = cut;
            }
            result.Add(Slice(samples, from, samples.Length));
            foreach (float[] r in result) if (r.Length < sampleRate * 0.35f) return null;
            return result;
        }

        private static float[] Slice(float[] samples, int from, int to)
        {
            var slice = new float[Math.Max(0, to - from)];
            Array.Copy(samples, from, slice, 0, slice.Length);
            return slice;
        }

        /// <summary>
        /// Remuestreo lineal: con <paramref name="factor"/> &gt; 1 el audio dura menos y suena más agudo
        /// (sube el tono de la voz de reserva de Windows).
        /// </summary>
        public static float[] Resample(float[] samples, float factor)
        {
            if (samples == null || samples.Length == 0 || factor <= 0f || Math.Abs(factor - 1f) < 1e-4f) return samples;
            int length = (int)(samples.Length / factor);
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                float src = i * factor;
                int a = (int)src;
                int b = Math.Min(a + 1, samples.Length - 1);
                float k = src - a;
                result[i] = samples[a] * (1f - k) + samples[b] * k;
            }
            return result;
        }

        /// <summary>Normaliza al pico indicado (p. ej. 0,89 ≈ -1 dBFS).</summary>
        public static void Normalize(float[] samples, float targetPeak = 0.89f)
        {
            float peak = 0f;
            foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
            if (peak <= 1e-5f) return;
            float gain = targetPeak / peak;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        }

        public static byte[] EncodeWav(float[] samples, int sampleRate)
        {
            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
            foreach (float s in samples) writer.Write((short)(Math.Max(-1f, Math.Min(1f, s)) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
