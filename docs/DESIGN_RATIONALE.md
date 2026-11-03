| Ayuda | Pistas contextuales, objetivo actual con Tab (`ObjectiveReminder`, `StorySequence.SetObjective`) y la narración guiada del Piso 12 (`StorySequence`). |# Justificación de diseño — NEXO: PROTOCOLO SOMBRA

Entregable de la materia **Diseño de Interfaces** (Ingeniería de Software, 5.º semestre).
Todo lo que se afirma aquí se apoya en archivos del repositorio, citados al final de cada sección. Lo que no se pudo comprobar en el código queda marcado como **[verificar]**.

---

## 1. Concepto: un HUD que miente y un escáner que dice la verdad

En NEXO la interfaz no solo informa: **narra y engaña**. El HUD NexoOS es diegético (forma parte de las gafas de Alex) y durante todo el juego reporta un estado de salud tranquilizador: `99% SANO`, `NO INFECTADO`, `♥ 72 BPM` y el sello `✔ BIO-ID VERIFICADO` en el canal de Elena. El jugador aprende a confiar en esa capa de información.

El giro ocurre en el Piso 1: al poner la mano en el **BioScanner**, la propia interfaz se corrompe y revela el dato real. Los paneles se dispersan, el tema pasa de azul (`theme-nexo`) a rojo colmena (`theme-hive`), el panel vital cambia a `98.4% INFECTADO` / `ROOT DEBUG`, el sello BIO-ID desaparece y aparece un panel de depuración con líneas de desclasificación. Es la subversión deliberada de la heurística de **visibilidad del estado del sistema**: el sistema siempre fue visible, pero no fiable.

Para que el giro sea justo, el HUD deja pistas antes: las **notificaciones falsas** (Mimicry) se distinguen de las reales por cuatro canales independientes (ver §3.4).

Referencias: `Assets/_Project/Scripts/UI/HUDController.cs` (`PlayCorruptionSequence`, `ShatterPanels`), `Assets/_Project/Scripts/Narrative/BioScanner.cs`, `Assets/_Project/Scripts/Narrative/PlotTwistManager.cs`, `Assets/_Project/UI/Layouts/HUD_Biometric.uxml`, `docs/ui/NEXOOS_DESIGN_SPEC.md` §6.

---

## 2. Sistema de diseño NexoOS

### 2.1 Temas y tokens

NexoOS define tres temas mediante variables USS con prefijo `--c-`:

| Tema | Clase | Uso |
| --- | --- | --- |
| nexo (azul) | `.theme-nexo` | Estado normal: menús y HUD mientras el jugador "está sano". |
| hive (rojo) | `.theme-hive` | Estado infectado: pantalla de piso reiniciado, créditos y HUD tras el giro. |
| alto contraste | `.a11y-hc` (combinado: `.theme-nexo.a11y-hc`, `.theme-hive.a11y-hc`) | Se aplica encima de cualquiera de los dos. |

Tokens semánticos (no de color literal): `bg`, `surface`, `surface-raised`, `ink`, `ink-muted`, `accent`, `on-accent`, `line`, `line-faint`, `focus`, `fake`, `status-ok`, `status-warn`, `status-danger`, `scrim`. Ejemplos en nexo: `bg #0b1220`, `accent #22d3ee`, `fake #ff9f43`; en alto contraste `focus` cambia a amarillo `#ffe14d` y todos los paneles pasan a borde de 2px.

Decisiones:
- **Tokens semánticos** permiten cambiar de tema alternando una sola clase en la raíz (`MenuFlowController` usa `EnableInClassList` para `theme-nexo`/`theme-hive`).
- `fake` es un token exclusivo: el naranja **solo** se usa en notificaciones falsas, así el color tiene un único significado.
- Los estados `status-*` siempre van acompañados de una palabra (OK, VERIFICADO…), nunca solo de color.
- El commit `fix(ui): use teal for ok status in hive theme` y `fix(ui): raise contrast of nexo surface-raised token` muestran ajustes de contraste hechos sobre los tokens. Las razones de contraste concretas (p. ej. 4,5:1) **[verificar]**: no hay medición registrada en el repo.

### 2.2 Tipografía

