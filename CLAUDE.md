# NEXO: PROTOCOLO SOMBRA — Guía para agentes de código

Actualizada el 2026-11-03 (integración final). Sustituye a la versión anterior, que describía un proyecto URP de un solo piso y un solo desarrollador.

## 0. Estado real del proyecto

- **Motor:** Unity 6000.6.7f1, **pipeline Built-in** (no hay asset SRP asignado en Graphics ni Quality). Todos los materiales usan el shader **Standard**. Un material URP/Lit se ve **rosa**: no los uses.
- **Juego completo de 3 pisos**, cargados de forma aditiva sobre `Assets/_Project/Scenes/Persistent/Systems_Persistent.unity`:
  1. `Scenes/Floors/Floor12_Offices.unity` (oficinas, combate, 2 infectados, historia, salida por quiosco/escalera).
  2. `Scenes/Floors/Floor06_Labs.unity` (laboratorios; Elena abre `F06_Door_Stairs` con la pregunta rápida [2]).
  3. `Scenes/Floors/Floor01_Lobby.unity` (vestíbulo, `BioScanner` y giro final → créditos).
- Lista de build (en este orden): Systems_Persistent, Floor12_Offices, Floor06_Labs, Floor01_Lobby.
- **Sistemas que existen:** pistola M1911 (`PlayerWeapon`: disparo, apuntar, recarga, munición), vida por golpes (`PlayerHealth`), forcejeo QTE (`StruggleQTE`), recogida de munición (`AmmoPickup`), persistencia entre pisos y reinicios (`GameSession` + `PlayerStatePersistence`), captura y reinicio (`PlayerCaptureHandler`), progresión de pisos (`FloorProgression` + `StairsExit`), giro final (`PlotTwistManager` + `TwistTimeline`), menús NexoOS (`MenuFlowController`), HUD biométrico, Elena con Gemini y fallback offline, IA de infectados (`SmartInfectedBrain`, FSM de 5 estados), audio ambiental (`AmbientOneShots`, `LightFlicker`, `ScareTrigger`).
- **Visión térmica:** tecla F (`FlashlightSystem`). El Volume `VP_Thermal` de `ThermalVisionController` no tiene efecto en Built-in; lo visible es la capa UI Toolkit `hud-thermal-overlay` que activa `Scripts/UI/ThermalOverlayController.cs` (tinte cian + viñeta `UI/Textures/T_ThermalVignette.png`).

## 1. Propiedad y autoridad

| Responsable | Áreas |
| --- | --- |
| Gabriel (`itsGabo22`) | Menús y UI (`Scripts/UI/**`, `UI/**`), narrativa (`Scripts/Narrative/**`), `Systems_Persistent`, pisos 6 y 1, docs |
| Carlos «Caliche» (`simviz12`) | Piso 12 (escena, prefabs, materiales, iluminación), jugador y gameplay (`Scripts/Player/**`), IA (`Scripts/AI/**`), audio |

- El **Piso 12 está terminado**: `Floor12_Offices.unity` y sus prefabs/datos solo se tocan si un bug bloquea la partida, con el cambio mínimo y documentado en el handoff.
- En código de Carlos, solo cambios mínimos y comentados, listados en el handoff o el PR.
- No toques `Packages/**` ni `ProjectSettings/**` salvo lo que el build necesite estrictamente. No cambies el pipeline ni instales paquetes.

## 2. Estándares de C# (siguen vigentes)

1. Namespace raíz `Nexo` con subnamespaces por módulo: `Nexo.Core`, `Nexo.Player`, `Nexo.Interaction`, `Nexo.AI.Elena`, `Nexo.AI.Infected`, `Nexo.UI`, `Nexo.UI.Menus`, `Nexo.Narrative`, `Nexo.Accessibility`.
2. Un tipo público por archivo; clases `sealed` por defecto; campos privados `_camelCase`; Inspector con `[SerializeField] private`; propiedades `PascalCase`.
3. Comentarios y XML-docs en **español**; identificadores en **inglés**.
4. **Eventos, no sondeo:** buses estáticos `Nexo.Core.GameEvents`, `Nexo.UI.Menus.MenuEvents` y `Nexo.Narrative.NarrativeEvents`, que se limpian con `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`. Suscripción en `OnEnable` (`+=`) y desuscripción simétrica en `OnDisable` (`-=`) con **métodos con nombre** (nunca lambdas).
5. `Update()` solo en `PlayerMovement`, `NoiseEmitter` y `FlashlightSystem` (con comentario `// JUSTIFICACIÓN UPDATE:`). Temporizadores con corrutinas o `VisualElement.schedule`; la IA evalúa cada 0,15 s.
6. Prohibido en tiempo de ejecución: `GameObject.Find`, `FindObjectOfType`, `SendMessage`, `Camera.main` en bucles, LINQ en rutas calientes. Parámetros del Animator con `Animator.StringToHash` en `static readonly int`.
7. Tests EditMode en `Scripts/Tests/EditMode/` solo para lógica pura (orden de pisos, timeline del giro, etiquetas, fallbacks de Elena, validación de respuestas).

## 3. UI Toolkit

