# NexoOS: especificación de diseño para UI Toolkit

Fuente de verdad visual (solo lectura, no accesible desde el repo):
- Sistema de diseño: https://claude.ai/artifact/SZ1F8ovgHJNbGXWz8fRoiZ
- Lienzo de pantallas: https://claude.ai/artifact/4BvY7JeFJBpugiYccBXtYp

Este documento recoge todo lo necesario para implementar las pantallas en UXML/USS sin abrir esos enlaces. Resolución de referencia: **1920×1080**. Idioma de la interfaz: español.

## 0. Alcance y reglas

- Entra: UXML y USS de las pantallas 1, 2, 3, 4, 6, 7, 8, 9 y 10 (sección 6) y de los componentes de la sección 5.
- No entra: controladores C#, escenas, prefabs, `PanelSettings` ya existentes, volúmenes URP, audio, cambios al HUD existente (pantalla 5: ya existe `HUD_Biometric.uxml`).
- Reutiliza antes de crear: si el HUD ya define un panel, una notificación real/falsa, el espectrograma o las clases `theme-nexo`, `theme-hive`, `a11y-hc`, `ui-scale-lg`, `ui-scale-xl`, úsalos tal cual.
- No cambies los valores de variables USS que ya existen. Si un valor de esta especificación difiere del existente, repórtalo y mantén el existente para el HUD.
- Cada elemento interactivo lleva `name` único en kebab-case con prefijo de pantalla (por ejemplo `menu-item-play`, `opt-row-quality`), `focusable="true"` y `tabindex` en orden visual, para que un controlador se enlace después.

## 1. Tokens de color

Tres temas: `nexo` (por defecto), `hive` (rojo), `hc` (alto contraste, se aplica encima de cualquiera). Nombre sugerido de variable USS: `--c-<token>`; respeta los nombres que ya existan (por ejemplo `--c-fake`).

| token | nexo | hive | hc | uso |
|---|---|---|---|---|
| bg | #0b1220 | #12060a | #000000 | fondo de pantalla completa |
| surface | #162033 | #1d0b10 | #000000 | paneles y filas |
| surface-raised | #1f2c45 | #2c1119 | #0a0a0a | fila enfocada, botón secundario, pistas de slider |
| ink | #e6edf6 | #f7eaea | #ffffff | texto principal |
| ink-muted | #a3b3c8 | #d0a9ad | #e6e6e6 | texto secundario, ítems sin foco |
| accent | #22d3ee | #ff4d5a | #00e5ff | foco, datos vivos, botón primario |
| on-accent | #05222b | #1a0508 | #000000 | texto sobre relleno accent |
| line | #5b7a93 | #a85a63 | #ffffff | bordes de control y panel |
| line-faint | #2a3a52 | #3a1a22 | #6b6b6b | separadores decorativos |
| focus | = accent | = accent | #ffe14d | anillo de foco |
| fake | #ff9f43 | #ffb347 | #ffb000 | solo notificación falsa |
| status-ok | #5eead4 | #7ee0d0 | #5cf2ff | estado correcto (con palabra) |
| status-warn | #fbbf24 | #ffc857 | #ffd23f | aviso (con palabra) |
| status-danger | #ff8a8a | #ff8a8a | #ff9a9a | error (con palabra) |
| scrim | rgba(5,9,16,0.72) | rgba(12,3,6,0.72) | rgba(0,0,0,0.90) | velo sobre la escena 3D |

En `hc`, todo panel y control lleva borde de 2px en `line`.

## 2. Tipografía

Fuentes (todas OFL; el usuario las descarga de Google Fonts y las deja en `Assets/_Project/UI/Fonts/`): Barlow Condensed Medium (500), SemiBold (600), Bold (700); IBM Plex Sans Regular (400); IBM Plex Mono Medium (500). UI Toolkit no elige pesos: cada clase apunta al archivo del peso que necesita.

| estilo | familia | tamaño / alto | peso | espaciado | uso |
|---|---|---|---|---|---|
| display-xl | Barlow Condensed | 112 / 108 | 700 | 0.02em | arranque, derrota, cifra del escaneo |
| title | Barlow Condensed | 64 / 68 | 600 | 0.02em | título de pantalla |
| menu-item-focus | Barlow Condensed | 56 / 60 | 600 | 0.02em | ítem con foco |
| menu-item | Barlow Condensed | 40 / 48 | 500 | 0.02em | ítem sin foco |
| heading | Barlow Condensed | 36 / 42 | 600 | 0.01em | encabezado de panel |
| tab | Barlow Condensed | 32 / 40 | 500 (activa 600) | 0.02em | pestañas |
| button | Barlow Condensed | 28 / 36 | 600 | 0.04em | botones |
| body | IBM Plex Sans | 24 / 34 | 400 | 0 | descripciones, valores, pistas |
| body-sm | IBM Plex Sans | 20 / 28 | 400 | 0 | notas, mensajes de notificación |
| label | IBM Plex Mono | 18 / 24 | 500 | 0.08em | etiquetas, teclas, estados |
| readout | IBM Plex Mono | 28 / 32 | 500 | 0 | cifras |