Tres familias (licencia OFL según la especificación) en `Assets/_Project/UI/Fonts/`:
- **Barlow Condensed** Medium / SemiBold / Bold: títulos, ítems de menú, pestañas y botones (condensada: mucho texto en mayúsculas sin ocupar ancho).
- **IBM Plex Sans** Regular: cuerpo y descripciones.
- **IBM Plex Mono** Medium: etiquetas, teclas y cifras (las cifras monoespaciadas no "bailan" al actualizarse).

Escala de 11 estilos (`display-xl` 112px hasta `label` 18px) con **mínimo absoluto de 18px**. Como UI Toolkit no elige pesos, cada clase apunta al archivo del peso exacto.

### 2.3 Espaciado y forma

Escala de 4/8/12/16/24/32/48/64 px, margen seguro de 64px, radios 2/4/8, bordes de 1px (2px en foco y en alto contraste), filas de ajuste de 64px y controles de 48px mínimo.

### 2.4 Componentes reutilizables

Doce plantillas UXML (`NxPanel`, `NxLensFrame`, `NxMenuItem`, `NxTabBar`, `NxSettingRowChoice/Toggle/Slider`, `NxButton`, `NxSegmentedControl`, `NxKeyHint`, `NxNotificationCard`, `NxNotificationCardFake`) instanciadas con `<ui:Instance>` y `AttributeOverrides`, para que cada pantalla componga piezas en vez de duplicar estilos.

Referencias: `Assets/_Project/UI/Styles/nexo-menu-tokens.uss`, `nexo-menu-components.uss`, `nexo-menu-screens.uss`, `theme-nexo-blue.uss`, `theme-hive-red.uss`, `tailwind-utilities.uss`, `Assets/_Project/UI/Layouts/Components/*.uxml`, `Assets/_Project/UI/Fonts/`, `docs/ui/NEXOOS_DESIGN_SPEC.md` §1–§5.

---

## 3. Flujo de menús y accesibilidad

### 3.1 Flujo

`Despertar` (progreso 0→100 % y apertura de "párpados", ~3 s) → `Escaneo biométrico` (~2,5 s) → `Menú principal`. Cualquier tecla, clic o botón de navegación salta directamente al menú. Tras el giro final se recarga la escena persistente y `MenuStartState.SkipBootOnce` evita repetir el arranque.

Menú principal con **cuatro ítems** (`JUGAR`, `OPCIONES`, `CRÉDITOS`, `SALIR`); Opciones con **dos pestañas** (`VIDEO`, `ACCESIBILIDAD`) que se alternan con Q/E; Pausa (`CONTINUAR`, `OPCIONES`, `REINICIAR PISO`, `MENÚ PRINCIPAL`), Piso reiniciado y Créditos (estos dos en tema hive). Reducir el menú a cuatro ítems y las opciones a dos pestañas fue una decisión de alcance registrada en commits (`fix(ui): reduce main menu to four items`, `docs(ui): update spec for four item menu and two option tabs`). La calibración óptica existe como pantalla UXML pero queda fuera del flujo actual.

Cada pantalla muestra una **pista contextual** y una fila de `KeyHint` con las teclas disponibles (reconocer antes que recordar).

### 3.2 Navegación por teclado y mando

`MenuNavigator` se engancha a los eventos de navegación de UI Toolkit (`NavigationMoveEvent`, `NavigationSubmitEvent`, `NavigationCancelEvent`), sin `Update()`: ↑ ↓ mueven el foco, ← → cambian valor, Enter/clic confirman, Esc cancela, Q/E cambian de pestaña. El foco real lo tiene la raíz de la pantalla y el ítem activo se marca con la clase `is-focused`, evitando que el foco por defecto de Unity compita con el visual. Al usar los eventos de navegación, el mando funciona igual que el teclado vía el Input System **[verificar en mando físico]**. El mapa `NexoControls.inputactions` incluye bindings de gamepad (sticks, `buttonWest`, hombros, `start`). P se añadió como tecla secundaria de pausa.

### 3.3 Anillo de foco

USS no tiene `outline`, así que el anillo se construye como contenedor `.focus-ring` con borde de 2px transparente y 3px de relleno; con `.is-focused` o `:focus` el borde toma `--c-focus` con transición de 0.12 s. En alto contraste el foco es amarillo, distinto del acento cian.

