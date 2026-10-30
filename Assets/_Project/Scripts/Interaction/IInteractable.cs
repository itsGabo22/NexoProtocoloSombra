namespace Nexo.Interaction
{
    /// <summary>
    /// Interfaz común para todos los objetos del mundo interactuables mediante la tecla de interacción [E].
    /// </summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact();
    }
}
