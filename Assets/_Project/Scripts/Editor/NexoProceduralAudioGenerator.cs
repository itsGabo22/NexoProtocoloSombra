using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Genera por código los efectos de sonido originales del Piso 6 y de los infectados (WAV PCM 16 bits, 44.1 kHz).
    /// Todo es síntesis propia (ruido filtrado, osciladores y reverberación Schroeder): no hay muestras de terceros.
    /// Semilla fija: volver a generar produce los mismos archivos.
    /// Menú: Nexo/Audio/Generar SFX procedurales (Piso 6 e infectados).
    /// </summary>
    public static class NexoProceduralAudioGenerator
    {
        private const int SampleRate = 44100;
        private const string SfxFolder = "Assets/_Project/Audio/SFX";
        private const string AmbienceFolder = "Assets/_Project/Audio/Ambience";

        [MenuItem("Nexo/Audio/Generar SFX procedurales (Piso 6 e infectados)")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(SfxFolder);
            Directory.CreateDirectory(AmbienceFolder);

            for (int i = 0; i < 3; i++) WriteMono($"{SfxFolder}/SFX_Infected_Step_0{i + 1}.wav", InfectedStep(100 + i));
            WriteMono($"{SfxFolder}/SFX_Infected_HitFlesh.wav", FleshHit(200));
            WriteMono($"{SfxFolder}/SFX_Infected_Pain.wav", PainGroan(210));
            WriteStereo($"{SfxFolder}/SFX_F06_Thunder_01.wav", Thunder(300, 7.5f, 0.0f));
            WriteStereo($"{SfxFolder}/SFX_F06_Thunder_02.wav", Thunder(301, 9f, 0.35f));
            WriteMono($"{SfxFolder}/SFX_F06_DoorKnocks.wav", DoorKnocks(400));
            WriteMono($"{SfxFolder}/SFX_F06_ObjectFall.wav", ObjectFall(500));
            WriteMono($"{AmbienceFolder}/SFX_F06_Breathing_Loop.wav", Breathing(600));
            WriteMono($"{AmbienceFolder}/SFX_F06_ElectricHum_Loop.wav", ElectricHum(700));
            WriteMono($"{AmbienceFolder}/SFX_F06_AlarmDistant_Loop.wav", DistantAlarm(800));
            WriteMono($"{AmbienceFolder}/SFX_F06_LabRoomTone_Loop.wav", RoomTone(900));

            AssetDatabase.Refresh();
            Debug.Log("[NexoProceduralAudioGenerator] SFX generados en " + SfxFolder + " y " + AmbienceFolder);
        }

        // ---------------- Sonidos ----------------

        /// <summary>Pisada descalza y pesada: golpe grave + roce de alta frecuencia.</summary>
        private static float[] InfectedStep(int seed)
        {
            var rng = new Random(seed);
            float[] s = new float[(int)(0.32f * SampleRate)];
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t) * Mathf.Exp(-t * 28f);
                float n = Noise(rng);
                lp += (n - lp) * 0.18f;
                float scuff = lp * Mathf.Exp(-t * 18f) * 0.6f;
                hp = 0.92f * (hp + n - prev); prev = n;
                float grit = hp * Mathf.Exp(-t * 60f) * 0.15f;
                s[i] = thud * 0.9f + scuff + grit;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>Impacto de bala en carne: chasquido seco + cuerpo húmedo grave.</summary>
        private static float[] FleshHit(int seed)
        {
            var rng = new Random(seed);
            float[] s = new float[(int)(0.4f * SampleRate)];
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                lp += (n - lp) * 0.08f;
                float crack = n * Mathf.Exp(-t * 90f) * 0.5f;
                float body = lp * Mathf.Exp(-t * 14f) * 2.2f;
                float low = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 20f) * 0.6f;
                s[i] = crack + body + low;
            }
            return Normalize(s, 0.85f);
        }

        /// <summary>Gruñido de dolor: oscilador con vibrato filtrado por formantes y ruido de garganta.</summary>
        private static float[] PainGroan(int seed)
        {
            var rng = new Random(seed);
            float dur = 0.9f;
            float[] s = new float[(int)(dur * SampleRate)];
            float phase = 0f, lp = 0f, bp1 = 0f, bp1v = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f0 = 95f + 25f * Mathf.Sin(t * 3.1f) - 30f * t + (float)rng.NextDouble() * 6f;
                phase += f0 / SampleRate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float n = Noise(rng) * 0.35f;
                float src = saw * 0.7f + n;
                // Formante ~500 Hz (resonador de dos polos simple)
                float w = 2f * Mathf.PI * 520f / SampleRate;
                bp1v += (src - bp1) * w - bp1v * 0.06f;
                bp1 += bp1v * w;
                lp += (src - lp) * 0.12f;
                float env = Mathf.Clamp01(t * 12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 4f);
                s[i] = (bp1 * 1.2f + lp * 0.5f) * env;
            }
            return Normalize(s, 0.75f);
        }

        /// <summary>
        /// Trueno cinematográfico en tres capas: chasquido inicial, cuerpo grave con retumbos y cola reverberada.
        /// Se normaliza con margen (pico -1.5 dBFS) y sin saturación: el peso viene del grave, no del volumen.
        /// </summary>
        private static float[][] Thunder(int seed, float duration, float delayCrack)
        {
            var rng = new Random(seed);
            int n = (int)(duration * SampleRate);
            float[] dry = new float[n];
            float lp1 = 0f, lp2 = 0f, lp3 = 0f, crackHp = 0f, crackPrev = 0f;

            // Retumbos aleatorios del cuerpo (picos de energía que decaen)
            int rumbles = 5 + rng.Next(3);
            float[] rumbleTimes = new float[rumbles];
            float[] rumbleAmps = new float[rumbles];
            for (int r = 0; r < rumbles; r++)
            {
                rumbleTimes[r] = delayCrack + 0.15f + (float)rng.NextDouble() * (duration * 0.45f);
                rumbleAmps[r] = 0.4f + (float)rng.NextDouble() * 0.6f;
            }

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = Noise(rng);

                // Capa 1: chasquido (ruido agudo, ataque instantáneo)
                float ct = t - delayCrack;
                float crack = 0f;
                if (ct >= 0f)
                {
                    crackHp = 0.85f * (crackHp + noise - crackPrev);
                    crackPrev = noise;
                    crack = crackHp * Mathf.Exp(-ct * 9f) * 0.55f;
                }

                // Capa 2: cuerpo grave (ruido marrón filtrado tres veces)
                lp1 += (noise - lp1) * 0.02f;
                lp2 += (lp1 - lp2) * 0.03f;
                lp3 += (lp2 - lp3) * 0.05f;
                float envBody = 0f;
                for (int r = 0; r < rumbles; r++)
                {
                    float dt = t - rumbleTimes[r];
                    if (dt > 0f) envBody += rumbleAmps[r] * (1f - Mathf.Exp(-dt * 6f)) * Mathf.Exp(-dt * 0.9f);
                }
                float slowSwell = Mathf.Clamp01((t - delayCrack) * 3f) * Mathf.Exp(-Mathf.Max(0f, t - delayCrack) * 0.45f);
                float body = lp3 * 26f * (0.35f * slowSwell + envBody);

                // Sub-grave que da el "impacto"
                float sub = Mathf.Sin(2f * Mathf.PI * (38f + 6f * Mathf.Sin(t * 1.3f)) * t) * slowSwell * 0.25f;

                dry[i] = crack + body + sub;
            }

            float[] left = SchroederReverb(dry, 0.84f, 0.35f, 0);
            float[] right = SchroederReverb(dry, 0.84f, 0.35f, 7);
            for (int i = 0; i < n; i++)
            {
                left[i] = dry[i] * 0.75f + left[i];
                right[i] = dry[i] * 0.75f + right[i];
            }
            FadeOut(left, 0.8f);
            FadeOut(right, 0.8f);
            float peak = Mathf.Max(Peak(left), Peak(right));
            float gain = peak > 0f ? 0.84f / peak : 1f;
            for (int i = 0; i < n; i++) { left[i] *= gain; right[i] *= gain; }
            return new[] { left, right };
        }

        /// <summary>Golpes pesados contra una puerta metálica al otro lado (cuatro golpes irregulares con eco).</summary>
        private static float[] DoorKnocks(int seed)
        {
            var rng = new Random(seed);
            float[] knockTimes = { 0.05f, 0.62f, 1.05f, 1.9f };
            int n = (int)(3.2f * SampleRate);
            float[] dry = new float[n];
            foreach (float kt in knockTimes)
            {
                float amp = 0.7f + (float)rng.NextDouble() * 0.3f;
                int start = (int)(kt * SampleRate);
                float lp = 0f;
                for (int i = 0; i < (int)(0.5f * SampleRate) && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float noise = Noise(rng);
                    lp += (noise - lp) * 0.1f;
                    float thump = Mathf.Sin(2f * Mathf.PI * 82f * t) * Mathf.Exp(-t * 16f);
                    float metal = (Mathf.Sin(2f * Mathf.PI * 410f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 655f * t) * 0.2f) * Mathf.Exp(-t * 9f);
                    dry[start + i] += amp * (thump + metal * 0.5f + lp * Mathf.Exp(-t * 30f) * 1.5f);
                }
            }
            float[] wet = SchroederReverb(dry, 0.7f, 0.3f, 3);
            for (int i = 0; i < n; i++) dry[i] = dry[i] * 0.8f + wet[i] * 0.6f;
            FadeOut(dry, 0.3f);
            return Normalize(dry, 0.85f);
        }

        /// <summary>Objeto metálico (bandeja/instrumental) cayendo y rebotando en el suelo del laboratorio.</summary>
        private static float[] ObjectFall(int seed)
        {
            var rng = new Random(seed);
            int n = (int)(2.4f * SampleRate);
            float[] dry = new float[n];
            float[] bounceTimes = { 0f, 0.28f, 0.47f, 0.6f, 0.69f, 0.75f };
            float[] partials = { 523f, 1187f, 1810f, 2630f, 3400f };
            for (int b = 0; b < bounceTimes.Length; b++)
            {
                float amp = Mathf.Pow(0.62f, b);
                int start = (int)(bounceTimes[b] * SampleRate);
                for (int i = 0; i < (int)(1.2f * SampleRate) && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float ring = 0f;
                    for (int p = 0; p < partials.Length; p++)
                    {
                        float detune = 1f + (b * 0.004f) * (p + 1);
                        ring += Mathf.Sin(2f * Mathf.PI * partials[p] * detune * t) * Mathf.Exp(-t * (4f + p * 3f)) / (p + 1);
                    }
                    float click = Noise(rng) * Mathf.Exp(-t * 400f);
                    dry[start + i] += amp * (ring * 0.6f + click * 0.8f);
                }
            }
            float[] wet = SchroederReverb(dry, 0.72f, 0.4f, 5);
            for (int i = 0; i < n; i++) dry[i] = dry[i] * 0.85f + wet[i] * 0.5f;
            FadeOut(dry, 0.4f);
            return Normalize(dry, 0.8f);
        }

        /// <summary>Respiración lenta y entrecortada en la oscuridad (bucle de 8 s sin clic en el punto de unión).</summary>
        private static float[] Breathing(int seed)
        {
            var rng = new Random(seed);
            int n = 8 * SampleRate;
            float[] s = new float[n];
            float bp = 0f, bpv = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float cycle = (t % 4f) / 4f; // inspiración 0-0.4, espiración 0.45-0.9
                float inhale = cycle < 0.4f ? Mathf.Sin(cycle / 0.4f * Mathf.PI) : 0f;
                float exhale = cycle > 0.45f && cycle < 0.9f ? Mathf.Sin((cycle - 0.45f) / 0.45f * Mathf.PI) : 0f;
                float rattle = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 23f * t) * exhale;
                float noise = Noise(rng);
                float w = 2f * Mathf.PI * (inhale > 0f ? 1300f : 700f) / SampleRate;
                bpv += (noise - bp) * w - bpv * 0.2f;
                bp += bpv * w;
                lp += (noise - lp) * 0.05f;
                s[i] = (bp * 0.6f + lp * 0.9f) * (inhale * 0.55f + exhale * rattle);
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>Zumbido eléctrico de reactancias de fluorescente (bucle de 4 s, 100 Hz + armónicos y chisporroteo).</summary>
        private static float[] ElectricHum(int seed)
        {
            var rng = new Random(seed);
            int n = 4 * SampleRate;
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f
                          + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.25f
                          + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.12f
                          + Mathf.Sin(2f * Mathf.PI * 1000f * t) * 0.03f;
                float crackle = rng.NextDouble() < 0.0004 ? Noise(rng) * 2f : 0f;
                s[i] = hum * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)) + crackle;
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>Alarma lejana de dos tonos tras varias paredes (paso bajo + reverberación; bucle de 4 s).</summary>
        private static float[] DistantAlarm(int seed)
        {
            int n = 4 * SampleRate;
            float[] dry = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float freq = (t % 1f) < 0.5f ? 740f : 587f;
                phase += freq / SampleRate;
                float sq = Mathf.Sin(2f * Mathf.PI * phase) + Mathf.Sin(6f * Mathf.PI * phase) / 3f;
                dry[i] = sq * 0.5f;
            }
            float lp = 0f;
            for (int i = 0; i < n; i++) { lp += (dry[i] - lp) * 0.06f; dry[i] = lp; }
            float[] wet = SchroederReverb(dry, 0.8f, 0.5f, 9);
            for (int i = 0; i < n; i++) dry[i] = dry[i] * 0.35f + wet[i];
            return Normalize(Crossfade(dry, (int)(0.25f * SampleRate)), 0.5f);
        }

        /// <summary>Tono de sala de laboratorio abandonado: ventilación grave y sutil (bucle de 10 s).</summary>
        private static float[] RoomTone(int seed)
        {
            var rng = new Random(seed);
            int n = 10 * SampleRate;
            float[] s = new float[n];
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = Noise(rng);
                lp1 += (noise - lp1) * 0.03f;
                lp2 += (lp1 - lp2) * 0.05f;
                s[i] = lp2 * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.07f * t)) + Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.02f;
            }
            return Normalize(Crossfade(s, SampleRate), 0.45f);
        }

        // ---------------- DSP ----------------

        private static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        /// <summary>Reverberación Schroeder: 4 filtros peine en paralelo + 2 pasa-todo en serie.</summary>
        private static float[] SchroederReverb(float[] input, float feedback, float mix, int stereoOffset)
        {
            int[] combDelays = { 1557 + stereoOffset * 23, 1617 + stereoOffset * 23, 1491 + stereoOffset * 23, 1422 + stereoOffset * 23 };
            int[] allpassDelays = { 225 + stereoOffset, 556 + stereoOffset };
            float[] output = new float[input.Length];

            foreach (int delay in combDelays)
            {
                float[] buffer = new float[delay];
                int index = 0;
                float damp = 0f;
                for (int i = 0; i < input.Length; i++)
                {
                    float y = buffer[index];
                    damp = y * 0.6f + damp * 0.4f;
                    buffer[index] = input[i] + damp * feedback;
                    output[i] += y * 0.25f;
                    index = (index + 1) % delay;
                }
            }

            foreach (int delay in allpassDelays)
            {
                float[] buffer = new float[delay];
                int index = 0;
                for (int i = 0; i < output.Length; i++)
                {
                    float buffered = buffer[index];
                    float y = -output[i] + buffered;
                    buffer[index] = output[i] + buffered * 0.5f;
                    output[i] = y;
                    index = (index + 1) % delay;
                }
            }

            for (int i = 0; i < output.Length; i++) output[i] *= mix;
            return output;
        }

        private static float Peak(float[] s)
        {
            float peak = 0f;
            foreach (float v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            return peak;
        }

        private static float[] Normalize(float[] s, float target)
        {
            float peak = Peak(s);
            if (peak <= 0f) return s;
            float gain = target / peak;
            for (int i = 0; i < s.Length; i++) s[i] *= gain;
            return s;
        }

        private static void FadeOut(float[] s, float seconds)
        {
            int len = Mathf.Min(s.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < len; i++) s[s.Length - 1 - i] *= i / (float)len;
        }

        /// <summary>
        /// Funde la cola sobre el principio y la recorta: el último frame enlaza con el primero sin clic.
        /// </summary>
        private static float[] Crossfade(float[] s, int length)
        {
            length = Mathf.Min(length, s.Length / 2);
            int loopLength = s.Length - length;
            for (int i = 0; i < length; i++)
            {
                float k = i / (float)length;
                s[i] = s[i] * k + s[loopLength + i] * (1f - k);
            }
            Array.Resize(ref s, loopLength);
            return s;
        }

        // ---------------- WAV ----------------

        private static void WriteMono(string path, float[] samples) => WriteWav(path, new[] { samples });

        private static void WriteStereo(string path, float[][] channels) => WriteWav(path, channels);

        private static void WriteWav(string path, float[][] channels)
        {
            int channelCount = channels.Length;
            int frames = channels[0].Length;
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            int dataSize = frames * channelCount * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channelCount);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channelCount * 2);
            writer.Write((short)(channelCount * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
            for (int i = 0; i < frames; i++)
            {
                for (int c = 0; c < channelCount; c++)
                {
                    float v = Mathf.Clamp(channels[c][i], -1f, 1f);
                    writer.Write((short)(v * short.MaxValue));
                }
            }
        }
    }
}
