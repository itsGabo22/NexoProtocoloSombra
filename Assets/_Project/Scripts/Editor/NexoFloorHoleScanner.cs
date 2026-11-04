using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Busca agujeros por los que el jugador puede caerse del mundo. Para cada piso hace un relleno por inundación
    /// sobre una rejilla de 0,3 m sembrada desde el NavMesh: una celda es transitable si un rayo hacia abajo encuentra
    /// suelo a un escalón de distancia y la cápsula del jugador cabe. Si desde una celda transitable se puede pasar
    /// (sin pared) a una celda sin suelo cercano, se informa como «caída» con la profundidad encontrada (o «vacío» si no
    /// hay nada debajo). También informa de NavMesh sin collider debajo. Solo lectura.
    /// Menú Nexo/Checks/Floor Holes o -executeMethod Nexo.EditorTools.NexoFloorHoleScanner.RunBatch.
    /// Resultado en Logs/NexoFloorHoles.txt.
    /// </summary>
    public static class NexoFloorHoleScanner
    {
        private const float Cell = 0.3f;
        private const float StepHeight = 0.45f;
        private const float PlayerRadius = 0.3f;
        private const float PlayerHeight = 1.8f;
        private const float HazardDrop = 2.2f;
        private const int MaxCells = 400000;
        private const string ReportPath = "Logs/NexoFloorHoles.txt";

        private static readonly string[] Floors =
        {
            "Assets/_Project/Scenes/Floors/Floor12_Offices.unity",
            "Assets/_Project/Scenes/Floors/Floor06_Labs.unity",
            "Assets/_Project/Scenes/Floors/Floor01_Lobby.unity"
        };

        /// <summary>Peligro detectado: posición de la celda transitable y de la celda sin suelo.</summary>
        public struct Hazard
        {
            public Vector3 Edge;
            public Vector3 Void;
            public float Drop; // float.PositiveInfinity = nada debajo
        }

        [MenuItem("Nexo/Checks/Floor Holes (agujeros del suelo)")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int total = RunAll(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Agujeros", total == 0 ? "Sin agujeros." : $"{total} zona(s) de caída. Detalle en la Console.", "OK");
        }

        public static void RunBatch()
        {
            int total = RunAll(out string report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static int RunAll(out string report)
        {
            var sb = new StringBuilder();
            int total = 0;
            foreach (string path in Floors)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                List<Hazard> hazards = Scan(out int walkable, out int navNoCollider, out Vector3 navNoColliderSample);
                List<Hazard> clusters = Cluster(hazards, 2.5f);
                total += clusters.Count;
                var meta = Object.FindFirstObjectByType<Nexo.Core.FloorMetadata>();
                sb.AppendLine($"   (spawn {(meta != null && meta.PlayerSpawn != null ? F(meta.PlayerSpawn.position) : "?")})");
                sb.AppendLine($"== {scene.name}: {walkable} celdas transitables, {hazards.Count} bordes peligrosos en {clusters.Count} zona(s); NavMesh sin collider debajo: {navNoCollider}" +
                              (navNoCollider > 0 ? $" (p. ej. {F(navNoColliderSample)})" : ""));
                foreach (Hazard h in clusters)
                {
                    string drop = float.IsPositiveInfinity(h.Drop) ? "VACÍO (nada debajo)" : $"caída {h.Drop:0.0} m";
                    sb.AppendLine($"   borde {F(h.Edge)} -> {F(h.Void)}: {drop}  [{Describe(h.Void)}]");
                }
                foreach (Hazard h in hazards) sb.AppendLine($"      · {F(h.Edge)} -> {F(h.Void)}");
                if (clusters.Count > 0) sb.Append(Nearby(clusters[0].Void, 4f));
            }
            report = "[NexoFloorHoleScanner]\n" + sb;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report);
            return total;
        }

        /// <summary>Escanea el piso abierto. Devuelve los bordes desde los que se cae más de HazardDrop.</summary>
        public static List<Hazard> Scan(out int walkableCount, out int navNoCollider, out Vector3 navNoColliderSample)
        {
            Physics.SyncTransforms();
            var hazards = new List<Hazard>();
            var visited = new Dictionary<Vector2Int, float>(); // celda -> altura del suelo
            var queue = new Queue<Vector2Int>();
            navNoCollider = 0;
            navNoColliderSample = Vector3.zero;

            // Solo se siembra a la altura del suelo del piso (el NavMesh también cubre tejados inalcanzables)
            var metadata = Object.FindFirstObjectByType<Nexo.Core.FloorMetadata>();
            float floorY = metadata != null && metadata.PlayerSpawn != null ? metadata.PlayerSpawn.position.y : 0f;
            if (metadata != null && metadata.PlayerSpawn != null && TryGround(metadata.PlayerSpawn.position + Vector3.up, 4f, out float spawnGround)) floorY = spawnGround;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                Vector3 c = (tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3f;
                if (Mathf.Abs(c.y - floorY) > 1.2f) continue;
                if (!TryGround(c + Vector3.up * 0.6f, 1.4f, out float y))
                {
                    navNoCollider++;
                    navNoColliderSample = c;
                    continue;
                }
                Vector2Int key = Key(c);
                if (visited.ContainsKey(key) || !CapsuleFree(new Vector3(c.x, y, c.z))) continue;
                visited[key] = y;
                queue.Enqueue(key);
            }

            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0 && visited.Count < MaxCells)
            {
                Vector2Int cur = queue.Dequeue();
                float groundY = visited[cur];
                Vector3 from = Pos(cur, groundY);
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int next = cur + d;
                    if (visited.ContainsKey(next)) continue;
                    Vector3 probe = Pos(next, groundY);
                    // Pared entre las dos celdas (rodilla y pecho)
                    if (Blocked(from, probe)) continue;

                    if (TryGround(probe + Vector3.up * (StepHeight + 0.05f), StepHeight * 2f + 0.05f, out float ny))
                    {
                        Vector3 standing = new Vector3(probe.x, ny, probe.z);
                        if (!CapsuleFree(standing)) continue;
                        visited[next] = ny;
                        queue.Enqueue(next);
                        continue;
                    }

                    // Sin suelo a un escalón: ¿cabe el jugador y cuánto cae?
                    if (!CapsuleFree(probe + Vector3.up * 0.05f)) continue;
                    float drop = TryGround(probe + Vector3.up * 0.2f, 40f, out float deepY) ? groundY - deepY : float.PositiveInfinity;
                    if (drop > HazardDrop) hazards.Add(new Hazard { Edge = from, Void = probe, Drop = drop });
                }
            }

            walkableCount = visited.Count;
            return hazards;
        }

        private static List<Hazard> Cluster(List<Hazard> hazards, float radius)
        {
            var result = new List<Hazard>();
            var counts = new List<int>();
            foreach (Hazard h in hazards)
            {
                int found = -1;
                for (int i = 0; i < result.Count; i++)
                {
                    if ((result[i].Void - h.Void).sqrMagnitude < radius * radius) { found = i; break; }
                }
                if (found < 0) { result.Add(h); counts.Add(1); }
                else if (h.Drop > result[found].Drop) { Hazard keep = h; result[found] = keep; counts[found]++; }
                else counts[found]++;
            }
            return result;
        }

        private static bool TryGround(Vector3 origin, float distance, out float y)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) && !IsPlayerOrActor(hit.collider))
            {
                y = hit.point.y;
                return true;
            }
            y = 0f;
            return false;
        }

        private static bool CapsuleFree(Vector3 feet)
        {
            Vector3 bottom = feet + Vector3.up * (StepHeight + PlayerRadius);
            Vector3 top = feet + Vector3.up * (PlayerHeight - PlayerRadius);
            foreach (Collider c in Physics.OverlapCapsule(bottom, top, PlayerRadius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsPlayerOrActor(c)) return false;
            }
            return true;
        }

        private static bool Blocked(Vector3 a, Vector3 b)
        {
            for (float h = 0.6f; h <= 1.5f; h += 0.45f)
            {
                Vector3 up = Vector3.up * h;
                if (Physics.Linecast(a + up, b + up, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !IsPlayerOrActor(hit.collider)) return true;
            }
            return false;
        }

        private static bool IsPlayerOrActor(Collider c)
        {
            // Los colliders de personajes (infectados, Elena) no son suelo ni pared
            // Las puertas se tratan como abiertas: el jugador acaba pasando por ellas
            return c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<NavMeshAgent>() != null ||
                   c.GetComponentInParent<Nexo.Interaction.DoorController>() != null;
        }

        private static string Describe(Vector3 p)
        {
            // Objeto más cercano con renderer (para localizar la zona en la escena)
            string best = "?";
            float bestDist = float.MaxValue;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                float d = (r.bounds.ClosestPoint(p) - p).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = r.name; }
            }
            return $"cerca de {best}";
        }

        private static string Nearby(Vector3 p, float radius)
        {
            var sb = new StringBuilder();
            foreach (Collider c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Collide))
                sb.AppendLine($"      collider {c.name} ({c.GetType().Name}, trigger={c.isTrigger}) bounds {F(c.bounds.min)}..{F(c.bounds.max)}");
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if ((r.bounds.ClosestPoint(p) - p).sqrMagnitude < radius * radius && r.bounds.size.magnitude < 30f)
                    sb.AppendLine($"      renderer {r.name} bounds {F(r.bounds.min)}..{F(r.bounds.max)} collider={r.GetComponent<Collider>() != null}");
            return sb.ToString();
        }

        private static Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / Cell), Mathf.RoundToInt(p.z / Cell));
        private static Vector3 Pos(Vector2Int k, float y) => new Vector3(k.x * Cell, y, k.y * Cell);
        private static string F(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
    }
}
