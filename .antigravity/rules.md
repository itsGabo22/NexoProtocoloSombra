# NEXO: PROTOCOLO SOMBRA — Reglas del Agente de Código (Antigravity)

## 0. Identidad y alcance
Eres el agente de código de **Gabriel** en un videojuego 3D en primera persona hecho en **Unity 6 LTS (URP)**, C# y **UI Toolkit**. El proyecto es para la materia Diseño de Interfaces y se entrega el 15 de noviembre de 2026.

**Puedes crear o modificar:** `Assets/_Project/Scripts/**`, `Assets/_Project/UI/**`, `Assets/_Project/Data/**` (excepto `Secrets/`), `Assets/_Project/Input/**`, `Assets/_Project/Prefabs/Systems/**`, `Assets/_Project/Scenes/{Persistent,Menus,Sandbox}/**`, `docs/**`, `.github/**` y archivos de configuración de la raíz.

**NUNCA toques:** `Assets/_Project/Scenes/Floors/**`, `Prefabs/Environment|Characters|Interactables/**`, `Models/`, `Materials/`, `Animations/`, `Lighting/`, `Audio/`, `Assets/_ThirdPartyAssets/**`, `ProjectSettings/` (salvo petición explícita de Gabriel). Si una tarea lo requiere, redacta un archivo en `docs/handoffs/` dirigido a Carlos con: qué necesitas, nombre exacto del campo o prefab, y criterio de aceptación.

## 1. Convenciones de C#
- Namespace raíz `Nexo`; subnamespaces por carpeta (`Nexo.AI.Elena`, `Nexo.UI`, `Nexo.Core`).
- Un tipo público por archivo; el nombre del archivo es igual al del tipo.
- Campos privados `_camelCase`; públicos solo propiedades PascalCase. Campos del Inspector: `[SerializeField] private`.
- Clases `sealed` salvo que exista una razón para heredar. Usa `readonly` y `const` cuando aplique.
- Interfaces con prefijo `I` (`IInteractable`, `IInfectedState`). Prefiere composición sobre herencia.
- Prohibido: `GameObject.Find`, `FindObjectOfType` en tiempo de ejecución, `SendMessage`, `Camera.main` dentro de bucles, cadenas mágicas (usa `const`, `enum` o `static readonly` con `Animator.StringToHash`).
- Cachea referencias en `Awake`/`OnEnable`. Usa `TryGetComponent`. Verifica nulos de referencias externas y registra un `Debug.LogWarning` con contexto.
- Comentarios y XML-docs en **español**; identificadores en **inglés**.
- Cada script que depende de otros componentes declara `[RequireComponent]` o valida en `Awake`.

## 2. Cero `Update()` innecesario
- **Prohibido** `Update()` para sondear estado. Usa eventos C# (`Action`, `Action<T>`) definidos en `Nexo.Core.GameEvents`.
- `Update()` solo está permitido en: `PlayerMovement` (entrada y movimiento), `NoiseEmitter` (muestreo del ruido) y el temporizador del `FlashlightSystem` si no se resuelve con corrutina. Cualquier otro caso requiere comentar `// JUSTIFICACIÓN UPDATE:` y su razón.
- Para temporizadores usa corrutinas, `Awaitable` o `VisualElement.schedule`. Para IA, evalúa en ticks de 0,1–0,25 s, no cada frame.
- Suscribe en `OnEnable` y desuscribe en `OnDisable`. Nunca dejes un handler sin su desuscripción simétrica.
- Evita asignaciones de memoria por frame: sin LINQ en rutas calientes, sin `new` en bucles, reutiliza `List<T>` y `StringBuilder`.

## 3. UI: solo UI Toolkit (UXML/USS)
- **Prohibido** uGUI (`Canvas`, `Image`, `Text`) y TextMeshPro para la interfaz. Todo es `UIDocument` + UXML + USS.
- Estilos mediante **clases utilitarias estilo Tailwind** definidas en `UI/Styles/tailwind-utilities.uss` (`flex-row`, `items-center`, `p-4`, `bg-slate-900`, `text-cyan-400`, `rounded-md`, `opacity-50`). Los componentes compuestos (`hud-panel`, `hud-notification`) viven en `components.uss` y se construyen *aplicando utilidades en UXML*, no con estilos en línea.
- Estilos en línea (`style=...` o `element.style.x`) **solo** para valores calculados en ejecución (barra de progreso, posición del radar).
- Temas por clase en el elemento raíz: `theme-nexo` (azul) y `theme-hive` (rojo). Los colores salen de variables USS (`--color-primary`, etc.), nunca hexadecimales sueltos en componentes.
- Limitaciones de USS: no existe `gap`, `@keyframes`, `rem` ni grid. Usa márgenes en `px`, `flex` y `transition`. Las animaciones se logran conmutando clases desde C# con `schedule.Execute(...)`.
- Consulta elementos con `rootVisualElement.Q<T>(name)` **una sola vez** en `OnEnable`, guarda la referencia y desregistra callbacks en `OnDisable`.
- Nombres en UXML: `kebab-case` con prefijo de módulo (`hud-vitals-label`, `radio-elena-portrait`).
- Toda pantalla debe: tener foco navegable con teclado/mando, contraste mínimo 4.5:1 en texto normal, tamaño de fuente escalable mediante clases `ui-scale-*`, y no usar el color como único canal de información.

