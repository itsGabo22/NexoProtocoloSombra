using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// "Piso reiniciado" (tema hive): REINTENTAR reanuda desde el checkpoint ya restaurado y MENÚ PRINCIPAL vuelve al hub.
    /// La abre MenuGameFlowBridge tras una captura, cuando PlayerCaptureHandler ya recargó el piso.
    /// </summary>
    public sealed class FloorRestartedController : IMenuScreen, IMenuNavigationHandler
    {
        private MenuFlowController _flow;
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
            _flow = flow;
            Label subtitle = root.Q<Label>("fr-subtitle");
            if (subtitle != null) subtitle.text = FloorLabels.Restarted(flow.CurrentFloor);

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
            if (index == Retry)
            {
                // MenuGameFlowBridge abre esta pantalla cuando el piso ya se reinició tras la captura: solo se reanuda
                UnityEngine.Time.timeScale = 1f;
                _flow?.ShowScreen(ScreenId.None);
            }
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
