using UnityEditor;
using UnityEditor.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Hace que el botón Play arranque siempre desde Systems_Persistent (jugador, HUD y Elena),
    /// aunque la escena abierta en el Editor sea Floor12_Offices. SceneLoader carga después el piso de forma aditiva.
    /// Se desactiva desde el menú Nexo/Play Desde Systems_Persistent.
    /// </summary>
    [InitializeOnLoad]
    public static class NexoPlayModeBootstrap
    {
        private const string ScenePath = "Assets/_Project/Scenes/Persistent/Systems_Persistent.unity";
        private const string MenuPath = "Nexo/Play Desde Systems_Persistent";
        private const string PrefKey = "Nexo.PlayFromPersistent";

        static NexoPlayModeBootstrap()
        {
            // Diferido: el AssetDatabase puede no estar listo durante la recarga de dominio
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        /// <summary>
        /// Asigna (o limpia) la escena de arranque del Play Mode. Se llama también desde el wizard tras crear la escena.
        /// </summary>
        public static void Apply()
        {
            SceneAsset scene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
            EditorSceneManager.playModeStartScene = scene;
        }
    }
}
