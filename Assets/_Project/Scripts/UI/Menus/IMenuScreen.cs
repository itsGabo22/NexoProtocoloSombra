using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Controlador de una pantalla de menú. MenuFlowController llama a Bind tras clonar el UXML y a Unbind
    /// antes de sustituirlo; todo registro hecho en Bind se deshace en Unbind.
    /// </summary>
    public interface IMenuScreen
    {
        void Bind(MenuFlowController flow, VisualElement root, ScreenId screen);
        void Unbind();
    }
}