Mínimo absoluto: 18px.

**Mayúsculas:** USS no tiene `text-transform` ni `line-height`. Escribe en MAYÚSCULAS el texto de los ítems de menú, títulos, etiquetas y botones directamente en el UXML, y obtén la altura de línea con `height`/`min-height` del contenedor.

## 3. Espaciado, formas y tamaños

- Espaciado: 4, 8, 12, 16, 24, 32, 48, 64 px. Margen seguro de pantalla: 64px.
- Radios: 2 (teclas, marcadores), 4 (paneles, botones, filas), 8 (diálogos).
- Bordes: 1px (paneles en nexo/hive), 2px (foco, controles activos, todo en hc).
- Alto de fila de ajuste 64; alto mínimo de control 48.
- Movimiento: cambio de foco 120ms, salida suave (`transition-duration: 0.12s`). "Reducir movimiento" lleva las duraciones a 0.

## 4. Restricciones de USS (verifica cada propiedad en la documentación de Unity 6 antes de usarla)

No existen: `gap`, `rem`, CSS Grid, `@keyframes`, `box-shadow`, degradados, `backdrop-filter`, `outline`, `line-height`, `text-transform`, `z-index`, pseudo-elementos `::before`/`::after`. Alternativas:
- Separación: `margin`.
- Anillo de foco: un contenedor con borde de 2px en `--c-focus` y 3px de relleno transparente alrededor del control (clase `.focus-ring`), sin `outline`.
- Animación: alternar clases con `transition`.
- Velo degradado y efectos (glitch, scanlines): sprites o shaders; mientras no existan, usa un color plano con alfa.
- Esquinas de panel y marco de lente: elementos hijos con posición absoluta.
- Orden de apilado: orden en la jerarquía.
- Arcos y ondas: requieren `generateVisualContent` (C#); en esta fase se dejan como elementos estáticos marcados con `<!-- TODO controller -->`.

## 5. Componentes

**Panel.** Fondo `surface`, borde 1px `line`, radio 4, relleno 24. Cuatro marcas de esquina de 16×16, borde 2px `accent`, solo dos lados visibles cada una, desplazadas -2px. Etiqueta (`label`, `ink-muted`) a 12px sobre el contenido.

**LensFrame.** Cuatro esquinas en L de 40×40, borde 2px `accent`, a 28px de los bordes de pantalla. Opacidad 100% en menús, 50% en juego. Reglilla superior: 21 marcas de 2px de ancho cada 20px (400px en total), centradas en x=760 a y=28; la central mide 16px de alto y las demás 8px (de y=8 a 16 dentro de la reglilla), `accent` al 60%.

**MenuList.** Filas con número (`label`, ancho 56) + texto. Foco: `menu-item-focus`, `ink`. Sin foco: `menu-item`, `ink-muted`. Margen inferior 12 entre filas. Marcador de foco (en listas sin número): cuadrado de 12×12 `accent` con 16px hasta el texto.

**TabBar.** Barra de ancho completo con borde inferior 1px `line-faint`. Tecla Q a la izquierda y E a la derecha (32×32, borde 2px `line`, radio 2, `label` `ink`). Pestañas centradas, cada una con margen horizontal 24, relleno `12 0 8`, alto mínimo 48, `tab` en `ink-muted`; la activa en `ink`, peso 600 y borde inferior 2px `accent`.

**SettingRow.** Alto 64, relleno horizontal 24, borde 2px transparente y separador inferior 1px `line-faint`. Etiqueta `body` `ink-muted` (flex 1), valor `ink` a la derecha. Con foco: fondo `surface-raised`, borde 2px `focus`, radio 4, etiqueta en `ink`. Variantes: `choice` (flechas `‹` `›` de 24px de ancho, visibles solo con foco), `toggle` (palabra ACTIVADO/DESACTIVADO en `label` + interruptor), `slider` (pista + valor `readout`).
- Interruptor: 56×28, borde 2px, radio 14, margen izquierdo 12. Encendido: fondo y borde `accent`, perilla 20×20 en `on-accent` en left 30. Apagado: borde `line`, fondo `bg`, perilla `ink-muted` en left 2.
- Slider: pista 160×16, borde 2px `line`, radio 2, fondo `bg`, relleno `accent`; valor `readout` en ancho 64, alineado a la derecha, margen izquierdo 12.

**Button.** Alto mínimo 48, relleno horizontal 24, `button`, radio 4, borde 2px. Primario: fondo y borde `accent`, texto `on-accent`. Secundario: fondo `surface-raised`, borde `line`, texto `ink`. Peligro: igual que secundario con borde y texto `status-danger` e ícono de aviso. Deshabilitado: texto y borde `line`.

**SegmentedControl.** Tres botones de 200×64, margen derecho 16, `button` a 28px. Elegido: fondo y borde `accent`, texto `on-accent`, peso 700, ícono de check de 20px a la izquierda. No elegido: fondo `surface-raised`, borde 2px `line`, texto `ink`.

**KeyHint.** Tecla: ancho mínimo 36, alto 36, relleno horizontal 8, borde 2px `line`, radio 2, `label` `ink`. Acción: `body` `ink-muted`, margen izquierdo 8. Separación entre leyendas: 32. Botón de mando: caja de 36×36 con radio 50%.

**NotificationCard (real y falsa).** Reutiliza lo que ya exista en el HUD. Ancho 580, relleno 16/20, fondo `surface`. Real: borde 1px `line`; remitente `ELENA` (`label`, `ink`) + sello `✔ BIO-ID` (`label`, `status-ok`, margen izquierdo 12); mensaje `body-sm` `ink`; onda senoidal. Falsa: borde 2px `fake`, rotación -0.6°, remitente en `fake`, sin sello, mensaje en cursiva, onda irregular. Las cuatro pistas: remitente y borde naranja, falta del sello, cursiva con inclinación, onda irregular.

## 6. Pantallas (todas a 1920×1080; posiciones en px desde la esquina superior izquierda salvo que diga right/bottom)

**Elementos comunes.** Raíz transparente a pantalla completa (la escena 3D se ve detrás). Pista contextual: `body` `ink-muted`, centrada, a bottom 112, y debajo la fila de KeyHint centrada. Etiqueta de versión `NEXOOS V4.2`: `label` `ink-muted`, a left 64 / bottom 64.

### 1. Despertar (arranque)
- Fondo: dos párpados de `#02050b`: el superior de 0 a 400 de alto y el inferior de 680 a 1080. Al terminar la animación, sus alturas pasan a 0 con transición de 1.2s.
- LensFrame al 50%.
- Bloque de progreso centrado a top 760: etiqueta `CALIBRANDO ÓPTICA` (`label`, `ink-muted`) con 16 de margen inferior; fila con pista de 360×4 (`line-faint`), relleno `accent` al 38% y valor `38%` (`label`, `ink`, ancho 72, margen izquierdo 16).
- Texto inferior izquierdo `NEXOOS V4.2 · PISO 12` y derecho (right 64 / bottom 64) `LOS OJOS SE ABREN`, ambos `label` `ink-muted`.

### 2. Escaneo biométrico
- Velo `scrim` plano al 62% (rgba(5,9,16,0.62)) sobre toda la pantalla. LensFrame al 100% y reglilla.
- Etiqueta centrada a top 196: `ESCANEO BIOMÉTRICO` (`label`, `ink-muted`).
- Anillo en left 740 / top 252, caja de 440×440: círculo de 400 de diámetro, borde 2px `line-faint`, y encima un círculo igual con borde de 6px `accent` (estático por ahora; marcar TODO para el arco al 99%).
- Centro del anillo: `99%` (`display-xl`, `ink`) y debajo `SANO` (Barlow Condensed 600, 36/42, espaciado 0.06em, `ink-muted`).
- Bajo el anillo, a top 728, centrado: `NO INFECTADO` (`body`, `ink`) y `✔ BIO-ID VERIFICADO` (`label`, `status-ok`).
- Columna izquierda en left 160 / top 420 / ancho 380: `RITMO CARDÍACO` (`label` `ink-muted`) + `72 BPM` (`readout` `ink`), margen 32, `ÓPTICA` + `OK` (`readout` `status-ok`).
- Columna derecha en right 160 / top 420 / ancho 380, texto a la derecha: `ENLACE SEGURO` + `OK` (`status-ok`), margen 32, `BIO-ID` + `VERIFICADO` (`ink`).
- Pista inferior: `Escaneo completo. Ajustando la interfaz a tu vista.`

### 3. Calibración óptica (solo primer arranque)
- Velo `scrim` plano al 74%. LensFrame.
- Left 160 / top 96: `PRIMER ARRANQUE · PASO 2 DE 4` (`label` `ink-muted`). Left 160 / top 136: `CALIBRACIÓN ÓPTICA` (`title`, `ink`).
- Pasos en left 160 / top 300 / ancho 560: `01 CONTRASTE` (`menu-item` `ink-muted`, a la derecha `✔ LISTO` `label` `status-ok`), `02 ESCALA` (con foco: `menu-item-focus`, `ink`, marcador cuadrado), `03 COLOR`, `04 MOVIMIENTO`.
- Tarjeta en left 800 / top 192 / ancho 960, Panel con relleno 40: `ESCALA DE LA INTERFAZ` (`heading`), margen 12, `Elige el tamaño con el que lees cómodo. Puedes cambiarlo después en Opciones.` (`body` `ink-muted`), margen 32, SegmentedControl con `100%`, `125%` (elegido, con anillo de foco), `150%`, margen 32.
- Caja de vista previa: relleno 24, fondo `bg`, borde 1px `line-faint`, radio 4. Contiene la etiqueta `VISTA PREVIA A 125%` (`label` `ink-muted`) y un Panel de 420 de ancho con etiqueta de 22px (`ESTADO BIOMÉTRICO`), valor `99% SANO` de 35px y `NO INFECTADO` de 25px.
- Debajo (margen 32): botones `CONTINUAR` (primario) y `OMITIR` (secundario), 16 de separación.
- KeyHint inferior (bottom 64): `← →` Cambiar tamaño, `Enter` Confirmar, `Esc` Omitir calibración.

### 4. Menú (hub)
- Velo lateral: mientras no exista el sprite degradado, un rectángulo de left 0 a 1056 de ancho con rgba(5,9,16,0.55). LensFrame al 100% y reglilla.
- Left 160 / top 128: `NEXO: PROTOCOLO SOMBRA` (`label` `ink-muted`).
- Regla vertical en left 131 / top 216, 2×396, `line-faint`; segmento de foco en left 130 / top 240, 4×60, `accent`.
- Lista en left 160 / top 240 / ancho 640: `01 JUGAR` (foco), `02 OPCIONES`, `03 ACCESIBILIDAD`, `04 CRÉDITOS`, `05 SALIR`.
- Línea del foco: left 160 / top 304, 520×2, `accent` al 60%; marca final en left 678 / top 298, 2×14.
- Panel biométrico en right 64 / top 64 / ancho 320: `ESTADO BIOMÉTRICO` / `99% SANO` (`readout`) / `NO INFECTADO` (`body-sm` `ink-muted`).
- Panel de Elena en right 64 / bottom 176 / ancho 420, relleno 24: `ELENA · CANAL SEGURO` (`label` `ink-muted`), `✔ BIO-ID VERIFICADO` (`label` `status-ok`), `En espera` (`body-sm` `ink`).
- Pistas según el ítem: JUGAR `Inicia el protocolo en el Piso 12.`; OPCIONES `Ajusta audio, video y controles.`; ACCESIBILIDAD `Contraste, escala, daltonismo y movimiento.`; CRÉDITOS `Conoce quién hizo el juego.`; SALIR `Cierra el juego.`. KeyHint: `↑ ↓` Mover, `Enter` Confirmar.

### 6. Opciones: video
- TabBar en left 160 / right 160 / top 64, con `VIDEO` activa: `AUDIO`, `VIDEO`, `CONTROLES`, `ACCESIBILIDAD`.
- Lista en left 160 / top 200 / ancho 900, contenedor Panel sin relleno interno. Filas: `Resolución` = `1920 × 1080`; `Modo de pantalla` = `Pantalla completa`; `Calidad de imagen` = `‹ Alta ›` (con foco); `Sombras` (interruptor, ACTIVADO); `Suavizado de bordes` = `TAA`; `Brillo` (slider al 60%, valor `60`); `Sincronización vertical` (interruptor, DESACTIVADO).
- Panel derecho en left 1180 / top 200 / ancho 580: `CALIDAD DE IMAGEN` (`heading`), 12, `Ajusta el detalle de texturas y sombras. Una calidad menor mejora el rendimiento.` (`body` `ink-muted`), 24, caja de 580×300 (`surface`, borde 1px `line`, radio 4) con `[VISTA PREVIA DE LA ESCENA]` centrado (`label` `ink-muted`; en el juego será una cámara o `RenderTexture`), 24, `IMPACTO ESTIMADO` (`label` `ink-muted`), 12, dos filas: nombre `body-sm` ancho 200 + pista (alto 12, borde 2px `line`, radio 2) con relleno `accent` al 60% / 85% + palabra `label` ancho 96 a la derecha: `Procesador` `MEDIO`, `Memoria de video` `ALTO`.
- KeyHint (bottom 64): `← →` Cambiar valor, `Esc` Atrás.

### 7. Opciones: accesibilidad
- Mismo TabBar con `ACCESIBILIDAD` activa. Misma posición de lista. Filas: `Alto contraste` (interruptor, DESACTIVADO); `Escala de la interfaz` = `‹ 125% ›` (con foco); `Modo daltonismo` = `Deuteranopia`; `Reducir movimiento` (interruptor, DESACTIVADO); `Subtítulos` (interruptor, ACTIVADO); `Tamaño de subtítulos` = `Mediano`.
- Panel derecho en left 1180 / top 200 / ancho 580: `ESCALA DE LA INTERFAZ` (`heading`), 12, `Agranda textos y paneles de todo el HUD. Se aplica al instante.` (`body` `ink-muted`), 24.
- `EJEMPLO · NOTIFICACIÓN REAL` (`label` `ink-muted`, margen 12) y NotificationCard real con `Por el pasillo de la izquierda no hay nadie. Ve agachado, ¿sí?`; margen 20.
- `EJEMPLO · NOTIFICACIÓN FALSA` y NotificationCard falsa con `Alex, ven al cuarto de servidores. Es seguro.`; margen 20.
- Leyenda (`body-sm` `ink`, número en `label`, margen 4): `1 Remitente y borde naranja.` `2 Falta el sello ✔ BIO-ID.` `3 Texto en cursiva y ligeramente inclinado.` `4 Onda de audio irregular.`; margen 12; `Con cualquier modo de daltonismo siguen visibles las otras tres pistas.` (`body-sm` `ink-muted`).
- KeyHint como en la pantalla 6.

### 8. Pausa
- Velo `scrim` plano. Left 64 / top 64: `PISO 12 · OFICINAS` (`label` `ink-muted`). Panel biométrico en right 64 / top 64.
- Columna centrada a top 232: `PAUSA` (`title`), margen 48, y los ítems centrados: `CONTINUAR` (foco, con marcador cuadrado), `OPCIONES`, `REINICIAR PISO`, `MENÚ PRINCIPAL`.
- Pista `Vuelve a la partida.`; KeyHint: `↑ ↓` Mover, `Enter` Confirmar, `Esc` Continuar.

### 9. Piso reiniciado (tema hive)
- Fondo `bg` del tema hive. Centrado a top 232: `SEÑAL PERDIDA` (`display-xl`, color `accent`) y, a 16 de margen, `PISO REINICIADO` (`title`, `ink-muted`).
- Columna de botones centrada a top 560: `REINTENTAR` (primario, 360 de ancho, anillo de foco) y `MENÚ PRINCIPAL` (secundario, 360 de ancho), 16 de separación.
- Pista `Reintenta desde el inicio del piso.`; KeyHint: `↑ ↓` Mover, `Enter` Confirmar.

### 10. Créditos (tema hive)
- Left 160 / top 112: `MODO DEPURACIÓN ROOT` (`label`, color `accent`). Left 160 / top 152: `CRÉDITOS` (`title`, `ink`).
- Panel biométrico (hive) en right 64 / top 64: `ESTADO BIOMÉTRICO` / `98.4% INFECTADO` / `FASE TERMINAL`.
- Bloque en left 160 / top 288 / ancho 1000, pares etiqueta (`label` `ink-muted`, margen inferior 4) + valor (`body` `ink`, margen inferior 24):
  - `DISEÑO Y DESARROLLO` / `Gabriel Esteban Paz Guerrero · Carlos Julian Benavides Burbano`
  - `MATERIA` / `Diseño de Interfaces · Ingeniería de Software, 5.º semestre`
  - `DIÁLOGOS DE ELENA` / `Gemini 2.5 Flash`
  - `ARMA M1911` / `Nokobot, Modern Guns · Handgun [confirmar licencia]`
  - `PERSONAJE Y ANIMACIONES` / `Mixamo [confirmar licencia]`
  - `EDIFICIO DE OFICINAS Y ENTORNO` / `[Autor del pack y licencia]`
- Pista `Vuelve al menú principal.`; KeyHint: `Esc` Atrás.

## 7. Pendientes que no cubre esta especificación

Controladores C# (navegación, cambio de pestañas con Q/E, guardado de la calibración, transición de los paneles del hub al HUD), escena de arranque con los párpados y la escena 3D, volumen URP de profundidad de campo, sprite del velo degradado, arcos y ondas animadas, sonido de interfaz.