### 3.4 Opciones de accesibilidad

`AccessibilityPrefs` persiste en `PlayerPrefs` (claves `nexo.a11y.*`): escala de interfaz (100 / 125 / 150 %), modo daltonismo (deuteranopia, protanopia, tritanopia), alto contraste, reducir movimiento, subtítulos y tamaño de subtítulos. `MenuFlowController` los aplica como clases en la raíz (`a11y-hc`, `reduce-motion`, `ui-scale-lg`, `ui-scale-xl`, `cb-deutan`, `cb-protan`, `cb-tritan`).

- **Reducir movimiento:** `.reduce-motion * { transition-duration: 0s; }` y, en el HUD, `HUDController.SetReduceMotion` suprime los destellos del glitch; durante el giro las luces cambian al instante en vez de en rampa.
- **Destellos acotados:** la secuencia de corrupción limita el glitch a **3 destellos** de 120 ms separados 380 ms, y 0 con reducir movimiento (comentario en el código: WCAG 2.3.1). WCAG se usa por analogía, no como certificación.
- **Notificación falsa sin depender del color:** borde y remitente naranja (`fake`), **ausencia** del sello `✔ BIO-ID`, texto en cursiva inclinado −0.6° con espaciado alterado, y **onda de audio irregular**. Con cualquier modo de daltonismo quedan tres pistas no cromáticas. La pantalla de accesibilidad enseña ambas tarjetas lado a lado con esta leyenda.
- Paleta Okabe-Ito para daltonismo: los selectores `.cb-*` existen en `theme-nexo-blue.uss`; y sus colores son exactamente los de Okabe-Ito (`rgb(230,159,0)`, `rgb(86,180,233)`, `rgb(0,158,115)`, `rgb(240,228,66)`, `rgb(213,94,0)`, `rgb(204,121,167)`).

Referencias: `Assets/_Project/Scripts/UI/Menus/MenuNavigator.cs`, `MenuFlowController.cs`, `AccessibilityPrefs.cs`, `BootSequenceController.cs`, `MenuStartState.cs`, `OptionsController.cs`, `Assets/_Project/UI/Layouts/Menus/*.uxml`, `Assets/_Project/UI/Styles/components.uss` (`.hud-notification--fake`), `Assets/_Project/Input/NexoControls.inputactions`.

---

## 4. HUD e integración con la IA de Elena

### 4.1 HUD biométrico

Cuatro zonas periféricas que dejan el centro libre: vitales (arriba izquierda), radio/videollamada de Elena (arriba derecha), ruido con espectrograma de 24 barras (abajo izquierda) y, abajo a la derecha, una columna con el arma encima de la batería térmica; en el centro mira, prompt `[E]`, notificaciones y subtítulos. Las animaciones usan `VisualElement.schedule.Execute(...).Every(...)` (espectrograma cada 80 ms, pulso del retrato cada 700 ms, typewriter cada 25 ms) en lugar de `Update()`.

### 4.1.1 Pulido del HUD (2026-11-03)

