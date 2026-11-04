# Handoff: develop verificado y listo para las cinemáticas

- **Fecha:** 2026-11-03
- **De:** Gabriel (agente de código)
- **Para:** Gabriel / Carlos
- **Prioridad:** Alta (entrega 2026-11-04)
- **Rama:** `feature/cinematics-ready` (desde `origin/develop` c80dfd7)
- **Archivo o prefab afectado:** ninguna escena ni prefab. Código en `Scripts/Cinematics/**`, ganchos mínimos en `MenuGameFlowBridge`, `FloorProgression`, `PlotTwistManager`; `Packages/manifest.json` + lock (solo el módulo integrado Video).

## Necesidad

Que en `develop` lo único pendiente para tener las tres cinemáticas (intro, transición de piso, final) sea copiar los `.mp4` en `Assets/StreamingAssets/Cinematics/`, y que todo lo entregado por Carlos en el PR #9 quede verificado.

## Contrato

- **API:** `Nexo.Cinematics.CinematicService.Play(string id, Action onFinished)`. Se crea solo (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`).
- **Clip:** `Application.streamingAssetsPath/Cinematics/<id>.mp4`. Subtítulos opcionales `<id>.captions.json` (array `{start, end, speaker, text}`) que pasan por `NarrativeSubtitles`.
- **Eventos:** `GameEvents.OnCinematicStarted(string id)` / `OnCinematicFinished(string id)`.
- **Ids:** `intro` (JUGAR), `floor_transition` o `floor_transition_to_06` / `_to_01` (LoadNext, también con F5/F8), `ending` (paso ShowCredits del giro, antes de los créditos).
- **Nunca bloquea:** sin clip, con id inválido, clip corrupto, sin módulo Video o si tarda más de 5 s en prepararse, `onFinished` se llama y todo se restaura.

### Decisiones

- **Render:** VideoPlayer → RenderTexture → `VisualElement` de un `UIDocument` propio (`PS_Nexo`, sortingOrder 1000), con `background-size: contain` sobre negro (letterbox 16:9). Se descartó `CameraNearPlane` porque en Built-in los paneles de UI Toolkit se dibujan después de las cámaras: HUD y menús taparían el vídeo.
- **Pausa:** se reutiliza la del juego (`Time.timeScale = 0`, igual que el menú de pausa): IA (ticks con `WaitForSeconds`), NavMesh, forcejeo, micrófono y física se congelan. El VideoPlayer usa `UnscaledGameTime` y su `AudioSource` ignora `AudioListener.pause`, que silencia el resto del audio. Verificado: el vídeo avanza con timeScale 0 y los infectados no se mueven (0,000 m).
- **Transición:** el piso nuevo se carga **después** del clip, para que su Momento 0 (subtítulos, Elena, objetivo) no ocurra tras el vídeo.
- **Salto:** Esc / Espacio / Intro / clic, aceptado a partir de 0,5 s; el aviso «Esc / Espacio: saltar» aparece al segundo. El salto termina un fotograma después de la pulsación (Espacio no salta ni dispara al devolver el control) y Esc no abre la pausa.
- **UI modal activa al empezar:** terminal, red de cámaras (libera su RT), rueda de órdenes y forcejeo se cierran; el HUD se oculta y recupera su display; con el jugador escondido, [E] queda desactivado durante el clip.
- **Módulo Video:** el proyecto no lo tenía. `Nexo.asmdef` define `NEXO_VIDEO` solo si `com.unity.modules.video` está en el manifest: sin él todo compila y `Play()` continúa al instante.

## Qué se verificó (y cómo)

| Área | Cómo | Resultado |
| --- | --- | --- |
| Git y archivos de la regresión | `git`, `grep` en YAML | OK: `StruggleUI`/`CameraNetworkUI` viven en `PF_Player` (instanciado en `Systems_Persistent`); `FirstPersonFeel`, `DemoShortcuts` (`EnableDemoShortcuts: 0`), `PlayerWeapon`; `ElenaConfig` con 3 fallbacks y `_forceFallback: 0`; `PF_ElenaService`. `PlayerHiding` es una clase estática (no un componente). |
| Compilación | batchmode sin y con módulo Video | 0 errores; solo el aviso UAC1001 del `Door.cs` del pack de oficinas |
| EditMode | `-runTests` | **190/190** (151 previos + 39 nuevos), sin y con módulo Video |
| Integridad (k, i) | `Nexo/Checks/Integrity` (4 escenas, 1855 objetos, 8 prefabs, 70 materiales) | 0 scripts que faltan, 0 shaders URP/rosa, 0 materiales vacíos, 0 AudioClip rotos. Única referencia rota: `ElenaAIService._secrets` (esperada: `ElenaSecrets.asset` no se versiona) |
| Recorrido completo (b-j) | `Nexo/Checks/Playthrough` en batchmode con gráficos | **196/196 con clips**, **106/106 sin clips**, 0 errores/excepciones en consola |
| Build Windows | `NexoReleaseBuilder.Build` a una carpeta aparte | Succeeded, 0 errores, 249 MB; los clips se copian a `NexoProtocoloSombra_Data/StreamingAssets/Cinematics/`. Carpeta de prueba borrada y `ElenaConfig.asset` restaurado con `git restore` |

Lo que comprueba el recorrido (extracto del log): arranque → menú en 8,5 s; JUGAR → intro (timeScale 0, audio en pausa, jugador bloqueado, HUD oculto, 2 infectados quietos, píxel de la RT = color del clip, aviso a 1 s, salto rechazado antes de 0,5 s y aceptado después, estado restaurado exactamente); Piso 12 (1 jugador, 1 HUD, spawn, 2/2 infectados en NavMesh, viewmodel activo, munición 2/0); forcejeo cancelado por la cinemática sin golpe; clip corrupto termina al instante; 12 → 6 con `floor_transition` (clip sin audio, carga después del clip, termina sola a los 3,00 s); Piso 6 (6/6 en NavMesh, 6 notas, 3 escondites, diferencial, 2 reptantes, `F06_Door_Stairs` bloqueada al llegar, terminal + red de cámaras cerradas por la cinemática, [2] de Elena en fallback la abre); 6 → 1 escondido con `floor_transition_to_01` temporal (se elige el específico, [E] no saca, al descargar se libera el escondite y el control); Piso 1 (3/3 en NavMesh, camino de 18 m al BioScanner, giro una sola vez, hijos de `F01_Ambience` apagados, 0 infectados activos, `ending` a los 17,5 s y créditos solo después, bloque CINEMÁTICAS); menú → JUGAR sin fugas (TwistHasPlayed limpio, 1 jugador/HUD/menú/servicio, 2/0, índice 0, subtítulos de `intro.captions.json` sobre el vídeo); atajos F7 (7/35), F6, F5 (con transición), F8 (1,6 m del BioScanner). Ningún texto con ids técnicos ni con «INFECTADO» antes del giro; 0 notificaciones duplicadas.

## Defectos encontrados y arreglados

| Archivo | Cambio |
| --- | --- |
| `UI/DoorDisplayNames.cs` (+ tests) | `F06_Door_Lab` → «Puerta de Contención» / «la sala de contención» / «de Contención» |
| `Data/ElenaFallback/fallback_floor06.json` | SAFE_PATH decía «El laboratorio está vacío» señalando esa puerta → «La sala de contención está vacía» |
| `Player/PlayerWeapon.cs` (Carlos, comentado) | Quitada la notificación duplicada «M1911 · Cargador x/7» tras recargar y recoger (el panel y el «+N» ya lo muestran) |
| `Player/StruggleQTE.cs` (Carlos, comentado) | Una cinemática cancela el forcejeo (Espacio para saltar contaría como empujón fallido y golpe) |
| `Interaction/HidingSpot.cs` (Carlos, comentado) | [E] desactivado durante una cinemática |
| `UI/TerminalController.cs`, `CameraNetworkController.cs`, `OrderWheelController.cs`, `HUDController.cs`, `Menus/PauseMenuController.cs` | Cierre/ocultación al empezar una cinemática; la rueda y la pausa no se abren durante el clip |

Notas sin cambio: `F01_Expansion` no se apaga en el giro y es correcto (es geometría: muros de sustitución; su contenido vivo cuelga de `F01_Ambience`). `docs/DEMO_SCRIPT.md` «Limitaciones conocidas» está desfasado (el pack del zombi ya está y los pisos 6 y 1 tienen enemigos): no se tocó por alcance.

## Cómo añadir los vídeos

1. Copiar en `Assets/StreamingAssets/Cinematics/`: `intro.mp4`, `floor_transition.mp4`, `ending.mp4` (opcionales `floor_transition_to_06.mp4`, `floor_transition_to_01.mp4`). MP4 H.264 + AAC (audio opcional), 1920x1080 o 1280x720, 24-30 fps, 4-8 s. La carpeta está en `.gitignore`.
2. Unity: **Nexo › Cinematics › Validate** (lista los clips, tamaño, resolución, códec; avisa de HEVC, resolución rara o nombres que no se usarán).
3. Editor: Play → JUGAR (intro) → bajar por la escalera o F5 (transición) → BioScanner (final y créditos). Opcional: **Nexo › Checks › Playthrough** hace el recorrido solo (resultado en `Logs/NexoPlaythroughCheck.txt`).
4. Build: **Nexo › Build › Windows** con los vídeos ya copiados. Quedan en `Builds/NexoProtocoloSombra_Windows/NexoProtocoloSombra_Data/StreamingAssets/Cinematics/` (se pueden copiar ahí a mano en un build ya hecho).

## Lo que queda manual

- Jugarlo con teclado y ratón: el salto real con Esc/Espacio/Intro/clic (en batchmode el teclado simulado no llega al juego; la lógica se probó con `RequestSkip` y tests).
- Ver los vídeos reales (calidad, sonido, letterbox en el monitor de la presentación).
- Sensación de combate, voz [V] con micrófono y llamadas reales a Gemini.
- Tras el build: `git restore Assets/_Project/Data/ElenaConfig.asset` si no tienes `ElenaSecrets.asset` (el builder no puede volver a enlazarlo).

## Criterio de aceptación

- [x] Compila sin errores; 190/190 EditMode.
- [x] Sin escenas ni prefabs modificados (`Floor01_Lobby.unity` intacto); `ElenaConfig._forceFallback = 0`; `EnableDemoShortcuts = 0`.
- [x] Ni vídeos, ni packs, ni secretos, ni logs, ni restos de Unity AI en Git.
- [x] Clips ausentes, corruptos o saltados nunca bloquean y restauran el estado.
