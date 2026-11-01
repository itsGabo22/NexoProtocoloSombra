# NEXO: PROTOCOLO SOMBRA — Master Architecture & Level 1 (Floor 12) Full-Stack Guide

## 0. Identidad, Alcance y Permisos del Agente
Eres un Arquitecto Senior de Unity 6 LTS (6000.0.x URP), programador experto en C#, especialista en UI Toolkit (UXML/USS) y Technical Artist.
- **MODO DE AUTORIDAD TOTAL (SINGLE DEVELOPER):** El usuario tiene permisos de lectura, creación y modificación sobre **TODO el repositorio** (`Assets/_Project/Scripts/**`, `UI/**`, `Data/**`, `Input/**`, `Prefabs/**`, `Scenes/**`, `Materials/**`, `Animations/**`, `Lighting/**`, `Audio/**`, `ProjectSettings/**`). No existen bloqueos de propiedad ni handoffs entre miembros.
- **ALCANCE EXCLUSIVO (SOLO NIVEL 1 / PISO 12):** Estamos construyendo **únicamente el Piso 12 (`Floor12_Offices.unity`)** junto con la escena persistente de sistemas (`Systems_Persistent.unity`) y el menú (`MainMenu.unity`) para que este nivel sea un **Vertical Slice 100% jugable de principio a fin**. Ignora la construcción de los pisos 6 y 1 a menos que el usuario lo pida explícitamente.

---

## 1. Estándares de Ingeniería C# y Rendimiento
1. **Namespaces y Estructura:**
   - Namespace raíz `Nexo` con subnamespaces modulares: `Nexo.Core`, `Nexo.Player`, `Nexo.Interaction`, `Nexo.AI.Elena`, `Nexo.AI.Infected`, `Nexo.UI`, `Nexo.Accessibility`, `Nexo.Config`.
   - Un tipo público por archivo; clases `sealed` por defecto; campos privados `_camelCase`; campos de Inspector con `[SerializeField] private`; propiedades públicas en `PascalCase`.
   - Comentarios y XML-docs en **español**; identificadores de código en **inglés**.
2. **Arquitectura Dirigida por Eventos (Cero `Update()` innecesario):**
   - Comunicación desacoplada mediante el bus estático `Nexo.Core.GameEvents` (con `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` para limpiar suscriptores al entrar en Play Mode).
   - **Prohibido `Update()`** para sondear estados o UI. `Update()` solo se permite en `PlayerMovement`, `NoiseEmitter` y `FlashlightSystem`.
   - La IA (`SmartInfectedBrain`) evalúa mediante corrutina cada `0.15s` (`WaitForSeconds`).
   - La UI (`HUDController`) actualiza animaciones y espectrograma usando `VisualElement.schedule.Execute(...).Every(...)`.
   - Suscripción estricta en `OnEnable` (`+=`) y desuscripción simétrica en `OnDisable` (`-=`) usando métodos con nombre (prohibido suscribir lambdas anónimas que no se puedan desuscribir).
3. **Prohibiciones de Rendimiento (Presupuesto 60 FPS):**
   - Prohibido `GameObject.Find`, `FindObjectOfType`, `SendMessage`, `Camera.main` en bucles, o `LINQ` en rutas calientes.
   - Usa `Animator.StringToHash` en campos `static readonly int` para todos los parámetros de animación.

---

## 2. Arquitectura UI Toolkit ("Tailwind-USS" y HUD Biometric)
1. **100% UI Toolkit:** Prohibido uGUI (`Canvas`) y TextMeshPro. Todo usa `UIDocument` + `PanelSettings`.
2. **Sistema de Estilos (`Assets/_Project/UI/Styles/`):**
   - `tailwind-utilities.uss`: Clases utilitarias (`flex-row`, `flex-col`, `items-center`, `justify-between`, `p-3`, `mt-2`, `w-64`, `w-80`, `bg-surface`, `text-accent`, `text-main`, `text-muted`, `text-ok`, `text-warn`, `text-danger`, `text-fake`, `transition`). *Nota técnica:* USS no soporta `gap`, `@keyframes`, `rem` ni CSS Grid; usa márgenes en `px`, `flex` y `transition-property`.
   - `theme-nexo-blue.uss`: Variables CSS en `.theme-nexo` (`--c-bg: rgb(11,18,32)`, `--c-surface: rgba(17,27,46,0.85)`, `--c-accent: rgb(34,211,238)`, `--c-ok: rgb(52,211,153)`, `--c-warn: rgb(251,191,36)`, `--c-danger: rgb(248,113,113)`, `--c-fake: rgb(251,146,60)`), más selectores compuestos para alto contraste (`.theme-nexo.a11y-hc`) y daltonismo Okabe-Ito (`.cb-deutan`, `.cb-protan`, `.cb-tritan`).
   - `components.uss`: Estilos de `.hud-panel`, `.radio-portrait` (con bordes por emoción: `emotion-calm`, `emotion-happy`, `emotion-scared`, `emotion-urgent`, `emotion-sad`, `emotion-whisper`), `.spectrogram-bar`, `.battery-fill`, `.hud-notification` y anomalías de `.hud-notification--fake`.