- **Panel de arma y munición.** El jugador tenía una M1911 pero el HUD no la mostraba: la única pista era un subtítulo al quedarse sin balas. El panel nuevo muestra «M1911», el contador grande `cargador / reserva` y una fila de celdas del cargador (lectura de un vistazo, como un juego de acción). Los estados se distinguen por **texto y color** (no solo color, WCAG 1.4.1): aviso con 2 balas o menos («MUNICIÓN BAJA»), peligro con el cargador vacío («RECARGAR [R]», que además enseña la tecla) o sin reserva («SIN MUNICIÓN») y «RECARGANDO…». Al recoger munición aparece «+N» con un pulso breve del borde. Los datos llegan por `GameEvents.OnAmmoChanged` y `OnWeaponReloadChanged` (bus de eventos, sin sondeo), y las decisiones viven en `AmmoDisplayState`, una clase pura con pruebas EditMode. Se coloca en una columna flex con la batería para que no se solape a ninguna escala.
- **Se restauró la mentira de los vitales.** Al añadir vida por golpes, `PlayerHealth` pasó a escribir «VIDA x/y · GOLPES x/y» en la línea donde ponía «NO INFECTADO», y el HUD dejó de mentir. Todo el giro depende de que el jugador confíe en esa línea hasta que el BioScanner revela «98.4% INFECTADO»; por eso vuelve como línea propia (`hud-vitals-infection`) sin quitar la información de combate. Tras la corrupción del HUD, `HUDController` ignora nuevos vitales para que nada sobrescriba la verdad.
- **Coherencia de idioma.** Todo lo que lee el jugador está en español: la emoción de Elena (CALMA, ALEGRE, ASUSTADA, URGENTE, TRISTE, SUSURRO, en vez del nombre del enum), los destinos de las órdenes («Ruta despejada hacia la puerta de las escaleras», nunca `F12_Door_Stairs`), la terminal («TERMINAL DE SEGURIDAD · PISO 12», puertas con nombre) y los diálogos de respaldo con sus tildes. Los ids técnicos solo existen en datos (`target_id`); una prueba impide que aparezcan en los diálogos.
- **Feedback de daño.** Un golpe produce un destello rojo breve en los bordes (viñeta, el centro queda libre) y, con el último punto de vida, un pulso lento y suave. Con «reducir movimiento» no hay destello y la viñeta de salud baja queda fija: la información se mantiene sin animación (WCAG 2.3.3). El subtítulo «(Te golpean: x/4)» sigue existiendo como canal no visual.
- **Ayuda de controles.** Una franja arriba al centro, una vez por sesión y durante 6 s, con las acciones que no son evidentes: «R recargar · clic der. apuntar · F visión térmica · V hablar con Elena · C agacharse» (teclas verificadas en `NexoControls.inputactions` y en el código). No se repite tras un reinicio de piso para no castigar al jugador que ya la vio (heurística de Nielsen 7: flexibilidad y eficiencia).
- **Higiene de textos.** Un traductor (`HudTextSanitizer`) reescribe en el momento de mostrar cualquier id interno que llegue al HUD, venga del código, de una escena, de un JSON de Elena o de Gemini: el dato original se conserva y el jugador nunca lee `F12_Door_Stairs`. Las notificaciones idénticas no se apilan (una tarjeta que «late» y reinicia su vida) y hay como máximo 3 a la vez, para que la columna izquierda no tape la vista (heurística de Nielsen 8: diseño estético y minimalista). La munición vacía usa un chip rojo relleno, distinto en forma y no solo en color del aviso ámbar de munición baja.
- **Detalles.** La batería térmica indica ACTIVA/APAGADA; el retrato de Elena carga `Resources/Elena/portrait.png` si existe; la terminal amplía la única cámara al ancho disponible; Opciones › VIDEO incluye «Volumen general» (no hay AudioMixer con parámetros expuestos, así que se aplica con `AudioListener.volume` y se guarda en PlayerPrefs).

### 4.2 Elena: Gemini con fallback local

- Tres **preguntas rápidas** (teclas 1, 2, 3 o botones `radio-quick-1..3`) mapeadas a las intenciones `SAFE_PATH`, `OPEN_DOOR`, `FEAR`. Limitar la conversación a tres opciones reduce la carga cognitiva y garantiza respuestas jugables.
- `GeminiProvider` hace POST a `gemini-2.5-flash` con cabecera `x-goog-api-key` y `timeout` de 6 s (configurable en `ElenaConfig`).
- Si no hay API key, no hay conexión, se agota el tiempo o la respuesta no pasa `IsValid`, `ElenaAIService` cae a `ElenaFallbackProvider` con JSON por piso (`fallback_floor12.json`, `fallback_floor06.json`, `fallback_floor01.json`) y muestra **una sola vez** la notificación «Enlace con Elena inestable: canal de contingencia activo.» sin bloquear la partida (prevención y recuperación de errores).
- `ElenaUiCommandRouter` valida que una orden `UNLOCK_DOOR` apunte a una puerta incluida en `available_doors`; si no, la rechaza. La IA nunca puede abrir una puerta que el nivel no publicó.
- La clave se lee de un asset de secretos excluido de Git (`chore(git): ignore elena secrets asset anywhere`).

Referencias: `Assets/_Project/Scripts/UI/HUDController.cs`, `QuickQuestionInput.cs`, `Assets/_Project/Scripts/AI/Elena/*.cs`, `Assets/_Project/Data/ElenaFallback/*.json`, `Assets/_Project/Data/ElenaConfig.asset`, `.gitignore`.

