# Elena: cómo está integrada la IA

Elena es una niña de 9 años que guía a Alex por la radio de sus gafas NexoOS. Sus respuestas libres las genera **Gemini** (API REST de Google) en tiempo real; si la IA no responde a tiempo, contesta un **respaldo offline** escrito por el equipo. Elena **nunca se queda callada, nunca lanza excepciones y nunca se cuelga**.

## Arquitectura

```
Jugador ─ [1][2][3] preguntas rápidas ─┐
        ─ [T] texto escrito ──────────┼─► GameEvents ─► ElenaAIService.AskAsync
        ─ [V] voz ─► micrófono ─► Gemini (transcripción) ─┘         │
                                                                     ▼
                     GameStateTracker.CaptureContext (piso, sala, infectados, ruido, batería, puertas…)
                                                                     │
                     GeminiProvider ─► gemini-2.5-flash ─(503/429/404/timeout)─► gemini-3.5-flash-lite ─► gemini-3.8-flash
                                                                     │ null / JSON inválido
                     ElenaReplyResolver ─► fallback_floorNN.json ─► respuesta de emergencia
                                                                     │  (response.source = modelo | fallback | emergency)
                     ElenaReplyGuard + HudTextSanitizer (sin spoilers, sin salir del personaje, sin ids internos)
                                                                     │
          GameEvents.OnElenaResponse ─► HUD (diálogo + marcador «IA: Gemini» / «IA: respaldo»)
                                    ─► ElenaVoicePlayer (clip grabado o TTS dinámico)
                                    ─► ElenaUiCommandRouter (abre puertas solo si target_id ∈ available_doors)
```

| Archivo (`Assets/_Project/Scripts/`) | Papel |
| --- | --- |
| `AI/Elena/ElenaAIService.cs` | Orquesta la petición, el historial (6 turnos) y publica la respuesta |
| `AI/Elena/GeminiProvider.cs` | Cliente REST, `responseMimeType: application/json`, cadena de modelos dentro de un plazo total |
| `AI/Elena/GeminiModelOrder.cs`, `GeminiThinking.cs` | Orden de modelos sin duplicados; razonamiento mínimo por familia de modelo |
| `AI/Elena/ElenaReplyResolver.cs` | Cadena Gemini → JSON → emergencia y texto del marcador |
| `AI/Elena/ElenaFallbackProvider.cs` | Respaldo offline por piso e intención (`Data/ElenaFallback/fallback_floor12/06/01.json`) |
| `AI/Elena/ElenaReplyGuard.cs` | Bloquea spoilers del giro y respuestas fuera de personaje |
| `AI/Elena/ElenaUiCommandRouter.cs` | Lista blanca de acciones sobre el mundo |
| `Player/ElenaVoiceInput.cs`, `Player/ElenaVoiceRules.cs` | Entrada de voz [V] |
| `AI/Elena/GeminiSpeechTranscriber.cs` | Transcripción del audio con Gemini |
| `AI/Elena/ElenaVoicePlayer.cs`, `GeminiTtsClient.cs` | Voz de Elena |

## Diseño del prompt y personaje

- **System prompt:** `Data/ElenaSystemPrompt.txt`. Define identidad (niña asustada pero valiente, lenguaje sencillo, «mi mamá dice…»), el **secreto** que no puede revelar (Alex está infectado desde el minuto 1 y las gafas mienten), reglas (español latino, nunca decir que es una IA, usar solo datos del contexto, urgencia si hay infectados cerca) y un **formato de salida JSON estricto**: `dialogue` (≤ 140 caracteres), `emotion`, `ui_action`, `target_id`, `trust_level`, `glitch_intensity`.
- **Contexto por piso:** cada petición incluye `CONTEXTO_JSON` con piso, sala, infectados cercanos, luces, ruido, batería, anomalías vistas, confianza, intención y texto del jugador, y las **puertas disponibles** de ese piso. Así Elena responde a la situación real y no inventa puertas.
- **Seguridad narrativa:** aunque el prompt lo prohíbe, `ElenaReplyGuard` filtra además cualquier respuesta que insinúe la infección («98,4 %», «estás infectado», «las gafas te mienten») o rompa el personaje; en ese caso se usa el respaldo. El giro solo lo revela el BioScanner del Piso 1.
- **Acciones:** Elena puede pedir `UNLOCK_DOOR`, `PING_ENEMY_RADAR`, etc. `ElenaUiCommandRouter` solo las ejecuta si son válidas (p. ej. en el Piso 6 la pregunta [2] abre `F06_Door_Stairs`).

## Modelos y plazos (configuración entregada)

