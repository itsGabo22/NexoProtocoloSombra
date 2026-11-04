# Guion de demo — NEXO: PROTOCOLO SOMBRA (10–12 min)

Para la presentación del 2026-11-04. Se ejecuta el build de Windows (`Builds/NexoProtocoloSombra_Windows/NexoProtocoloSombra.exe`, ver `docs/LEEME_EJECUCION.md`). El build lleva a Elena **sin conexión** (fallback escrito por el equipo, no depende de internet) y los **atajos de presentación F5-F8 activados**. Plan B: proyecto en Unity 6000.6.7f1, `Systems_Persistent.unity`, Play (ahí Elena usa Gemini si hay `ElenaSecrets.asset` y los atajos están apagados).

## Antes de empezar (5 min antes)

1. Portátil enchufado, brillo al máximo, sonido por los altavoces de la sala. Proyector en modo «presentación/brillante».
2. Abrir el `.exe`, **Alt + Intro** para pantalla completa, comprobar que se ve el menú y que suena.
3. Volver al menú principal y dejarlo ahí. Cerrar notificaciones de Windows (modo «No molestar»).

## Guion

| Tiempo | Dónde | Qué hacer | Qué decir |
| --- | --- | --- | --- |
| 0:00–0:45 | Arranque | Dejar correr el despertar y el escaneo biométrico; cualquier tecla. | «NexoOS son las gafas de Alex. Todo lo que veis es su interfaz, y esa interfaz va a ser el narrador no fiable del juego.» |
| 0:45–2:00 | Opciones | OPCIONES → ACCESIBILIDAD (E). Alto contraste, reducir movimiento, ejemplo de notificación real frente a falsa. Esc. | «Escala, daltonismo Okabe-Ito, alto contraste, reducir movimiento y subtítulos se aplican al instante. Una notificación falsa se distingue por 4 pistas; con daltonismo quedan 3.» |
| 2:00–2:30 | Intro | JUGAR. Dejar ver unos segundos de la cinemática y saltarla (Esc). | «Las cinemáticas se generaron con IA y se pueden saltar; si faltara un vídeo, el juego sigue sin él.» |
| 2:30–5:00 | **Piso 12** | Leer el HUD (ILESO, ruido, radio de Elena con su retrato, batería). **F** térmica. Disparar a los 2 infectados (clic derecho apunta). Si uno agarra: Espacio. Usar la terminal de seguridad (E) y la red de cámaras (C). | «El HUD dice que estoy ileso. El disparo hace ruido y atrae a los infectados. Desde la terminal veo las cámaras de los tres pisos: el piso actual en vivo, los otros en diferido.» |
| (en el 12) | Mecánicas | Mirar una nota (se resalta) y leerla (E): papel a pantalla completa; **J** diario. Matar al primer patrullero y recoger su cuchillo (E); **X** por la espalda = ejecución silenciosa. **G** empuja; **Espacio** salta. Asomarse tras el cristal de la sala de servidores: ahí está Elena. | «Las notas quedan en el diario. El cuchillo es silencioso; el disparo atrae a todos.» |
| 5:00–5:45 | Elena | Pulsar **1, 2, 3**: videollamada, sello BIO-ID, barra de confianza. Bajar por la escalera (E). | «Elena responde con Gemini 2.5 Flash en un JSON estricto (diálogo, emoción y una acción de UI que validamos antes de ejecutar). Aquí va sin conexión: entra el fallback que escribimos por piso.» |
| 5:45–8:30 | **Piso 6** | Explorar el pasillo; leer una nota; entrar al Lab B y vivir el apagón; usar el diferencial del pasillo; esconderse en un armario si se acerca algo. Pulsar **2**: Elena abre la puerta de las escaleras. Esc → mostrar REINICIAR PISO (no pulsarlo). Bajar. | «En este piso no se gana disparando: escondites, ruido y luz. Ojo con los mensajes de Elena sin sello BIO-ID: el imitador usa su voz. Vida y munición se conservan entre pisos.» |
| 8:30–10:30 | **Piso 1** | Caminar al BioScanner del centro (unos 18 m) y poner la mano (E). No tocar nada durante el giro. | «El escáner dice la verdad: 98,4 % infectado. El HUD se rompe, el tema pasa a rojo colmena y Elena confiesa. Todo lo que el HUD decía era mentira.» |
| 10:30–11:30 | Créditos | Dejar ver la cinemática final y los créditos; Esc → menú principal. | «Los créditos solo incluyen datos verificados; lo que no pudimos probar está acreditado de forma genérica.» |

## Si falta tiempo (atajos del build de entrega)

| Recorte | Atajo |
| --- | --- |
| Saltar el combate del Piso 12 | **F5** (piso siguiente, con su transición) |
| Se acaba el tiempo en el Piso 6 | **F5** otra vez, o **F8** directo al BioScanner |
| Quedan menos de 3 minutos en total | Desde el Piso 12: **F8** (baja al Piso 1 y deja a Alex frente al escáner) → E |
| Te dejaron sin vida o munición | **F6** vida · **F7** munición |

Versión mínima de 5 minutos: arranque → JUGAR → 1-2-3 con Elena → F8 → BioScanner → créditos.

## Elena con y sin internet