---

## 5. Diseño del giro final

La secuencia está separada en **datos testeables** (`TwistTimeline`, `TwistStep`, `TwistAction`) y un **ejecutor** (`PlotTwistManager`):

| t | Acción |
| --- | --- |
| 0 s | Bloqueo del jugador y `OnPlotTwistTriggered` (el BioScanner pasa de cian a rojo, se bloquean las preguntas rápidas). |
| +0.8 s | Corrupción del HUD (destellos, dispersión, cambio a tema hive, ROOT DEBUG). |
| +2.0 s | Luces de emergencia a rojo en ~1.5 s (instantáneo con reducir movimiento). |
| +3.5 s | Líneas finales de Elena como subtítulos (3.5 s por línea). |
| fin + 4 s | Créditos (`MenuEvents.ShowCreditsRequested`). |

Solo se reproduce una vez (`NarrativeEvents.TwistHasPlayed`). Tras los créditos se recarga la escena persistente y el arranque se omite una vez. Los pisos se recorren en orden 12 → 6 → 1 mediante `FloorProgression` / `FloorOrder`.

Referencias: `Assets/_Project/Scripts/Narrative/*.cs`, `Assets/_Project/Scripts/UI/Menus/MenuGameFlowBridge.cs`, `Assets/_Project/Scripts/UI/Menus/CreditsController.cs`.

---

## 6. Restricciones de UI Toolkit y cómo se resolvieron

USS no ofrece `gap`, `rem`, CSS Grid, `@keyframes`, `box-shadow`, degradados, `outline`, `line-height`, `text-transform`, `z-index` ni pseudo-elementos.

| Restricción | Solución en el proyecto |
| --- | --- |
| Sin `gap` | Márgenes en px (utilidades `nx-mb-*`). |
| Sin `@keyframes` | Alternar clases con `transition` desde `schedule.Execute` (glitch, notificaciones, dispersión de paneles). |
| Sin `outline` | Contenedor `.focus-ring` con borde propio. |
| Sin `text-transform` | Mayúsculas escritas directamente en el UXML. |
| Sin `line-height` | Altura de línea con `height`/`min-height` del contenedor. |
| Sin `z-index` | Orden de apilado por jerarquía (p. ej. `fix(ui): draw hud subtitles above the root debug panel`). |
| Sin degradados | Velos de color plano con alfa (`scrim`). |
| Sin pesos tipográficos | Un archivo de fuente por peso. |
| Arcos y ondas | Barras de `VisualElement` (espectrograma, ondas de 16 barras); el arco del escaneo biométrico sigue siendo un círculo estático (TODO en `Boot_BiometricScan.uxml`, pendiente de `generateVisualContent`). |
| Foco por defecto de Unity | El foco real en la raíz y el visual con `is-focused` (`MenuNavigator`). |

Referencias: `docs/ui/NEXOOS_DESIGN_SPEC.md` §4, `Assets/_Project/UI/Styles/*.uss`, `Assets/_Project/Scripts/UI/Menus/MenuNavigator.cs`.

---

## 7. Pruebas

146 pruebas EditMode (NUnit, `Nexo.Tests.EditMode.asmdef`), todas en verde en batchmode el 2026-11-03 (35 antes del pulido del HUD, 108 antes de la higiene de textos):

| Archivo | Casos | Qué cubre |
| --- | --- | --- |
| `ElenaResponseTests.cs` | 9 | Contrato JSON de Elena y `IsValid`. |
| `FloorLabelsTests.cs` | 9 | Etiquetas de piso en pausa, reinicio y captura. |
| `FloorProgressionOrderTests.cs` | 9 | Orden 12 → 6 → 1 y parseo del número de piso. |
| `PlotTwistTimelineTests.cs` | 5 | Orden y tiempos de la línea del giro; rechazo de offsets negativos. |
| `ElenaFallbackFilesTests.cs` | 4 | Que cada fallback de piso exista, cumpla el contrato y no muestre ids técnicos. |
| `AmmoDisplayStateTests.cs` | 27 | Estados, textos, celdas y «+N» del panel de arma. |
| `HudLabelsTests.cs` | 24 | Emociones y puertas en español, título de la terminal, ayuda de controles una vez por sesión. |
| `VitalsFeedbackTests.cs` | 21 | Lectura del porcentaje de vida, detección de daño, salud baja y volumen general. |
| `HudTextSanitizerTests.cs` | 26 | Traducción de ids internos en frases reales, varios ids, puntuación, ids desconocidos y textos sin ids. |
| `NotificationStackTests.cs` | 12 | Deduplicación dentro y fuera de la ventana de 8 s, tope de 3, expulsión de la más antigua y notificaciones falsas. |

