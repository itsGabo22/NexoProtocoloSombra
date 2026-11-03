# Guion de demo — NEXO: PROTOCOLO SOMBRA (5–7 min)

Para la presentación del 2026-11-04. Ejecuta el build de Windows (`Builds/NexoProtocoloSombra_Windows/NexoProtocoloSombra.exe`); el build trae a Elena en modo sin conexión (fallback), así que no depende de internet. Si el build falla, abre `Systems_Persistent.unity` en el Editor y pulsa Play.

## Controles (verificados en `Input/NexoControls.inputactions` y en el código)

| Acción | Teclado/ratón | Dónde se define |
| --- | --- | --- |
| Moverse / correr | WASD / Shift izquierdo | `NexoControls` (Move, Sprint) y `PlayerMovement` |
| Mirar | Ratón | `NexoControls` (Look) |
| Agacharse | Ctrl izquierdo o C | `PlayerMovement` |
| Interactuar | E | `NexoControls` (Interact) |
| Linterna / visión térmica | F | `FlashlightSystem` |
| Disparar / apuntar / recargar | Clic izquierdo / clic derecho / R | `PlayerWeapon` |
| Forcejeo (cuando te agarran) | Espacio | `StruggleQTE` |
| Preguntas rápidas a Elena | 1, 2, 3 | `NexoControls` (UI/QuickQuestion1-3) |
| Hablar con Elena por voz | Mantener V | `ElenaVoiceInput` |
| Rueda de órdenes | Mantener Q | `NexoControls` (OrderWheel, interacción Hold) |
| Ver objetivo | Tab | `ObjectiveReminder` |
| Pausa | Esc o P | `NexoControls` (Pause) |
| Menús | ↑ ↓ mover, ← → cambiar valor, Enter confirmar, Esc atrás, Q/E pestañas | `MenuNavigator` |

## Guion

| Tiempo | Qué hacer | Qué decir |
| --- | --- | --- |
| 0:00–0:40 | Arrancar. Dejar correr el despertar y el escaneo biométrico; luego cualquier tecla. | «NexoOS son las gafas de Alex. Todo lo que veis es su interfaz, y esa interfaz va a ser el narrador no fiable del juego.» |
| 0:40–1:30 | Menú principal → OPCIONES → pestaña ACCESIBILIDAD (E). Activar alto contraste y reducir movimiento; mostrar el ejemplo de notificación real frente a falsa. Volver con Esc. | «Escala, daltonismo Okabe-Ito, alto contraste, reducir movimiento y subtítulos se aplican al instante. Fijaos en las 4 pistas que distinguen una notificación falsa: aunque tengas daltonismo quedan 3.» |
| 1:30–3:00 | JUGAR. Piso 12: leer el HUD (vitales «ILESO», ruido, radio de Elena, batería). Pulsar F para la visión térmica. Disparar a los 2 infectados (clic derecho para apuntar). | «El HUD dice que estoy ileso. El disparo hace ruido y atrae a los infectados; si me agarran hay un forcejeo con Espacio. La persiana de la escalera solo se abre cuando el piso está limpio.» |
| 3:00–3:45 | Pulsar 1, 2, 3 para hablar con Elena (mostrar la videollamada, el sello BIO-ID y la barra de confianza). Opcional: mantener V. Bajar por la escalera [E]. | «Elena responde con Gemini 2.5 Flash en un JSON estricto: diálogo, emoción y una acción de UI que validamos antes de ejecutar. Sin red, entra un fallback escrito por nosotros.» |
| 3:45–4:30 | Piso 6: pulsar 2 («abre la puerta»). Elena abre `F06_Door_Stairs`. Mostrar pausa (Esc) con la etiqueta del piso y REINICIAR PISO. Bajar. | «Vida y munición se conservan entre pisos; reiniciar te devuelve al checkpoint con el que entraste.» |
| 4:30–5:45 | Piso 1: poner la mano en el BioScanner [E]. Dejar correr el giro sin tocar nada. | «El escáner dice la verdad: 98,4 % infectado. El HUD se rompe, el tema pasa a rojo colmena y Elena confiesa. Todo lo que el HUD decía era mentira.» |
| 5:45–6:30 | Créditos → Esc → menú principal. | «Los créditos solo incluyen datos verificados: packs, licencias y fuentes OFL.» |

## Qué miente el HUD y qué revela el giro

| El HUD dice | La verdad (giro final) |
| --- | --- |
| `100% ILESO`, ♥ 72 BPM (vitales del HUD) | `98.4% INFECTADO`, `FASE TERMINAL` |
| Elena «verificada» con sello BIO-ID | El protocolo activo era un «engaño sensorial para escolta de VIP (Elena)» |
| La realidad aumentada ayuda | «Realidad aumentada correctiva: ocultando venas negras», «supresión de dolor: activa» |
| Notificaciones con BIO-ID son fiables | Las falsas (naranja, cursiva, sin sello, onda irregular) eran del infectado imitador |

## Plan de contingencia

- **Elena sin conexión o lenta:** es lo esperado en el build (fallback forzado). Las preguntas 1–3 responden siempre y [2] abre la puerta del Piso 6. Si la voz (V) no reconoce, usa 1–3.
- **El juego se atasca** (puerta, carga, jugador bloqueado): Esc → REINICIAR PISO. Si te capturan, en «SEÑAL PERDIDA» elige REINTENTAR.
- **El build se cierra o no arranca:** abrir el proyecto en Unity 6000.6.7f1, escena `Assets/_Project/Scenes/Persistent/Systems_Persistent.unity`, Play.
- **El infectado no se ve:** falta el pack `Pack_ZombieMale_AAB` en esa máquina; la IA funciona igual (se oye y ataca). Ver «Limitaciones».

## Limitaciones conocidas

- En la máquina de Gabriel falta `Pack_ZombieMale_AAB`: el infectado no tiene malla ni animación y la consola repite «Animator is not playing an AnimatorController». Con el pack de Carlos se ve normal.
- Pipeline Built-in: el Volume `VP_Thermal` no tiene efecto; la visión térmica es una capa UI (tinte cian y viñeta).
- Los pisos 6 y 1 no tienen enemigos (decisión de ritmo).
- Mando: navegación de menús por eventos de UI Toolkit; en juego, Interactuar y Recargar comparten el botón oeste y F (térmica) no tiene binding de mando [verificar en mando físico].
- El arco del escaneo biométrico de arranque es estático.
- Dictado por voz de Elena sin probar en esta sesión [verificar en el ensayo].
