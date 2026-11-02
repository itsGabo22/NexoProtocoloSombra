namespace Nexo.UI.Menus
{
    /// <summary>
    /// Receptor de las acciones que MenuNavigator traduce desde los eventos de navegación de UI Toolkit.
    /// </summary>
    public interface IMenuNavigationHandler
    {
        /// <summary>El ítem indicado pasa a tener el foco (flechas, stick o puntero).</summary>
        void OnItemFocused(int index);

        /// <summary>Confirmar (Enter, botón sur del mando o clic) sobre el ítem indicado.</summary>
        void OnItemSubmitted(int index);

        /// <summary>← (-1) o → (+1) sobre el ítem enfocado: cambiar valor.</summary>
        void OnValueStep(int index, int direction);

        /// <summary>Q (-1) o E (+1): cambiar de pestaña.</summary>
        void OnTabStep(int direction);

        /// <summary>Cancelar (Esc o botón este del mando).</summary>
        void OnCancel();
    }
}
