namespace Nexo.Player
{
    /// <summary>Resultado de una captura de voz.</summary>
    public enum VoiceOutcome
    {
        Ok,
        TooShort,
        Silent,
        NotUnderstood
    }

    /// <summary>Reglas puras de la entrada de voz [V] (testeadas en EditMode).</summary>
    public static class ElenaVoiceRules
    {
        public const int MaxRecordSeconds = 8;
        public const string Listening = "ESCUCHANDO... (suelta [V] para enviar)";
        public const string Processing = "PROCESANDO...";
        public const string NotHeard = "No te escuché, repite o usa 1/2/3/T";
        public const string NoMic = "No hay micrófono: usa 1/2/3/T";

        private const float SilenceThreshold = 0.01f;

        /// <summary>Clasifica la captura antes de gastar red: muy corta, silenciosa o con audio utilizable.</summary>
        public static VoiceOutcome ClassifyCapture(float[] samples, int count, int sampleRate)
        {
            if (samples == null || count < sampleRate / 3) return VoiceOutcome.TooShort;
            int n = count < samples.Length ? count : samples.Length;
            for (int i = 0; i < n; i++)
            {
                float v = samples[i];
                if (v > SilenceThreshold || v < -SilenceThreshold) return VoiceOutcome.Ok;
            }
            return VoiceOutcome.Silent;
        }

        /// <summary>Resultado tras transcribir: texto vacío o nulo = no se entendió.</summary>
        public static VoiceOutcome ClassifyTranscript(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? VoiceOutcome.NotUnderstood : VoiceOutcome.Ok;
        }
    }
}