- **Solo UI Toolkit** (`UIDocument` + UXML + USS). Prohibido uGUI y TextMeshPro.
- Estilos en `Assets/_Project/UI/Styles/`: `tailwind-utilities.uss`, `nexo-menu-tokens.uss`, `theme-nexo-blue.uss`, `theme-hive-red.uss`, `components.uss`, `nexo-menu-components.uss`, `nexo-menu-screens.uss`. Temas: `.theme-nexo` (azul), `.theme-hive` (rojo, créditos y «piso reiniciado»), alto contraste `.a11y-hc`, daltonismo `.cb-*`, reducir movimiento `.reduce-motion`.
- USS no soporta `gap`, `@keyframes`, `rem` ni Grid: márgenes en `px`, `flex` y `transition-property`.
- Especificación visual: `docs/ui/NEXOOS_DESIGN_SPEC.md`. Justificación de diseño: `docs/DESIGN_RATIONALE.md`.

## 4. Elena (Gemini + fallback)

- `Data/ElenaConfig.asset`: modelo `gemini-2.5-flash`, timeout 6 s, `_forceFallback` (en el repo queda en **false** para que Gemini funcione en el Editor; ponlo en **true** solo para generar el build de entrega y restáuralo después).
- La clave vive en `ElenaSecrets.asset` dentro de `Data/Secrets/` (ignorado). Sin clave, sin red o con JSON inválido entra el fallback (`fallback_floor12/06/01`).
- `ElenaUiCommandRouter` solo abre puertas cuyo `target_id` esté en `available_doors`.

## 5. Secretos (el repo es PÚBLICO)

- Nunca versiones `ElenaSecrets.asset` ni ninguna clave.
- Antes de cada push, escanea el diff contra `origin/develop`: `AIza…`, `"api_key": "…"`, valores de `x-goog-api-key`, `Bearer …`. Si aparece algo, para y avisa.

## 6. Packs de terceros

- No se versionan (ni con LFS). Viven en `Assets/_ThirdPartyAssets/` o `Assets/ThirdPartyAssets/` (ambas ignoradas; usa solo una para no duplicar GUIDs).
- Para actualizar: el dueño exporta la carpeta `Pack_<nombre>` completa **con sus `.meta`** en un zip; el otro cierra Unity, la extrae en su carpeta de packs y no la reimporta desde la Asset Store. Registra fuente, autor y licencia en `docs/ASSETS.md`.
- Si falta un pack, Unity muestra «Missing Nested Prefab»; no «arregles» el prefab: consigue el pack.

## 7. Git

- Ramas de trabajo desde `origin/develop`. Nunca push a `main` ni a `develop`, nunca force-push, nunca reescribir historia publicada, nunca mergear PRs (los mergea Gabriel). Integra con `merge`, no con `rebase`.
- Conventional Commits: `<tipo>(<ámbito>): <resumen en inglés y minúsculas, máx. 72>`; tipos `feat`, `fix`, `refactor`, `style`, `perf`, `test`, `docs`, `chore`, `build`.
- Un commit = una unidad lógica. **Prohibido `git add .` / `git add -A`**; añade cada archivo con su `.meta`: `git add <archivo> <archivo>.meta`.
- Nunca subas `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/Packages/**` ni líneas de Unity AI (`com.unity.ai*`, `app-ui`) en `EditorBuildSettings.asset`. En la máquina de Gabriel hay `skip-worktree` sobre manifest/lock y un hook `pre-commit` que lo bloquea. Si un merge/switch los necesita: `git update-index --no-skip-worktree` en ambos, `git restore`, la operación y de nuevo `--skip-worktree`. Ojo: el cambio local del manifest es `com.unity.ai.assistant`, que aloja el puente MCP; si `develop` no toca `Packages/`, no hace falta restaurarlo.
- Unity reescribe `Materials/Floor12/MAT_F12_ExitButton.mat` al entrar en Play: descártalo con `git restore`.
- Traspasos y progreso en `docs/handoffs/` (plantilla `docs/handoffs/TEMPLATE.md`).

## 8. Unity MCP (opcional)

- `ManageEditor` (Play/Stop/GetState), `RunCommand` (C# en el Editor), `ManageScene`, `ManageGameObject`, `ReadConsole`/`GetConsoleLogs`, `Camera_Capture`.
- `RunCommand`: la clase debe ser `internal class CommandScript : IRunCommand`; rechaza `System.Reflection` y `GetInstanceID()`.
- `ManageGameObject` solo busca en la escena **activa** (en juego, la del piso).
- `Camera_Capture` no dibuja lo que vive en `Systems_Persistent` (jugador, arma, HUD). Para ver el arma, renderiza la Main Camera a un `RenderTexture` desde `RunCommand`; para ver el HUD usa `ScreenCapture.CaptureScreenshot` y espera un frame.
- `ReadConsole` ignora filtros de fecha y devuelve máx. 200 entradas.
- El puente se cae ~20 s en recompilaciones, al entrar en Play y durante builds: espera y reintenta una vez.
- Con el Editor sin foco, Play no avanza: `Application.runInBackground = true` en Play, y al salir **`PlayerSettings.runInBackground = false`** (si no, ProjectSettings queda modificado).
- Nunca dejes el Editor en Play. Guarda las escenas tras editarlas. Ignora el error rojo `manager != NULL` (bug del Hierarchy de Unity 6.6).