| Situación | Qué pasa | Qué decir |
| --- | --- | --- |
| **Build de entrega** (lo que se presenta) | Siempre sin conexión: 1, 2, 3 y [T] responden con el fallback escrito por piso, con voz grabada. No hace falta wifi. | «Esta versión no depende de la red: Elena responde con líneas que escribimos para cada piso.» |
| **Unity con clave y wifi** (plan B) | [T] y V van a Gemini; si tarda más de 5 s o falla, entra el fallback sin que se note. | «Con conexión, Elena genera la respuesta en vivo y validamos el JSON antes de ejecutar nada.» |
| **Unity sin wifi** | Igual que el build: fallback. | «Sin red, el juego sigue igual.» |

## Si algo sale mal (qué hacer y qué decir)

| Problema | Acción | Frase |
| --- | --- | --- |
| Jugador o puerta atascados | Esc → **REINICIAR PISO** (o F5) | «Reiniciar devuelve al checkpoint con el que entraste al piso, con tu vida y munición de entonces.» |
| Te capturan | **REINTENTAR** en «SEÑAL PERDIDA» | «La captura también es parte del diseño: la señal de las gafas se pierde.» |
| Un vídeo no arranca o se ve negro | Esperar ≤ 5 s o pulsar Esc | «Si un clip falla, el juego no se bloquea: sigue sin él.» |
| Elena tarda o no responde a la voz | Usar 1, 2, 3 | «En el build Elena va sin conexión; la voz depende del dictado de Windows.» |
| Muy oscuro en el proyector | F (linterna), subir brillo | «Es terror, pero la interfaz tiene alto contraste para estos casos.» (Opciones › Accesibilidad) |
| El build se cierra | Plan B: Unity → `Systems_Persistent.unity` → Play | — |

## Qué miente el HUD y qué revela el giro

| El HUD dice | La verdad (giro final) |
| --- | --- |
| `100% ILESO`, `NO INFECTADO`, ♥ 72 BPM | `98.4% INFECTADO`, `FASE TERMINAL` |
| Elena «verificada» con sello BIO-ID | El protocolo activo era un «engaño sensorial para escolta de VIP (Elena)» |
| La realidad aumentada ayuda | «Realidad aumentada correctiva: ocultando venas negras», «supresión de dolor: activa» |
| Notificaciones con BIO-ID son fiables | Las falsas (naranja, cursiva, sin sello, onda irregular) eran del infectado imitador |

## Cinemáticas

| Clip (`Assets/StreamingAssets/Cinematics/`) | Cuándo suena |
| --- | --- |
| `intro.mp4` | Al pulsar JUGAR (partida nueva), antes del Piso 12. No suena al volver de los créditos al menú. |
| `floor_transition.mp4` | Al bajar de piso (escaleras, Elena o F5/F8), antes de cargar el siguiente. Si existen `floor_transition_to_06.mp4` o `floor_transition_to_01.mp4`, se usan para ese piso. |
| `ending.mp4` | Tras la última línea de Elena en el giro; al acabar (o saltarlo) salen los créditos. |

- **Saltar:** Esc, Espacio, Intro o clic, a partir de 0,5 s; el aviso aparece al segundo. Esc no abre la pausa durante un clip.
- **Durante el clip** el juego queda en pausa (infectados, forcejeo y audio) y el HUD se oculta.
- **Si falta un clip** (o está roto, o tarda más de 5 s en prepararse) el juego sigue sin él. Comprobar antes del build: **Nexo › Cinematics › Validate**.
- Los vídeos no están en Git: se copian en `Assets/StreamingAssets/Cinematics/` **antes** de **Nexo › Build › Windows** (o después, a mano, en `NexoProtocoloSombra_Data/StreamingAssets/Cinematics/`).

## Atajos de presentación

El build de entrega (**Nexo › Build › Windows (entrega: Elena offline + atajos de demo)**) los activa solo durante el build y deja el repositorio como estaba. En el Editor siguen desactivados (`EnableDemoShortcuts = false` en `PF_Player`); para probarlos ahí, marcar la casilla en el prefab y no subir el cambio.

| Tecla | Efecto |
| --- | --- |
| F5 | Siguiente piso (pasa por la cinemática de transición). |
| F6 | Vida completa. |
| F7 | Munición llena (7 + 35). |
| F8 | Baja hasta el Piso 1 y deja al jugador frente al BioScanner (una transición por piso; se pueden saltar). |

## Limitaciones conocidas

- Pipeline Built-in: la visión térmica es una capa de UI (tinte cian y viñeta), no un postproceso.
- Elena se ve como retrato holográfico en la radio del HUD y como figura (niña, modelo sustituto Ch33 con colores propios) tras el cristal de la sala de servidores del Piso 12; desaparece al salir de la sala.
- Voz de Elena: 28 líneas con Gemini TTS y 23 con la voz de Windows (mismo volumen y filtro; cambia el timbre). Ver `docs/ELENA_VOICE_REGEN.md`.
- Infectados sin ragdoll: caídas y derribos procedurales.
- Mando: menús por eventos de UI Toolkit; en juego, Interactuar y Recargar comparten el botón oeste y la linterna (F) no tiene botón [verificar en mando físico].
- En Opciones › Video solo se aplican resolución, modo de pantalla y volumen; calidad, sombras, AA, brillo y VSync son de muestra.
- Voz (V) no probada en esta sesión con micrófono real [verificar en el ensayo].
