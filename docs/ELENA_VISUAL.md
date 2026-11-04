# Elena visible: cómo cambiar el modelo (sin código)

Elena es sobre todo una voz por la radio de las gafas, pero se la **ve** en un momento concreto:

| Dónde | Qué se ve | Cuándo desaparece |
| --- | --- | --- |
| Piso 12, sala de servidores | Una niña (~1,30 m) quieta tras el cristal blindado, respirando y girando la cabeza hacia Alex | Cuando la historia la saca de la sala (`StorySequence` → acción `ElenaFollow`): desde ahí vuelve a ser solo voz |
| Piso 6 y Piso 1 | No se la ve (el Piso 1 es la zona del giro y no se toca) | — |

La figura nunca camina junto al jugador: el marcador `Elena_Placeholder` sigue existiendo (su IA de seguimiento y su
Animator son los de siempre), pero sus primitivas azules (Body/Head/Legs) quedan ocultas y la figura se apaga al salir de la sala.

## Piezas

- `Assets/_Project/Prefabs/Characters/PF_ElenaVisual.prefab`: raíz con `ElenaVisualIdle` (idle procedural: respiración y
  cabeza; no necesita Animator ni clips) y el modelo como hijo `Model`.
- `Nexo › Elena › Rebuild Visual (modelo intercambiable)` (`NexoElenaVisualBuilder`): reconstruye el prefab y lo coloca
  dentro de `Elena_Placeholder` en `Floor12_Offices.unity`.

## Sustituto actual

No hay un modelo de niña en el proyecto. El sustituto usa el humanoide del proyecto (Mixamo **Ch33**, el mismo FBX que
Alex) escalado a 1,30 m, con un material propio gris claro (chaqueta) y una melena oscura hecha con primitivas para que
no se lea como el adulto. Visto a través del cristal y en penumbra funciona como silueta; de cerca es evidente que es un
apaño.

## Cambiarlo por un modelo real

1. Consigue un FBX de una niña (Humanoid o Generic; Mixamo vale). Comprueba su licencia y anótala en `docs/ASSETS.md`.
2. Cierra Unity si quieres evitar reimportaciones a medias y copia el FBX (con sus texturas) en
   `Assets/_Project/Models/Characters/Elena/`.
3. Abre Unity y ejecuta **Nexo › Elena › Rebuild Visual**. El constructor:
   - usa el primer `.fbx` o `.prefab` de esa carpeta (orden alfabético) en lugar de Ch33;
   - lo escala a 1,30 m con los pies en el suelo y le quita los colliders;
   - conserva sus materiales (el tinte y la melena solo se aplican al sustituto);
   - vuelve a colocar la figura en el Piso 12 y guarda la escena.
4. Comprueba en el Piso 12 (o con `Nexo › Checks › Visual Capture`) que mira hacia el pasillo y no atraviesa el cristal.
   Si el modelo trae su propio Animator con un idle, puedes asignarlo en el prefab: `ElenaVisualIdle` es compatible
   (solo mueve la escala y el hueso de la cabeza).

Para volver al sustituto, vacía la carpeta `Models/Characters/Elena/` y repite el paso 3.
