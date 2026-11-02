using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Créditos (tema hive): sin ítems enfocables; Esc vuelve al menú principal.
    /// </summary>
    public sealed class CreditsController : IMenuScreen, IMenuNavigationHandler
    {
        private static readonly VisualElement[] NoItems = new VisualElement[0];

        private readonly MenuNavigator _navigator;
        private MenuFlowController _flow;

        public CreditsController()
        {
            _navigator = new MenuNavigator(this);
        }

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _flow = flow;
            _navigator.Attach(root, NoItems, -1);
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
        }

        public void OnValueStep(int index, int direction)
        {
        }

        public void OnTabStep(int direction)
        {
        }

        public void OnCancel()
        {
            _flow.ShowScreen(ScreenId.MainMenu);
        }
    }
}