## 4. IA generativa y red (Elena)
- Toda llamada HTTP usa `UnityWebRequest` con `timeout` de 6 s y un único reintento. Cualquier fallo, timeout o JSON inválido activa el **fallback local** sin romper la partida.
- La respuesta del LLM se deserializa a un modelo tipado (`ElenaResponse`) y se **valida**: `emotion` y `ui_action` deben pertenecer a enums conocidos; si no, se descartan.
- Nunca registres la API key ni la subas al repositorio. Se lee de `ElenaSecrets` (ignorado). Si falta, modo fallback.
- El System Prompt vive en un `TextAsset`/`ScriptableObject`, no incrustado en código.
- Todo comando de UI recibido (`ui_action`) pasa por `ElenaUiCommandRouter`, que lo convierte en un evento de `GameEvents`; la IA nunca manipula la UI directamente.

## 5. IA táctica (FSM)
- Cada estado implementa `IInfectedState` con `Enter`, `Tick(float dt)` y `Exit`. Las transiciones se definen en un solo lugar (`SmartInfectedBrain`).
- Los sensores publican eventos (`OnNoiseHeard`, `OnPlayerSeen`) en lugar de ser consultados en cada frame.
- Las acciones sobre el HUD (notificación falsa) se envían **solo** mediante `GameEvents.OnFakeNotification`.

## 6. Calidad y rendimiento
- Presupuesto: 60 FPS en un portátil medio, sin asignaciones por frame en gameplay.
- Pruebas EditMode para: transiciones de la FSM y parser/validador del JSON de Elena.
- Sin código muerto, sin `TODO` huérfanos (usa `// TODO(gabriel): ...` con un issue).

## 7. Git: Conventional Commits atómicos (OBLIGATORIO)
Formato: `<tipo>(<ámbito>): <resumen imperativo en minúsculas, máx. 72 caracteres>`

- **Tipos:** `feat`, `fix`, `refactor`, `style`, `perf`, `test`, `docs`, `chore`, `build`.
- **Ámbitos:** `core`, `player`, `ai`, `elena`, `infected`, `ui`, `hud`, `terminal`, `a11y`, `narrative`, `input`, `git`, `docs`.
- **Un commit = una unidad lógica cerrada**: una clase nueva, una interfaz, una hoja de estilos o un UXML. El archivo y su `.meta` (que genera Unity al enfocar el Editor) van **en el mismo commit**.
- Cuerpo opcional en español explicando el *por qué*. Si es un cambio rompedor: `BREAKING CHANGE:` en el pie.
- Ejemplos válidos: `feat(ui): add tailwind utility stylesheet`, `feat(elena): implement unitywebrequest client with timeout`, `fix(infected): unsubscribe noise handler on disable`.
- **Prohibido:** `git add .`, `git add -A`, `git commit -a`, `git push --force`, mensajes como “avances” o “fix”, commits que mezclan UI con IA, subir secretos.
- Siempre añade rutas explícitas: `git add Assets/_Project/Scripts/AI/Elena/ElenaModels.cs Assets/_Project/Scripts/AI/Elena/ElenaModels.cs.meta`.
- Usa comillas simples en `git commit -m '...'` (PowerShell/Git Bash).

## 8. Bucle de trabajo del agente
1. Lee la tarea y confirma en una línea qué archivos vas a crear o modificar.
2. Crea **un** archivo o cambio cohesivo.
3. Verifica que compila (sin errores en consola) y que cumple estas reglas.
4. Haz `git add` de esos archivos (más `.meta`) y `git commit` con el mensaje convencional.
5. Repite para el siguiente archivo. No acumules cambios sin commitear.
6. Al terminar, resume: commits creados, archivos tocados y pendientes para Carlos (si los hay).

## 9. Definición de terminado (DoD)
Compila sin advertencias nuevas · respeta namespaces y nombres · sin `Update()` injustificado · UI solo con UXML/USS · sin secretos · commit atómico con mensaje válido · documentado en español.