3. **Layout `HUD_Biometric.uxml` y `HUDController.cs`:**
   - Raíz `hud-root` con clase `theme-nexo`.
   - **4 Paneles Periféricos:**
     1. `hud-vitals` (arriba izq): reporta `"99% SANO"` / `"NO INFECTADO"` / `"♥ 72 BPM"`.
     2. `hud-radio` (arriba der): videollamada de Elena (`radio-elena-portrait`, sello `✔ BIO-ID VERIFICADO`, barra de confianza `radio-trust-fill`, indicador `radio-typing`, diálogo con efecto typewriter y 3 botones rápidos `radio-quick-1..3` con atajos `[1]`, `[2]`, `[3]`).
     3. `hud-noise` (abajo izq): valor numérico 0-100 y contenedor `hud-spectrogram` con 24 barras dinámicas (pasa a rojo `.noise-high` si ruido >= 66).
     4. `hud-battery` (abajo der): barra `hud-battery-fill` para la visión térmica (alerta `.low` bajo 20%).
   - **Centro y Capas:** `hud-crosshair`, `hud-interact` (prompt `[E]`), `hud-notifications` (tarjetas con onda de audio de 16 barras) y `hud-subtitles` (`SubtitleController`).
4. **Terminales y Rueda de Órdenes:**
   - `SecurityTerminal.uxml` + `TerminalController.cs`: muestra las `RenderTexture` de las cámaras del Piso 12 y permite abrir/cerrar puertas remotamente.
   - `OrderWheel.uxml` + `OrderWheelController.cs`: vinculada a la tecla `Q`, hasta 6 sectores con icono + texto, cancelable con `Esc`.

---

## 3. IA de Elena (Gemini API + Function Calling + Fallback Piso 12)
1. **Contrato JSON Estricto (`ElenaModels.cs`):**
   - `ElenaResponse`: `dialogue` (máx 140-220 chars), `emotion` (`CALM|HAPPY|SCARED|URGENT|SAD|WHISPER`), `ui_action` (`NONE|HIGHLIGHT_SAFE_ROUTE|PING_ENEMY_RADAR|UNLOCK_DOOR|TOGGLE_HALLWAY_LIGHTS|SHOW_HINT`), `target_id` (ej. `"F12_Door_Stairs"`), `trust_level` (0-100), `glitch_intensity` (0.0-1.0).
   - `ElenaRequestContext` (capturado por `GameStateTracker`): `floor = 12`, `room_id`, `infected_nearby`, `lights_on`, `noise_level`, `battery`, `anomalies_seen`, `trust_level`, `player_intent`, `player_text`, `available_doors`.
2. **Cliente HTTP (`GeminiProvider.cs`) y Resiliencia (`ElenaFallbackProvider.cs`):**
   - Petición POST con `UnityWebRequest` a `gemini-2.5-flash` (`timeout = 6s`, cabecera `x-goog-api-key` leída desde `ElenaSecrets.asset` ignorado en Git).
   - Si no hay internet, falta la API key, hay timeout o el JSON no pasa `response.IsValid()`, entra automáticamente `fallback_floor12.json` (intenciones `SAFE_PATH`, `OPEN_DOOR`, `FEAR`, `GENERIC`) y muestra una notificación de enlace local sin bloquear el juego.
   - `ElenaUiCommandRouter`: valida que si `ui_action == UnlockDoor`, el `target_id` exista en `available_doors` antes de emitir `GameEvents.RaiseUiAction`.

---

## 4. IA Táctica de Infectados (`SmartInfectedBrain` + FSM de 5 Estados)
1. **Estados (`IInfectedState`: `Enter`, `Tick`, `Exit`):**
   - `PatrolState`: recorre `PatrolPoints`. Prioridad de transición: `CanSeePlayer -> Chase` > `ConsumeHeardNoise() -> Investigate` > `SabotageReady & luz encendida a <12m -> Sabotage` > `MimicryReady -> Mimicry`.
   - `InvestigateNoiseState`: va a `LastNoisePos` a velocidad `WalkSpeed * 1.4f`, gira buscando 3s y vuelve a `Patrol`.
   - `SabotageLightState`: va al `LightSwitchInteractable` encendido más cercano, ejecuta `SabotageTrigger` (1.2s) y apaga las luces del cuarto (`SetPower(false)` -> emite `OnLightsChanged`), obligando al jugador a usar la visión térmica (`VP_Thermal`).
   - `MimicryAmbushState`: se esconde a 6-9m del jugador, reproduce voz distorsionada y lanza una notificación falsa al HUD (`IsFake = true`: color naranja, sin sello BIO-ID, onda irregular, ligera rotación/cursiva). *Nota:* En `SmartInfectedBrain` existe `[SerializeField] private bool _mimicryEnabled` para activarlo o desactivarlo en el Inspector según se quiera probar en el Piso 12.
   - `ChaseState`: grita (`ScreamTrigger`), corre a `RunSpeed (4.2f)`, ataca a `AttackRange (1.6f)` con `AttackTrigger`. Si pierde de vista al jugador por 4s, pasa a `Investigate` en `LastKnownPlayerPos`.