| Parámetro | Valor | Motivo |
| --- | --- | --- |
| Modelo principal | `gemini-2.5-flash` (`thinkingBudget: 0`) | Medido el 2026-11-04 (5 llamadas): 5/5 en 0,9-2,1 s |
| Reservas | `gemini-3.5-flash-lite`, `gemini-3.8-flash` | Ese día: 3.5-flash-lite 3/5 en 3,8-8,1 s; 3.8-flash 429 (cuota). Fallan rápido, así que probarlas cuesta poco |
| Plazo por modelo | 4,5 s | Si el principal no llega, aún queda tiempo para la reserva |
| Plazo total | 8 s | Agotado → respaldo offline al instante |
| Razonamiento | 2.5: `thinkingBudget: 0`; 3.x: `thinkingLevel: low` | 3.x rechaza `minimal` y `thinkingBudget: 0`; sin limitarlo la latencia sube a ~4,6 s |
| Transcripción [V] | Mismo orden de modelos, plazo total 10 s | Subir audio tarda algo más que un texto |
| TTS dinámico | `gemini-3.8-flash-lite-tts`, voz Leda, 4 s | Si no llega a tiempo, solo subtítulo |

Todo es editable sin código en `Data/ElenaConfig.asset` (Inspector). Se reintenta con el siguiente modelo ante 404, 429, 5xx o timeout; ante 400/403 (petición o clave inválidas) se pasa directo al respaldo.

## Respaldo offline y marcador de origen

- Cada respuesta lleva `source`: el nombre del modelo de Gemini, `fallback` (JSON del piso) o `emergency` (línea fija si ni el JSON responde).
- Bajo el diálogo de la radio, la HUD muestra **«IA: Gemini»** o **«IA: respaldo»** (etiqueta `radio-ai-marker`, `pickingMode Ignore`). Las líneas fijas de la historia no muestran marcador.
- La consola registra `[GeminiProvider] Respuesta de <modelo> en <s> s` y `[ElenaAIService] Respuesta de Elena desde: <origen>`.

## Entrada de voz [V]

1. Mantén **V** (máximo **8 s**; al llegar al límite se envía sola). La HUD muestra **«ESCUCHANDO...»**.
2. Al soltar, Unity `Microphone` entrega el audio (16 kHz); si es demasiado corto o silencioso no se gasta red.
3. **«PROCESANDO...»**: Gemini transcribe el audio en español. No depende del ajuste de Windows «Reconocimiento de voz en línea».
4. El texto aparece como «TÚ: …» y se envía a Elena como una pregunta escrita.
5. Fallos: sin micrófono → «No hay micrófono: usa 1/2/3/T»; audio vacío o no entendido → «No te escuché, repite o usa 1/2/3/T». Sin clave (build offline) se intenta el dictado de Windows como alternativa. Nada bloquea el juego.

## Voz de Elena (TTS)

- **51 líneas fijas** de la historia tienen clip grabado (`Audio/Elena/Voice`, tratamiento de radio con filtros paso alto/bajo).
- Las **respuestas dinámicas** de Gemini se sintetizan en tiempo real con Gemini TTS enviando **solo la frase en español** con `languageCode: es-US`. (Con una indicación de estilo en inglés delante, el TTS la leía en voz alta.)

## Limitaciones conocidas

- **Clips fijos con prefijo en inglés (probable):** de las 51 líneas, las 28 sintetizadas con Gemini TTS se generaron con una indicación de estilo en inglés («Say nervously…») delante de la frase; es probable que algunas empiecen leyendo ese texto en inglés (las 23 con la voz de Windows no se ven afectadas). No se regeneraron en esta entrega. Para corregirlo: `Nexo › Elena › Generate Voice (Gemini TTS)` tras cambiar el generador a `BuildBodyRaw` (ver `docs/ELENA_VOICE_REGEN.md`).
- La calidad y latencia de Gemini dependen de la red y de la cuota de la clave; por eso el respaldo existe y está probado.
- La transcripción por voz necesita clave y red; en el build offline [V] depende del dictado de Windows.
- Gemini puede tardar o fallar con cuotas gratuitas (429): Elena contesta igualmente con el respaldo y el marcador lo indica.

## Configurar la clave en local (sin subirla nunca)

1. En Unity: clic derecho en `Assets/_Project/Data/Secrets/` → **Create › Nexo › AI › Elena Secrets** → `ElenaSecrets.asset` y pega la clave en su campo. Esa carpeta está en `.gitignore`.
2. Arrastra el asset al campo **Secrets** de `Data/ElenaConfig.asset`. **No hagas commit de `ElenaConfig.asset` con ese enlace** (en el repo queda sin él); restáuralo con `git restore` antes de subir.
3. Comprueba con **Nexo › Checks › Elena Live (Gemini)**: responde en los tres pisos, prueba el primer modelo inválido y el respaldo, y escribe origen y latencia en `Logs/NexoElenaLiveCheck.txt` (la clave nunca se registra).
4. El build **«Windows (entrega: Elena offline)»** quita la clave y fuerza el respaldo; **«Windows (entrega: IA real con respaldo)»** la incluye: úsalo solo en tu PC (cualquiera con la carpeta del juego podría extraerla).
