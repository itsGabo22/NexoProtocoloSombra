# Regenerar las voces de Elena (solo las de Windows)

Estado a 2026-11-04: de las **51** líneas fijas de Elena, **28** tienen voz de Gemini TTS (voz *Leda*) y **23** la voz de reserva de Windows (*Microsoft Helena*, con el tono subido), porque la cuota gratuita de TTS se agotó (≈10 peticiones al día por modelo; se renueva a medianoche, hora del Pacífico ≈ 03:00 en Bogotá).

Mientras tanto, las 51 están **igualadas** con `Nexo › Elena › Igualar voces`: 24 kHz, volumen −18 dBFS RMS (±0,4 dB), picos ≤ −1 dBFS, sin recortes ni silencios largos, y el mismo filtro de radio en el juego (`ElenaVoicePlayer`). La voz cambia de timbre entre Gemini y Windows, pero ya no de volumen.

## Pasos (en la máquina de Gabriel, que tiene la clave)

1. `git switch develop` y `git pull`. Cierra cualquier otra sesión de Unity sobre el proyecto.
2. Abre el proyecto en Unity 6000.6.7f1 y comprueba que existe `Assets/_Project/Data/Secrets/ElenaSecrets.asset` con tu clave (**no** se versiona; no la copies en ningún otro sitio).
3. Espera a que se renueve la cuota (después de las 03:00 de Bogotá).
4. Ejecuta **Nexo › Elena › Generate Voice (Gemini TTS)**.
   - Reutiliza las 28 líneas que ya son de Gemini y **solo** rehace las 23 de Windows (no gasta peticiones en el resto).
   - Si se acaba la cuota a mitad, las que falten se quedan con la voz de Windows: vuelve a ejecutarlo al día siguiente.
   - El resumen de la Console dice «con voz de Windows: N». El objetivo es 0.
5. Ejecuta **Nexo › Elena › Igualar voces (volumen y frecuencia)** para que las nuevas queden al mismo nivel que el resto.
6. Ejecuta **Nexo › Checks › Elena Voice Coverage**: debe dar 51/51.
7. Commit de los WAV y del manifiesto (nunca de `ElenaSecrets.asset`):

   ```
   git add Assets/_Project/Audio/Elena/Voice/*.wav Assets/_Project/Audio/Elena/Voice/voice_manifest.json Assets/_Project/Resources/Elena/ElenaVoiceLibrary.asset
   git commit -m "feat(elena): regenerate windows voice lines with gemini tts"
   ```

   Antes del push, escanea el diff (`git diff origin/develop | Select-String 'AIza'`): no debe salir nada.

## Líneas que siguen con la voz de Windows (23)

| Origen | Archivo | Texto |
| --- | --- | --- |
| `fallback_floor12:FEAR` | elena_f12_272001a9.wav | Tengo el llavero de mi mamá. Dice que nos va a cuidar. Tú sigue caminando, Alex. |
| `Floor01_Lobby:giro` | elena_f01_63d4ea0a.wav | Perdóname, Alex. |
| `Floor01_Lobby:giro` | elena_f01_7895e003.wav | Necesitaba que llegaras hasta aquí. |
| `Floor01_Lobby:giro` | elena_f01_9237b881.wav | Tú abriste la puerta por los dos. |
| `Floor06_Labs:Seq_F06_02_LabA_Sombra` | elena_f06_f33d4130.wav | Eso... eso lo escribió mi mamá. Seguro estaba muy cansada cuando lo dejó. |
| `Floor12_Offices:Seq_01_Intro` | elena_f12_2229cfd3.wav | ...Bien. Estás vivo. |
| `Floor12_Offices:Seq_01_Intro` | elena_f12_f6c66ec3.wav | Llega a la sala de servidores. Allí podré ayudarte. |
| `Floor12_Offices:Seq_01_Intro` | elena_f12_c6da58e6.wav | Soy Elena, la asistente del edificio. El piso 12 está en cuarentena y la salida segura está bloqueada. |
| `Floor12_Offices:Seq_01_Intro` | elena_f12_82cb5bd2.wav | No te muevas todavía. Necesito comprobar que eres tú. |
| `Floor12_Offices:Seq_02_NoteReply` | elena_f12_3dc602a6.wav | Ese mensaje fue escrito durante un episodio de pánico. No le des importancia. |
| `Floor12_Offices:Seq_03_Evacuated` | elena_f12_231b3578.wav | El personal fue evacuado. |
| `Floor12_Offices:Seq_03_Evacuated` | elena_f12_4d53b54c.wav | ...Eso es lo que indican los registros. |
| `Floor12_Offices:Seq_05_Recording` | elena_f12_4e41d3ee.wav | Por favor, continúa hacia la sala de servidores. |
| `Floor12_Offices:Seq_05_Recording` | elena_f12_741dbe9c.wav | No tengo autorización para hablar de ese incidente. |
| `Floor12_Offices:Seq_05_Recording` | elena_f12_566afca8.wav | Ese empleado participaba en una investigación no autorizada. |
| `Floor12_Offices:Seq_06_ServerArrive` | elena_f12_b7049068.wav | No pienso dejarte solo ahí fuera. Voy contigo. |
| `Floor12_Offices:Seq_06_ServerArrive` | elena_f12_fb586330.wav | Los archivos que buscas están en la consola de vigilancia. |
| `Floor12_Offices:Seq_06_ServerArrive` | elena_f12_303eeb35.wav | Estás aquí. Te veo a través del cristal. |
| `Floor12_Offices:Seq_07_ProtocoloSombra` | elena_f12_93c8429b.wav | Las puertas se están bloqueando. Te guiaré hasta la escalera de emergencia. |
| `Floor12_Offices:Seq_07_ProtocoloSombra` | elena_f12_8b070722.wav | No soy lo que estás buscando. |
| `Floor12_Offices:Seq_07_ProtocoloSombra` | elena_f12_35e083af.wav | Yo fui creada para ayudar a las personas. |
| `Floor12_Offices:Seq_08_FalseVoice` | elena_f12_0ce2048e.wav | No fui yo. |
| `Floor12_Offices:Seq_08_FalseVoice` | elena_f12_1171f498.wav | No respondas a esa voz. |

Las tres líneas del giro final (`Floor01_Lobby:giro`) son las más importantes de regenerar primero: son las que oye toda la clase.
