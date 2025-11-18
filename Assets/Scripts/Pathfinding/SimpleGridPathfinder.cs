using System.Collections.Generic;
using UnityEngine;

namespace CSU.Go.Pathfinding
{
    public class SimpleGridPathfinder : MonoBehaviour
    {
        [Header("Grid Area")]
        [SerializeField] Vector3 gridCenter = Vector3.zero;
        [SerializeField] Vector3 gridSize = new Vector3(40f, 5f, 40f);
        [SerializeField, Min(0.25f)] float cellSize = 1f;
        [SerializeField] LayerMask obstacleMask;
        [SerializeField] float obstaclePadding = 0.1f;

        [Header("Runtime")]
        [SerializeField] bool drawGizmos = true;
        [SerializeField] Color walkableColor = new Color(0f, 1f, 0f, 0.15f);
        [SerializeField] Color blockedColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] Color pathColor = new Color(1f, 0.8f, 0.1f, 0.9f);

        Node[,] grid;
        int sizeX, sizeZ;
        float halfCell => cellSize * 0.5f;

        class Node
        {
            public int x;
            public int z;
            public Vector3 worldPos;
            public bool walkable;
            public int gCost;
            public int hCost;
            public int fCost => gCost + hCost;
            public Node parent;
            public Node(int x, int z, Vector3 pos, bool walkable)
            {
                this.x = x; this.z = z; this.worldPos = pos; this.walkable = walkable;
            }
        }

        void OnEnable()
        {
            BuildGrid();
        }

        public void BuildGrid()
        {
            sizeX = Mathf.Max(1, Mathf.RoundToInt(gridSize.x / cellSize));
            sizeZ = Mathf.Max(1, Mathf.RoundToInt(gridSize.z / cellSize));
            grid = new Node[sizeX, sizeZ];

            Vector3 origin = transform.TransformPoint(gridCenter) - new Vector3(gridSize.x, 0f, gridSize.z) * 0.5f;
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Vector3 worldPos = origin + new Vector3(x * cellSize + halfCell, 0f, z * cellSize + halfCell);
                    bool walkable = !Physics.CheckBox(worldPos, new Vector3(halfCell - obstaclePadding, gridSize.y * 0.5f, halfCell - obstaclePadding), Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);
                    grid[x, z] = new Node(x, z, worldPos, walkable);
                }
            }
        }

        Node NodeFromWorld(Vector3 world)
        {
            Vector3 origin = transform.TransformPoint(gridCenter) - new Vector3(gridSize.x, 0f, gridSize.z) * 0.5f;
            Vector3 local = world - origin;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, sizeX - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize), 0, sizeZ - 1);
            return grid[x, z];
        }

        IEnumerable<Node> GetNeighbors(Node n)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = n.x + dx;
                    int nz = n.z + dz;
                    if (nx < 0 || nz < 0 || nx >= sizeX || nz >= sizeZ) continue;
                    var cand = grid[nx, nz];
                    if (!cand.walkable) continue;

                    // prevent diagonal cutting through corners
                    if (dx != 0 && dz != 0)
                    {
                        var n1 = grid[nx, n.z];
                        var n2 = grid[n.x, nz];
                        if (!n1.walkable || !n2.walkable) continue;
                    }
                    yield return cand;
                }
            }
        }

        static int Heuristic(Node a, Node b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.z - b.z);
            // 10/14 cost (Manhattan with diag): 10 straight, 14 diagonal
            int diag = Mathf.Min(dx, dz);
            int straight = dx + dz - 2 * diag;
            return 14 * diag + 10 * straight;
        }

        static int Cost(Node a, Node b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.z - b.z);
            return (dx == 1 && dz == 1) ? 14 : 10;
        }

        public bool TryFindPath(Vector3 startWorld, Vector3 endWorld, out List<Vector3> path)
        {
            path = null;
            if (grid == null || grid.Length == 0) BuildGrid();

            Node start = NodeFromWorld(startWorld);
            Node target = NodeFromWorld(endWorld);

            if (!target.walkable)
            {
                // simple fallback: if target cell blocked, we still try to reach closest neighbors
            }

            var open = new List<Node>(64);
            var closed = new HashSet<Node>();
            open.Add(start);

            foreach (var n in grid)
            {
                n.gCost = int.MaxValue;
                n.hCost = 0;
                n.parent = null;
            }
            start.gCost = 0;
            start.hCost = Heuristic(start, target);

            while (open.Count > 0)
            {
                // get node with lowest fCost
                Node current = open[0];
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].fCost < current.fCost || (open[i].fCost == current.fCost && open[i].hCost < current.hCost))
                        current = open[i];
                }
                open.Remove(current);
                closed.Add(current);

                if (current == target)
                {
                    path = RetracePath(start, target);
                    return true;
                }

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (closed.Contains(neighbor)) continue;
                    int tentativeG = current.gCost + Cost(current, neighbor);
                    if (tentativeG < neighbor.gCost)
                    {
                        neighbor.gCost = tentativeG;
                        neighbor.hCost = Heuristic(neighbor, target);
                        neighbor.parent = current;
                        if (!open.Contains(neighbor)) open.Add(neighbor);
                    }
                }
            }

            return false;
        }

        List<Vector3> RetracePath(Node start, Node end)
        {
            var nodes = new List<Node>();
            Node current = end;
            while (current != start)
            {
                nodes.Add(current);
                current = current.parent;
                if (current == null) break;
            }
            nodes.Reverse();

            // simple smoothing by snapping to cell centers at ground y of start
            float y = start.worldPos.y;
            var result = new List<Vector3>(nodes.Count);
            foreach (var n in nodes)
            {
                var p = n.worldPos; p.y = y;
                result.Add(p);
            }
            return result;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireCube(transform.TransformPoint(gridCenter), new Vector3(gridSize.x, 0.1f, gridSize.z));

            if (grid == null) return;
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var n = grid[x, z];
                    Gizmos.color = n.walkable ? walkableColor : blockedColor;
                    Gizmos.DrawCube(n.worldPos + Vector3.up * 0.02f, new Vector3(cellSize * 0.9f, 0.02f, cellSize * 0.9f));
                }
            }
        }

        // Debug draw of a path (optional)
        public void DrawPath(List<Vector3> path)
        {
            if (path == null) return;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Debug.DrawLine(path[i] + Vector3.up * 0.1f, path[i + 1] + Vector3.up * 0.1f, pathColor, 0.1f);
            }
        }
    }
}
