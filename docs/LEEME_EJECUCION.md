# Cómo ejecutar NEXO: PROTOCOLO SOMBRA

## Requisitos

- Windows 10 u 11 de 64 bits.
- Tarjeta gráfica compatible con DirectX 11.
- Unos 400 MB libres (el juego descomprimido ocupa unos 250 MB más los vídeos).
- Teclado y ratón. Altavoces o auriculares recomendados (el sonido avisa de los infectados).
- **Build offline** (el que se entrega): no necesita internet; Elena responde con diálogos escritos por el equipo.
- **Build con IA real** (solo en el PC de Gabriel): necesita internet; Elena responde con Gemini y, si falla, con el respaldo offline.
- Micrófono opcional para hablar con Elena con **V**.

## Ejecutar

1. Descomprime `NexoProtocoloSombra_Windows.zip` en una carpeta normal (por ejemplo, el Escritorio). **No lo ejecutes desde dentro del zip.**
2. Abre `NexoProtocoloSombra_Windows\NexoProtocoloSombra.exe`.
3. Si Windows muestra «Windows protegió tu PC» (SmartScreen): **Más información → Ejecutar de todas formas**. Es porque el ejecutable no está firmado.
4. Arranca en **ventana de 1920 × 1080**. Para pantalla completa pulsa **Alt + Intro** o ve a **Opciones › Video › Modo de pantalla**. En un proyector, si la imagen se ve oscura, sube el brillo del proyector o del portátil: el juego es de terror pero debe leerse.
5. En la pantalla **«PRESIONA [ENTER] PARA CONTINUAR»** pulsa Intro (o cualquier tecla / clic) y elige **NUEVA PARTIDA** en el menú principal (W/S o ↑/↓ y Intro, o el ratón). Tras los créditos finales vuelves a este menú.

## Controles

| Acción | Tecla |
| --- | --- |
| Moverse / correr | WASD / Shift izquierdo |
| Mirar | Ratón |
| Agacharse | Ctrl izquierdo o C |
| Interactuar (puertas, terminales, notas, escondites, BioScanner) | E |
| Linterna / visión térmica | F |
| Disparar / apuntar / recargar | Clic izquierdo / clic derecho / R |
| Saltar | Espacio |
| Forcejeo cuando te agarran | Espacio (o G para romper el agarre) |
| Empujar a un infectado que tienes delante | G (cada 1,5 s) |
| Cuchillo (lo suelta el primer patrullero del Piso 12) | X; por la espalda y sin que te vea, ejecución silenciosa |
| Leer una nota / cerrarla | E · E o Esc |
| Diario de notas leídas | J |
| Escribir a Elena | T (Intro envía, Esc cancela) |
| Preguntas rápidas a Elena | 1, 2, 3 |
| Hablar con Elena por voz | Mantener V (ver «Voz» abajo) |
| Rueda de órdenes | Mantener Q |
| Red de cámaras (con una terminal abierta) | C |
| Ver el objetivo | Tab |
| Pausa (reiniciar piso, opciones, salir) | Esc o P |
| Menús | ↑ ↓ mover, ← → cambiar valor, Intro confirmar, Esc atrás, Q / E pestañas |

- **Dentro de un escondite** la mirada queda limitada a ±70° a los lados y ±35° arriba/abajo.
- **Caer** por un hueco (p. ej. el de los ascensores del Piso 12) cuesta 1 vida: fundido y reapareces en el último punto de control. **Botiquines** (+1 vida): dos por piso; a veces los suelta un infectado.
- **Opciones › Accesibilidad › Velocidad de lectura** (Normal / Lento) alarga el tiempo de subtítulos y mensajes.

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

Detalle técnico completo: [`ELENA_AI.md`](ELENA_AI.md).

- **Preguntas rápidas 1, 2 y 3:** responden siempre (con o sin internet). En los pisos 12 y 6, la **2** abre la puerta de las escaleras: es el camino previsto, también con Gemini en vivo.
- **Escribir (T):** escribe la pregunta, **Intro** envía, **Esc** cancela.
- **Voz (mantener V):** habla mientras mantienes V (máximo 8 s) y suelta. Verás **ESCUCHANDO...** y **PROCESANDO...**; tu frase aparece como «TÚ: …» y Elena responde.
  - Con clave (Editor o build con IA real): el micrófono se graba con Unity y Gemini transcribe en español. **No hace falta** el ajuste de Windows de voz en línea.
  - Sin clave (build offline): se usa el dictado de Windows si está activado (*Configuración › Privacidad y seguridad › Voz › Reconocimiento de voz en línea*).
  - Sin micrófono o sin audio: aparece «No hay micrófono: usa 1/2/3/T» o «No te escuché, repite o usa 1/2/3/T». El juego nunca se bloquea.
  - Comprueba antes de la demo: *Configuración › Privacidad y seguridad › Micrófono* permitido para aplicaciones de escritorio y el micrófono correcto como predeterminado.
