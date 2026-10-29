# Justificación de diseño — NEXO: PROTOCOLO SOMBRA

## Idea central

En NEXO, la interfaz no solo informa: **narra, protege y miente**. El HUD NexoOS es diegético porque forma parte de las gafas de Alex; por eso puede convertirse en un narrador no confiable durante el giro narrativo.

## Heurísticas de Nielsen

| Heurística | Aplicación |
| --- | --- |
| Visibilidad del estado | Vitales, ruido, batería, confianza y estado «Elena está escribiendo…». |
| Correspondencia con el mundo real | Lenguaje de gafas corporativas, radio, esclusa y bioseguridad. |
| Control y libertad | Rueda cancelable, interacción contextual y terminal sin límite de tiempo. |
| Consistencia | Cuatro paneles periféricos, componentes compartidos y tokens semánticos. |
| Prevención de errores | Acciones contextuales desactivadas cuando no aplican; validación de comandos de Elena. |
| Reconocer antes que recordar | Icono + texto, prompts con teclas y tres preguntas rápidas. |
| Flexibilidad y eficiencia | Atajos 1–2–3, rueda con Q y controles remapeables. |
| Minimalismo | Centro de pantalla libre y divulgación progresiva. |
| Recuperación de errores | Fallo remoto de Elena degrada a fallback local sin bloquear la partida. |
| Ayuda | Tutorial integrado en el piso 12 y documentación de NexoOS. |

La visibilidad del estado se subvierte deliberadamente en la narrativa: NexoOS reporta salud falsa, pero deja pistas para que el jugador atento pueda dudar del sistema.

## Carga cognitiva

- Máximo tres acciones rápidas hacia Elena y seis sectores en la rueda.
- HUD agrupado en vitales, radio, ruido/batería y notificaciones.
- Una notificación activa salvo emergencia.
- Diálogos breves y lenguaje infantil consistente.

## Accesibilidad

WCAG 2.2 se emplea por analogía, no como certificación web:

- Texto normal con contraste objetivo mínimo 4,5:1 y componentes 3:1.
- Escala de texto mediante clases `ui-scale-*`.
- Modos deuteranopía, protanopía y tritanopía con paleta Okabe-Ito.
- Alto contraste, reducción de movimiento y subtítulos.
- Navegación por teclado y mando; foco visible.
- Los mensajes falsos se distinguen por cuatro canales: sello ausente, onda irregular, tipografía alterada y color; nunca solo por color.
- El glitch se limita a tres destellos espaciados y desaparece con «Reducir movimiento».

## Taxonomía de interfaz

- **Diegética:** HUD de las gafas, videollamada y terminales.
- **Espacial:** rutas seguras y pings sobre el mundo.
- **Meta:** viñeta, daño y glitch.
- **No diegética:** menú principal, accesibilidad y pausa.

La especificación completa y su trazabilidad de implementación están en `docs/TECHNICAL_BIBLE.md`.