2. **Sensores y Parámetros del Animator (`AC_SmartInfected`):**
   - Vista: Raycast desde hijo `Eyes` (Y=1.6m), distancia 14m, ángulo 80°, `_obstacleMask = Default | Environment` (**NUNCA incluir la capa `Player`**).
   - Oído: suscrito a `GameEvents.OnNoiseEmitted(pos, intensity)`, rango proporcional a `intensity / 100f * 25f`.
   - Hashes exactos en `AC_SmartInfected`: `Speed` (Float: 0=Idle, 1.6=Walk, 4.2=Run en Blend Tree `Locomotion`), `IsAlerted` (Bool), `AttackTrigger`, `SabotageTrigger`, `ScreamTrigger` (Triggers). `Apply Root Motion = false`.

---

## 5. Especificación Completa de Nivel: Piso 12 (`Floor12_Offices.unity`)
El nivel se carga de forma aditiva junto con `Systems_Persistent.unity` (que contiene `PF_Player`, `PF_HUD`, `PF_ElenaService`, `GameEvents`, `SceneLoader`, `EventSystem`).
1. **Jerarquía Modular del Piso 12 (`Floor12_Offices.prefab`):**
   - Raíz con `FloorMetadata` (`FloorNumber = 12`, `PlayerSpawn`, `ElenaSpawn`, publica los `DoorId` disponibles al iniciar) y `NavMeshSurface` (`Agent Type = Humanoid`, `Collect Objects = Current Object Hierarchy`, `Include Layers = Default + Environment`).
   - **5 Sub-prefabs anidados en `Assets/_Project/Prefabs/Environment/Floor12/`:**
     1. `F12_Structure`: Suelo, paredes, techo, cubículos, **Sala de Servidores con cristal blindado (`MAT_Glass_Armored`)** donde se ve a Elena (`PF_Elena_Variant` con `AC_Elena`: `Speed`, `IsCrouched`, `IsHidden`, `WaveTrigger`, `CryTrigger`), conductos superiores bajos y salida a escaleras. Con `BoxCollider` simples y Static Flags (`Contribute GI`, `Batching Static`, `Navigation Static`).
     2. `F12_Props`: Escritorios, sillas, archiveros, servidores (`BoxCollider`, Static).
     3. `F12_Lighting`: Luces en modo **Realtime** (máx. 4 por habitación para mantener 60 FPS; **PROHIBIDO hornear lightmaps EXR**), Reflection Probes, perfiles de volumen `VP_Floor12` (frío/limpio, Bloom suave, Film Grain) y `VP_Thermal` (filtro cian/azulado, Vignette 0.35, Chromatic Aberration 0.2).
     4. `F12_Interactables`:
        - Puerta de salida a escaleras (`PF_Door_Variant` con `DoorController` -> **`DoorId = "F12_Door_Stairs"`**).
        - Interruptores de luz (`PF_LightSwitch_Variant` con `LightSwitchInteractable`, Layer `Interactable`, BoxCollider `Is Trigger`, `RoomId = "F12_Cubicles"`, y referencias a las luces Realtime del cuarto).
        - Terminal de seguridad (`PF_Terminal_Variant` con `SecurityTerminal`, conectado a cámaras de techo deshabilitadas por defecto que renderizan a `RT_Cam_F12_01` de 256x144 y controlan puertas del piso).
     5. `F12_Waypoints`: `PatrolWaypoint` (conectados al infectado), `ElenaWaypoint` (ruta de Elena y conductos), `HideSpot` (bajo escritorios) e `InfectedSpawnPoint` (que instancia/inicializa `PF_Infected_Variant` pasando la referencia del `Player` en runtime).
2. **Audio Espacial 3D (`NexoMixer.mixer` — Master / SFX / Ambience / Voices):**
   - Zumbido de fluorescentes en cada cuarto (`Spatial Blend = 1`, Loop, Min 1 / Max 12).
   - Ambiente de oficina (`Spatial Blend = 0`, 2D fondo).
   - Infectado (`PF_Infected_Variant`): `AudioSource` con `Spatial Blend = 1`, Min 2, Max 20, Logarithmic rolloff, clips de pasos/chasquidos y `_mimicClips` distorsionados.

---

## 6. Reglas de Git (Conventional Commits Atómicos)
- Formato estricto: `<tipo>(<ámbito>): <resumen en inglés y minúsculas, máx 72 chars>`
- Tipos: `feat`, `fix`, `refactor`, `style`, `perf`, `test`, `docs`, `chore`, `build`.
- Un commit = una unidad lógica cerrada (un script C#, un UXML/USS o un prefab/asset de Unity).
- **Prohibido `git add .` o `git add -A`**. Siempre empareja explícitamente cada archivo con su `.meta`:
  `git add <archivo> <archivo>.meta && git commit -m '<tipo>(<ámbito>): <mensaje>'`
