using System;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Lógica pura de la cadena de respuesta de Elena: Gemini (remoto) → respaldo JSON → respuesta de emergencia.
    /// Garantiza que SIEMPRE hay una respuesta válida y anota su origen (<see cref="ElenaResponse.source"/>).
    /// </summary>
    public static class ElenaReplyResolver
    {
        public const string SourceFallback = "fallback";
        public const string SourceEmergency = "emergency";

        private const string EmergencyDialogue = "Alex... sigue caminando hacia la esclusa, por favor.";

        /// <summary>Elige la respuesta final. <paramref name="remoteModel"/> es el modelo que respondió (para el marcador).</summary>
        public static ElenaResponse Pick(ElenaResponse remote, string remoteModel, Func<ElenaResponse> fallback, out bool usedFallback)
        {
            if (remote != null && remote.IsValid(out _))
            {
                usedFallback = false;
                remote.source = string.IsNullOrWhiteSpace(remoteModel) ? "gemini" : remoteModel.Trim();
                return remote;
            }

            usedFallback = true;
            ElenaResponse local = null;
            try
            {
                local = fallback?.Invoke();
            }
            catch (Exception)
            {
                local = null;
            }

            if (local != null && local.IsValid(out _))
            {
                local.source = SourceFallback;
                return local;
            }

            return new ElenaResponse
            {
                dialogue = EmergencyDialogue,
                emotion = "scared",
                ui_action = "none",
                trust_level = 50,
                source = SourceEmergency
            };
        }

        /// <summary>
        /// La progresión no puede depender de que Gemini acierte la orden: si la respuesta del guion para esta intención abre
        /// una puerta (p. ej. [2] en el Piso 6 → F06_Door_Stairs) y la respuesta en vivo no la abre, se conserva el diálogo de
        /// Gemini y se añade la orden del guion. ElenaUiCommandRouter la sigue validando contra available_doors.
        /// Devuelve true si añadió la orden.
        /// </summary>
        public static bool KeepScriptedUnlock(ElenaResponse live, ElenaResponse scripted)
        {
            if (live == null || scripted == null) return false;
            if (!string.Equals(scripted.ui_action, "UNLOCK_DOOR", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(scripted.target_id)) return false;
            if (string.Equals(live.ui_action, "UNLOCK_DOOR", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((live.target_id ?? string.Empty).Trim(), scripted.target_id.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            live.ui_action = scripted.ui_action;
            live.target_id = scripted.target_id;
            return true;
        }

        /// <summary>Texto del marcador de la HUD: «IA: Gemini» si responde la IA en vivo, «IA: respaldo» si no; vacío si no aplica.</summary>
        public static string MarkerText(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (source == SourceFallback || source == SourceEmergency) return "IA: respaldo";
            return "IA: Gemini";
        }
    }
}
