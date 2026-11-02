using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// "Piso reiniciado" (tema hive): REINTENTAR pide reiniciar el piso y MENÚ PRINCIPAL vuelve al hub.
    /// Se muestra con MenuEvents.RaiseShowFloorRestartedRequested() (ver el comentario en MenuEvents).
    /// </summary>
    public sealed class FloorRestartedController : IMenuScreen, IMenuNavigationHandler
    {
        private const int Retry = 0;
        private const int MainMenu = 1;

        private readonly MenuNavigator _navigator;
        private readonly VisualElement[] _items = new VisualElement[2];

        public FloorRestartedController()
        {
            _navigator = new MenuNavigator(this);
        }

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _items[Retry] = root.Q<VisualElement>("fr-retry");
            _items[MainMenu] = root.Q<VisualElement>("fr-main-menu");
            _navigator.Attach(root, _items, Retry);
        }

        public void Unbind()
        {
            _navigator.Detach();
        }

        public void OnItemFocused(int index)
        {
        }

        public void OnItemSubmitted(int index)
        {
            if (index == Retry) MenuEvents.RaiseRestartFloorRequested();
            else if (index == MainMenu) MenuEvents.RaiseReturnToMainMenuRequested();
        }

        public void OnValueStep(int index, int direction)
        {
        }

        public void OnTabStep(int direction)
        {
        }

        public void OnCancel()
        {
        }
    }
}