Además, el 2026-11-03 se recorrió el juego completo en Play (ver `docs/handoffs/2026-11-03_final-integration.md`): menú → JUGAR → Piso 12 (combate, salida) → Piso 6 (Elena abre `F06_Door_Stairs` con la pregunta [2]) → Piso 1 → BioScanner → giro → créditos → menú → JUGAR desde cero, con persistencia de vida y munición entre pisos y REINICIAR en cada piso.

Referencias: `Assets/_Project/Scripts/Tests/EditMode/`, `docs/playtest/`.

---

## 8. Decisiones y compromisos

- **Lógica pura separada de MonoBehaviour** (`TwistTimeline`, `FloorOrder`, `FloorLabels`, `ElenaResponse`): permite pruebas EditMode rápidas sin escenas. Costo: más tipos pequeños.
- **Bus de eventos estático** (`GameEvents`, `MenuEvents`, `NarrativeEvents`) con limpieza en `SubsystemRegistration`: desacopla HUD, menús y narrativa. Costo: el flujo es menos explícito que una referencia directa.
- **Sin `Update()` en la UI:** todo con `schedule` de UI Toolkit y eventos de navegación. Costo: la lógica temporal queda repartida en callbacks.
- **Fallback local obligatorio:** el juego es completable sin internet ni API key; la IA generativa es un plus, no un requisito.
- **Alcance de menús reducido** (4 ítems, 2 pestañas, sin calibración) para entregar un vertical slice completo.
- **Pipeline Built-in en lugar de URP:** ver §13.
- **Créditos:** solo con datos verificables (packs, `README`/`Read me` de cada pack, licencias OFL de las fuentes, historial de commits). Lo que no tiene evidencia (autor del pack de oficinas, origen del modelo del infectado) no aparece en pantalla y queda listado en `docs/ASSETS.md`.

---

## 9. Taxonomía de interfaz

- **Diegética:** HUD de las gafas, videollamada de Elena, terminales.
- **Espacial:** flechas de salida en el suelo del Piso 12 (material de Carlos en `Materials/Floor12/`) y luces/puertas que Elena manipula en el mundo (`HIGHLIGHT_SAFE_ROUTE`, `UNLOCK_DOOR`, `TOGGLE_HALLWAY_LIGHTS`).
- **Meta:** glitch, viñeta térmica y corrupción del HUD.
- **No diegética:** menú principal, opciones, pausa, créditos (con el marco de lente para mantener la ficción).

## 10. Heurísticas de Nielsen (resumen)

| Heurística | Aplicación verificable |
| --- | --- |
| Visibilidad del estado | Vitales, ruido, batería, confianza e indicador de escritura de Elena (`radio-typing`); subvertida a propósito en el giro. |
| Correspondencia con el mundo real | Lenguaje de gafas corporativas, radio y bioseguridad. |
| Control y libertad | Esc vuelve atrás en todos los menús; el arranque se puede saltar. |
| Consistencia | Componentes `Nx*` compartidos y tokens semánticos. |
| Prevención de errores | `ElenaUiCommandRouter` rechaza puertas no publicadas. |
| Reconocer antes que recordar | KeyHints y pistas contextuales en cada pantalla; tres preguntas rápidas. |
| Flexibilidad y eficiencia | Atajos 1–2–3, Q/E entre pestañas, P como pausa alternativa. |
| Minimalismo | Centro de pantalla libre; cuatro paneles periféricos. |
| Recuperación de errores | Fallo de Gemini → fallback local con aviso no bloqueante. |
| Ayuda | Pistas contextuales, objetivo actual con Tab (`ObjectiveReminder`, `StorySequence.SetObjective`) y la narración guiada del Piso 12 (`StorySequence`). |

