namespace Nexo.Narrative
{
    /// <summary>
    /// Acciones de la secuencia del giro final (BioScanner del Piso 1).
    /// </summary>
    public enum TwistAction
    {
        /// <summary>Bloquear al jugador y emitir GameEvents.OnPlotTwistTriggered.</summary>
        StartTwist,

        /// <summary>Emitir GameEvents.OnHudCorruptionRequested (HUD azul → colmena roja).</summary>
        HudCorruption,

        /// <summary>Llevar las luces de emergencia al color rojo (rampa; instantánea con reducir movimiento).</summary>
        LightRamp,

        /// <summary>Mostrar una línea final de Elena como subtítulo (TwistStep.LineIndex).</summary>
        ElenaLine,

        /// <summary>Pedir la pantalla de créditos (MenuEvents.ShowCreditsRequested).</summary>
        ShowCredits
    }
}
