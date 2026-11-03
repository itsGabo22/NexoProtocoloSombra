namespace Nexo.UI
{
    /// <summary>
    /// Estado visual del panel de arma del HUD (lo decide AmmoDisplayState).
    /// </summary>
    public enum AmmoStatus
    {
        /// <summary>Cargador con munición suficiente.</summary>
        Normal,
        /// <summary>Quedan pocas balas en el cargador (color de aviso).</summary>
        Low,
        /// <summary>Cargador vacío pero hay reserva: hay que recargar.</summary>
        Empty,
        /// <summary>Cargador y reserva vacíos.</summary>
        Depleted,
        /// <summary>Recarga en curso.</summary>
        Reloading
    }
}