---

## 11. Jugabilidad: combate, vida, forcejeo y persistencia

- **Pistola M1911** (`Scripts/Player/PlayerWeapon.cs`): disparo hitscan desde la cámara (clic izquierdo), apuntado con FOV reducido (clic derecho), recarga con R, cargador de 7 y reserva limitada. Cada disparo emite ruido (`GameEvents.RaiseNoiseEmitted`), así que disparar atrae a los infectados: el arma resuelve un problema y crea otro. Sin munición, el subtítulo lo dice («Clic. El cargador está vacío…»).
- **Vida por golpes** (`PlayerHealth.cs`): 3 puntos de vida, cada uno se pierde tras varios golpes; el panel de vitales muestra `ILESO / HERIDO / CRÍTICO` y sube los BPM. Es la misma información biométrica que luego miente en el giro.
- **Forcejeo QTE** (`StruggleQTE.cs`): el primer agarre estando ileso abre un forcejeo con Espacio; fallarlo cuesta un punto de vida. Da una segunda oportunidad antes de la captura.
- **Captura y reinicio** (`Core/PlayerCaptureHandler.cs`): al caer, el HUD avisa «Señal vital interrumpida», el piso se recarga y la pantalla «Piso reiniciado» (tema hive) deja REINTENTAR o volver al menú (`UI/Menus/MenuGameFlowBridge.cs`).
- **Persistencia** (`Core/GameSession.cs`, `Player/PlayerStatePersistence.cs`): al entrar en un piso se guarda un checkpoint con munición, vida y arma; reiniciar o morir restaura ese checkpoint (no se puede acumular munición muriendo) y JUGAR desde el menú empieza de cero.
- **Salida del Piso 12** (`Interaction/LevelExitGate.cs`): la persiana de la escalera solo se abre al eliminar a los 2 infectados; después `StairsExit` baja al Piso 6.

## 12. Audio e infectados

- **Audio** (`Assets/_Project/Audio/`): zumbido de fluorescentes 3D, sirena, golpes, crujidos, susurros, teléfono, grabadora y los sonidos de la M1911, todos generados por procedimiento para el proyecto (ver `docs/ASSETS.md`). `AmbientOneShots`, `LightFlicker` y `ScareTrigger` (`Scripts/Interaction/`) los reparten en el espacio para sostener la tensión sin música.
- **Infectados** (`Scripts/AI/Infected/`): FSM de 5 estados (patrulla, investigar ruido, sabotear luces, emboscada con mímica, persecución) evaluada cada 0,15 s. La mímica lanza notificaciones falsas al HUD (`IsFake`: naranja, sin sello BIO-ID): enseña al jugador a desconfiar de la interfaz antes del giro final.
- **Pisos 6 y 1 sin enemigos:** a propósito. El 6 es el respiro donde Elena demuestra que puede ayudar (abre la puerta); el 1 es calma antes del giro.

## 13. Built-in en lugar de URP

- **Decisión:** el proyecto corre en el pipeline Built-in con materiales `Standard`; no hay asset URP asignado (`ProjectSettings/GraphicsSettings.asset`).
- **Por qué:** el paso de configuración del asset URP que pedía el `README` original nunca quedó versionado, y los pisos 6 y 1 y el viewmodel se construyeron ya con `Standard`. Cambiar de pipeline a un día de la entrega habría obligado a reconvertir todos los materiales.
- **Costes:** los materiales URP/Lit se ven rosas (hay que mantenerlos fuera); sin Volumes de post-proceso. La visión térmica, pensada como Volume `VP_Thermal`, se resolvió con una capa UI Toolkit a pantalla completa (`Scripts/UI/ThermalOverlayController.cs`, `hud-thermal-overlay` en `UI/Layouts/HUD_Biometric.uxml`, viñeta `UI/Textures/T_ThermalVignette.png`), que además respeta `.reduce-motion` y `.a11y-hc`. `BioScanner` tiñe `_Color` (Standard) y `_BaseColor` (URP) solo si el material los tiene, así que funciona en ambos.
- **Beneficio:** un build de Windows más ligero y sin dependencias de configuración de render; el estilo visual descansa en la iluminación en tiempo real y en el HUD, no en el post-proceso.
