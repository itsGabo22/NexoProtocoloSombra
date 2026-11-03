namespace Nexo.UI
{
    /// <summary>
    /// Qué debe hacer el HUD con una notificación entrante (lo decide NotificationStack).
    /// </summary>
    public enum NotificationAction
    {
        /// <summary>Crear una tarjeta nueva (y retirar la más antigua si se superó el tope).</summary>
        Create,
        /// <summary>Ya hay una tarjeta idéntica visible: reiniciar su tiempo de vida.</summary>
        Refresh,
        /// <summary>Se mostró una idéntica hace poco y ya caducó: no se crea otra.</summary>
        Suppress
    }
}
