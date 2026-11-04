using System;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Una cámara de la red de seguridad NexoOS. Define su piso, su encuadre y, para cuando su piso no está cargado,
    /// el último fotograma archivado (lo genera la herramienta Nexo/Cámaras/Capturar archivo de la red).
    /// Si SceneCameraName coincide con una cámara existente del piso cargado, se reutiliza esa cámara.
    /// </summary>
    [Serializable]
    public sealed class CameraFeedDefinition
    {
        [Tooltip("Identificador visible, p. ej. F06-02.")]
        public string Id = "F12-01";
        [Tooltip("Nombre de la zona, p. ej. LABORATORIO B.")]
        public string Label = "PASILLO";
        public int Floor = 12;
        [Tooltip("RoomId de LightSwitchInteractable para saber si la sala está a oscuras (opcional).")]
        public string RoomId = "";

        [Header("Encuadre")]
        [Tooltip("Cámara ya existente en la escena del piso que se reutiliza (opcional).")]
        public string SceneCameraName = "";
        public Vector3 Position;
        public Vector3 EulerAngles;
        [Range(30f, 100f)] public float FieldOfView = 70f;

        [Header("Archivo (piso no cargado)")]
        public Texture2D ArchiveFrame;
        [Tooltip("Hora mostrada como última sincronía del archivo (HH:MM:SS).")]
        public string ArchiveTimestamp = "";
    }
}
