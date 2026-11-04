# Cómo ejecutar NEXO: PROTOCOLO SOMBRA

## Requisitos

- Windows 10 u 11 de 64 bits.
- Tarjeta gráfica compatible con DirectX 11.
- Unos 400 MB libres (el juego descomprimido ocupa unos 250 MB más los vídeos).
- Teclado y ratón. Altavoces o auriculares recomendados (el sonido avisa de los infectados).
- No necesita internet: en el build, Elena responde con diálogos escritos por el equipo (modo sin conexión).

## Ejecutar

1. Descomprime `NexoProtocoloSombra_Windows.zip` en una carpeta normal (por ejemplo, el Escritorio). **No lo ejecutes desde dentro del zip.**
2. Abre `NexoProtocoloSombra_Windows\NexoProtocoloSombra.exe`.
3. Si Windows muestra «Windows protegió tu PC» (SmartScreen): **Más información → Ejecutar de todas formas**. Es porque el ejecutable no está firmado.
4. Arranca en **ventana de 1920 × 1080**. Para pantalla completa pulsa **Alt + Intro** o ve a **Opciones › Video › Modo de pantalla**. En un proyector, si la imagen se ve oscura, sube el brillo del proyector o del portátil: el juego es de terror pero debe leerse.
5. Deja correr el arranque de NexoOS, pulsa cualquier tecla y elige **JUGAR**.

## Controles

| Acción | Tecla |
| --- | --- |
| Moverse / correr | WASD / Shift izquierdo |
| Mirar | Ratón |
| Agacharse | Ctrl izquierdo o C |
| Interactuar (puertas, terminales, notas, escondites, BioScanner) | E |
| Linterna / visión térmica | F |
| Disparar / apuntar / recargar | Clic izquierdo / clic derecho / R |
| Forcejeo cuando te agarran | Espacio |
| Preguntas rápidas a Elena | 1, 2, 3 |
| Hablar con Elena por voz | Mantener V (ver «Voz» abajo) |
| Rueda de órdenes | Mantener Q |
| Red de cámaras (con una terminal abierta) | C |
| Ver el objetivo | Tab |
| Pausa (reiniciar piso, opciones, salir) | Esc o P |
| Menús | ↑ ↓ mover, ← → cambiar valor, Intro confirmar, Esc atrás, Q / E pestañas |

## Atajos de presentación (solo en el build de entrega)

El build de entrega se genera con estos atajos activados como salida de emergencia. En el proyecto de Unity están desactivados.

| Tecla | Efecto |
| --- | --- |
| F5 | Pasar al piso siguiente (con su cinemática de transición) |
| F6 | Vida completa |
| F7 | Munición llena |
| F8 | Bajar hasta el Piso 1 y quedar frente al BioScanner |

## Cinemáticas (vídeos)

- Están en `NexoProtocoloSombra_Data\StreamingAssets\Cinematics\` (`intro.mp4`, `floor_transition.mp4`, `ending.mp4`).
- **Saltar un vídeo:** Esc, Espacio, Intro o clic (a partir de medio segundo; aparece el aviso «Esc / Espacio: saltar»).
- **Si un vídeo no se reproduce** (pantalla negra, sin códec, archivo dañado): el juego sigue solo en un máximo de 5 segundos, sin errores. Si quieres evitarlo del todo, cierra el juego y borra o renombra ese `.mp4`: el juego funciona igual sin él.
- **Jugar sin vídeos:** borra o mueve la carpeta `Cinematics`. Nada más cambia.
- Formato que funciona seguro: MP4 H.264 + AAC. Los vídeos HEVC/H.265 pueden no reproducirse en Windows sin el códec instalado.

## Elena y la voz

- En el build, Elena funciona **sin conexión**: las preguntas **1, 2 y 3** responden siempre. En el Piso 6, la pregunta **2** abre la puerta de las escaleras: es el camino previsto.
- **Voz (V):** usa el dictado de Windows si está activado (*Configuración › Privacidad › Voz › Reconocimiento de voz en línea*). Si no hay micrófono, permiso o dictado, aparece un subtítulo que remite a las preguntas 1-3; el juego no se interrumpe.

## Si algo falla

| Problema | Qué hacer |
| --- | --- |
| Te quedas atascado (puerta, jugador bloqueado) | Esc → **REINICIAR PISO** (vuelves al inicio del piso con la vida y munición con que llegaste). En el build de entrega también vale F5. |
| Te capturan | En «SEÑAL PERDIDA» elige **REINTENTAR**. |
| No se ve nada / muy oscuro | Linterna (F), brillo del monitor o proyector; en el Piso 6, tras el apagón, usa el diferencial del pasillo. |
| La ventana no cabe en la pantalla | Alt + Intro (pantalla completa) u Opciones › Video › Resolución 1600 × 900 o 1280 × 720. |
| El juego no arranca | Comprueba que la carpeta `NexoProtocoloSombra_Data` está junto al `.exe`. El registro está en `%USERPROFILE%\AppData\LocalLow\Equipo NEXO\NEXO - PROTOCOLO SOMBRA\Player.log`. |
| Último recurso | Abrir el proyecto en Unity 6000.6.7f1, escena `Assets/_Project/Scenes/Persistent/Systems_Persistent.unity`, Play. |