- **Marcador de origen:** bajo el diálogo de Elena aparece **«IA: Gemini»** (respuesta generada en vivo) o **«IA: respaldo»** (diálogo escrito por el equipo). Si Gemini tarda más de 8 s, responde el respaldo.
- **Voz de Elena:** 51 líneas grabadas con tratamiento de radio; las respuestas de Gemini se sintetizan al momento (si no llegan a tiempo, solo subtítulo).

## Generar los builds (Unity, menú Nexo › Build)

| Menú | Uso | Clave |
| --- | --- | --- |
| **Windows (entrega: Elena offline)** | Build para entregar/compartir. Fuerza el respaldo y activa los atajos F5-F8 | No incluye la clave |
| **Windows (entrega: IA real con respaldo)** | Build para la demo en el PC de Gabriel con Gemini en vivo | **Incluye la clave**: no lo subas ni lo compartas |
| Windows (Elena offline, sin atajos de demo) | Igual que el offline, sin F5-F8 | No incluye la clave |

Los tres escriben en `Builds/NexoProtocoloSombra_Windows/` (+ `.zip`), carpeta ignorada por Git. **Antes de compilar, renombra o copia esa carpeta si quieres conservar el build anterior.** En batchmode (Editor cerrado) se puede elegir otra carpeta:

```
Unity.exe -batchmode -projectPath <proyecto> -executeMethod Nexo.EditorTools.NexoReleaseBuilder.BuildBatch -nexoFolder NexoProtocoloSombra_Windows_<fecha> [-nexoRealAi] -logFile Logs/build.log
```

### Build con IA real (Gabriel, a mano)

1. Con Unity cerrado, crea o comprueba `Assets/_Project/Data/Secrets/ElenaSecrets.asset` con la clave de Gemini (carpeta ignorada por Git).
2. Abre Unity, selecciona `Data/ElenaConfig.asset` y arrastra `ElenaSecrets` a su campo **Secrets**.
3. **Nexo › Checks › Elena Live (Gemini)**: debe terminar con `RESULTADO: OK` y respuestas «IA: Gemini» (ver `Logs/NexoElenaLiveCheck.txt`).
4. **Nexo › Build › Windows (entrega: IA real con respaldo)** → acepta el aviso. Al terminar, el builder restaura los archivos que tocó.
5. Prueba el `.exe` con internet: pregunta **1** → debe verse «IA: Gemini» bajo el diálogo.
6. Antes de cualquier commit: `git restore Assets/_Project/Data/ElenaConfig.asset` (el enlace a la clave nunca se sube). No copies el build con clave a USB compartidos ni lo subas a la nube.

## Si algo falla

| Problema | Qué hacer |
| --- | --- |
| Te quedas atascado (puerta, jugador bloqueado) | Esc → **REINICIAR PISO** (vuelves al inicio del piso con la vida y munición con que llegaste). En el build de entrega también vale F5. |
| Te capturan | En «SEÑAL PERDIDA» elige **REINTENTAR**. |
| No se ve nada / muy oscuro | Linterna (F), brillo del monitor o proyector; en el Piso 6, tras el apagón, usa el diferencial del pasillo. |
| La ventana no cabe en la pantalla | Alt + Intro (pantalla completa) u Opciones › Video › Resolución 1600 × 900 o 1280 × 720. |
| El juego no arranca | Comprueba que la carpeta `NexoProtocoloSombra_Data` está junto al `.exe`. El registro está en `%USERPROFILE%\AppData\LocalLow\Equipo NEXO\NEXO - PROTOCOLO SOMBRA\Player.log`. |
| Último recurso | Abrir el proyecto en Unity 6000.6.7f1, escena `Assets/_Project/Scenes/Persistent/Systems_Persistent.unity`, Play. |
