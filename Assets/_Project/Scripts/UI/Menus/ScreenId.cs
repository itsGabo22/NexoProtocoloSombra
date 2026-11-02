namespace Nexo.UI.Menus
{
    /// <summary>
    /// Pantallas de menú NexoOS (docs/ui/NEXOOS_DESIGN_SPEC.md §6). None = ningún menú visible (en juego).
    /// </summary>
    public enum ScreenId
    {
        None,
        BootAwakening,
        BootBiometricScan,
        OpticalCalibration,
        MainMenu,
        OptionsVideo,
        OptionsAccessibility,
        Pause,
        FloorRestarted,
        Credits
    }
}
