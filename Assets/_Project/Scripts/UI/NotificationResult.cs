namespace Nexo.UI
{
    /// <summary>
    /// Resultado de NotificationStack.Push: la acción, la tarjeta afectada y la tarjeta retirada por el tope.
    /// </summary>
    public readonly struct NotificationResult
    {
        /// <summary>Valor de id cuando no hay tarjeta.</summary>
        public const int None = -1;

        public NotificationAction Action { get; }

        /// <summary>Tarjeta creada o refrescada (None si se suprimió).</summary>
        public int CardId { get; }

        /// <summary>Tarjeta más antigua que hay que retirar por superar el tope (None si no hay).</summary>
        public int EvictedId { get; }

        public NotificationResult(NotificationAction action, int cardId, int evictedId)
        {
            Action = action;
            CardId = cardId;
            EvictedId = evictedId;
        }
    }
}
