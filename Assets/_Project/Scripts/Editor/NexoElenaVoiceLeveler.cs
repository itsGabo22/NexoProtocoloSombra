using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Iguala las 51 líneas de voz de Elena (Gemini TTS y voz de reserva de Windows) sin regenerarlas:
    /// - misma frecuencia de muestreo (24 kHz, la de Gemini; la de Windows es 22,05 kHz);
    /// - mismo volumen percibido: RMS objetivo -18 dBFS (antes iban de -24 a -12,8 dB porque solo se normalizaba el pico);
    /// - limitador suave para que ningún pico supere -1 dBFS (sin recortes);
    /// - silencio inicial y final recortado a 30 ms.
    /// El filtro de radio es el mismo para todas (ElenaVoicePlayer, en tiempo de ejecución). Idempotente: aplicarlo
    /// dos veces da el mismo resultado. Ejecutarlo de nuevo tras Nexo/Elena/Generate Voice.
    /// Menú: Nexo/Elena/Igualar voces (volumen y frecuencia).
    /// </summary>
    public static class NexoElenaVoiceLeveler
    {
        public const string VoiceFolder = "Assets/_Project/Audio/Elena/Voice";
        private const int TargetRate = 24000;
        private const float TargetRmsDb = -18f;
        private const float CeilingDb = -1f;
        private const float KneeDb = -6f;
        private const float SilenceThreshold = 0.01f;
        private const float EdgeSeconds = 0.03f;

        [MenuItem("Nexo/Elena/Igualar voces (volumen y frecuencia)")]
        public static void LevelAll()
        {
            var sb = new StringBuilder("[NexoElenaVoiceLeveler]\n");
            int count = 0;
            foreach (string path in Directory.GetFiles(VoiceFolder, "*.wav"))
            {
                if (!ReadWav(path, out float[] samples, out int rate)) { sb.AppendLine("  ilegible: " + Path.GetFileName(path)); continue; }

                float beforeRms = RmsDb(samples);
                if (rate != TargetRate) samples = Resample(samples, rate, TargetRate);
                samples = TrimSilence(samples, TargetRate);
                Level(samples);
                WriteWav(path, samples, TargetRate);
                sb.AppendLine($"  {Path.GetFileName(path)}: {rate} Hz → {TargetRate} Hz · RMS {beforeRms:0.0} → {RmsDb(samples):0.0} dB · pico {PeakDb(samples):0.0} dB");
                count++;
            }
            AssetDatabase.Refresh();
            Debug.Log(sb.Append($"  {count} archivos igualados.").ToString());
        }

        // ---------- Procesado ----------

        /// <summary>Lleva el RMS al objetivo y limita los picos con una rodilla suave por encima de -6 dBFS.</summary>
        private static void Level(float[] s)
        {
            float rms = Rms(s);
            if (rms <= 1e-6f) return;
            float gain = Mathf.Pow(10f, TargetRmsDb / 20f) / rms;
            float knee = Mathf.Pow(10f, KneeDb / 20f);
            float ceiling = Mathf.Pow(10f, CeilingDb / 20f);
            float room = ceiling - knee;
            for (int i = 0; i < s.Length; i++)
            {
                float v = s[i] * gain;
                float a = Mathf.Abs(v);
                if (a > knee)
                {
                    // Compresión suave hacia el techo: nunca lo supera y no hay recorte duro
                    float over = (a - knee) / room;
                    a = knee + room * (float)Math.Tanh(over);
                    v = Mathf.Sign(v) * a;
                }
                s[i] = v;
            }
        }

        private static float[] TrimSilence(float[] s, int rate)
        {
            int first = 0, last = s.Length - 1;
            while (first < s.Length && Mathf.Abs(s[first]) < SilenceThreshold) first++;
            while (last > first && Mathf.Abs(s[last]) < SilenceThreshold) last--;
            if (first >= last) return s;
            int edge = Mathf.RoundToInt(EdgeSeconds * rate);
            int start = Mathf.Max(0, first - edge);
            int end = Mathf.Min(s.Length - 1, last + edge);
            var trimmed = new float[end - start + 1];
            Array.Copy(s, start, trimmed, 0, trimmed.Length);
            // Fundidos de 5 ms para que el corte no haga clic
            int fade = Mathf.Min(trimmed.Length / 4, Mathf.RoundToInt(0.005f * rate));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                trimmed[i] *= k;
                trimmed[trimmed.Length - 1 - i] *= k;
            }
            return trimmed;
        }

        /// <summary>Remuestreo lineal (suficiente para voz en esta relación 22,05 → 24 kHz).</summary>
        private static float[] Resample(float[] s, int from, int to)
        {
            int length = (int)((long)s.Length * to / from);
            var result = new float[length];
            double step = from / (double)to;
            for (int i = 0; i < length; i++)
            {
                double pos = i * step;
                int index = (int)pos;
                float frac = (float)(pos - index);
                float a = s[Mathf.Min(index, s.Length - 1)];
                float b = s[Mathf.Min(index + 1, s.Length - 1)];
                result[i] = a + (b - a) * frac;
            }
            return result;
        }

        public static float Rms(float[] s)
        {
            double sum = 0;
            foreach (float v in s) sum += v * v;
            return (float)Math.Sqrt(sum / Math.Max(1, s.Length));
        }

        public static float RmsDb(float[] s) => 20f * Mathf.Log10(Mathf.Max(1e-6f, Rms(s)));

        public static float PeakDb(float[] s)
        {
            float peak = 0f;
            foreach (float v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            return 20f * Mathf.Log10(Mathf.Max(1e-6f, peak));
        }

        // ---------- WAV PCM 16 bits mono ----------

        public static bool ReadWav(string path, out float[] samples, out int rate)
        {
            samples = null;
            rate = 0;
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF") return false;
            int channels = BitConverter.ToInt16(b, 22);
            rate = BitConverter.ToInt32(b, 24);
            int bits = BitConverter.ToInt16(b, 34);
            if (bits != 16) return false;

            int p = 12;
            while (p < b.Length - 8)
            {
                string id = Encoding.ASCII.GetString(b, p, 4);
                int len = BitConverter.ToInt32(b, p + 4);
                if (id == "data")
                {
                    int frames = len / 2 / channels;
                    samples = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        // Mezcla a mono si hiciera falta
                        float sum = 0f;
                        for (int c = 0; c < channels; c++) sum += BitConverter.ToInt16(b, p + 8 + (i * channels + c) * 2) / 32768f;
                        samples[i] = sum / channels;
                    }
                    return true;
                }
                p += 8 + len;
            }
            return false;
        }

        private static void WriteWav(string path, float[] samples, int rate)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int dataSize = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);
            foreach (float v in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
