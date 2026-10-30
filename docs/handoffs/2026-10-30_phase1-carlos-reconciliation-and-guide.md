# Handoff: Reconciliación de Estructura de Proyecto y Guía Fase 1 para Carlos

**Fecha:** 30 de octubre de 2026  
**De:** Gabriel (Arquitectura C#, UI Toolkit, Git)  
**Para:** Carlos (Unity Editor, Diseño de Nivel, Arte, Animación)  
**Proyecto:** NEXO: PROTOCOLO SOMBRA  

---

## 1. Resumen de la Reconciliación (Tus assets están 100% a salvo)

¡Hola Carlos! Hiciste un gran trabajo integrando el modelo de Mixamo, el arma M1911 y armando el prototipo del Piso 12.

Al revisar el push remoto notamos dos detalles técnicos que solucionamos quirúrgicamente para que no pierdas nada de tu trabajo:
1. **Subcarpeta anidada `My project/`:** Al abrir o crear el proyecto desde Unity Hub, se creó una carpeta anidada llamada `My project` dentro de la raíz del repositorio con su propio `Assets/` y `ProjectSettings/`.
2. **Uso de Git LFS y paquetes pesados:** Se subieron 545 MB de archivos directamente al historial de Git sin Git LFS.

### ¿Qué hicimos para solucionarlo?
Reubicamos todos tus archivos a las rutas canónicas del proyecto en la raíz **preservando el 100% de los GUIDs de sus `.meta`**:
- **Tu escena:** Ahora está en `Assets/_Project/Scenes/Floors/Floor12_Offices.unity` (mismo GUID, tus referencias no se rompieron).
- **Tu personaje Mixamo:** En `Assets/_Project/Models/Characters/Ch33_nonPBR1.fbx`.
- **Tu animación de retroceso:** En `Assets/_Project/Animations/Clips/Ch33_nonPBR@Pistol Walk Backward.fbx`.
- **Tu Animator Controller:** En `Assets/_Project/Animations/Controllers/AC_Alex.controller`.
- **Tus scripts (`FPSController` y `WeaponMotion`):** Se adaptaron al New Input System y sus GUIDs siguen intactos en `Assets/_Project/Scripts/Player/Legacy/` para que tu escena no muestre "Missing (Mono Script)".
- **Packs de terceros pesados (`Nokobot` y `Office building`):** Se movieron a `Assets/_ThirdPartyAssets/` para sincronizarlos limpiamente por Drive sin saturar la cuota de Git.

---

## 2. Paso OBLIGATORIO en tu equipo antes de abrir Unity

Para que ambos estemos en sincronía perfecta y no haya conflictos futuros:

1. **Abre tu terminal en la carpeta del repositorio y ejecuta:**
   ```bash
   git lfs install
   git pull origin develop
   ```
2. **En Unity Hub:**
   - Si tenías añadido `My project`, elimínalo de la lista de proyectos en Unity Hub.
   - Haz clic en **Add project from disk** (Añadir proyecto desde el disco).
   - Selecciona la carpeta raíz del repositorio: `NexoProtocoloSombra` (donde están `Assets`, `Packages`, `ProjectSettings`).
   - Abre el proyecto usando **Unity 6 LTS (6000.0.0f1)** para evitar discrepancias de versión con `6000.6.7f1`.

---

## 3. Arquitectura de Cámara en Primera Persona (Por qué desacoplamos `mixamorig7:Head`)

Me pediste emparentar la cámara a `mixamorig7:Head`. En videojuegos en primera persona en Unity, emparentar la cámara directamente al hueso animado de la cabeza genera tres problemas críticos:
1. **Cinetosis extrema:** Las animaciones de Mixamo tienen balanceo y cabeceo cocinado en la cabeza, lo que hace que la pantalla se sacuda violentamente al caminar.
2. **Conflicto con el ratón:** El componente `Animator` sobreescribe la rotación de los huesos cada fotograma, peleando contra la rotación del ratón (*mouse look*).
3. **Clipping:** La cámara queda dentro del cráneo y recorta ojos y rostro.

### Solución aplicada en `PlayerMovement.cs`:
- La cámara se emparenta a un `CameraHolder` estable en el GameObject raíz a la altura de los ojos.
- En `Awake()`, el script escala automáticamente el hueso de la cabeza (`mixamorig7:Head`) a `(0.001, 0.001, 0.001)`: esto hace que la cabeza no tape la cámara ni genere clipping, mientras que **los brazos, torso y la pistola M1911 en la mano derecha siguen perfectamente visibles y animados**.

---

## 4. Tu Lista de Tareas para la Fase 1 (30 Oct – 01 Nov)

Siguiendo la Matriz de Propiedad de la Biblia Técnica, tú eres el dueño absoluto de `Scenes/Floors/`, `Models/`, `Materials/` y `Animations/`:

- [ ] **Decoración del Piso 12 (`Floor12_Offices.unity`):** Distribuye los escritorios, mamparas, pasillos y computadores del pack de oficinas. Recuerda dejar el centro de la escena limpio y colocar waypoints bajo los escritorios.
- [ ] **Crear `AC_SmartInfected`:** En `Assets/_Project/Animations/Controllers/`, crea el Animator Controller del infectado con los siguientes parámetros exactos:
  - `Speed` (Float)
  - `IsAlerted` (Bool)
  - `AttackTrigger` (Trigger)
  - `SabotageTrigger` (Trigger)
  - `ScreamTrigger` (Trigger)
- [ ] **Crear `AC_Elena`:** Animator Controller para Elena (estados `Idle`, `Cower`, `Follow`).
- [ ] **Crear `MAT_Glass_Armored`:** Material para el cristal blindado de la esclusa del Piso 12 en `Assets/_Project/Materials/`.
- [ ] **Waypoints de patrulla e interruptores:** Coloca instancias de prefabs para los interruptores (`LightSwitchInteractable`) y define los cuartos (`F12_Office_Entrance`, `F12_Hallway_North`).

¡Cualquier duda me avisas por chat o me pides un script por handoff!
