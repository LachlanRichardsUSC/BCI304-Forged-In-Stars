using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathNetworkGenerator : MonoBehaviour
{
    [Header("Area Settings")]
    [SerializeField] private Vector2 areaSize = new Vector2(200f, 200f);
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private LayerMask terrainMask = 1;

    [Header("Settlement Detection")]
    [SerializeField] private float settlementRadius = 50f; // Radius to detect building clusters
    [SerializeField] private float maxSlope = 30f;
    [SerializeField] private float buildingClearance = 3f; // Smaller clearance around buildings
    [SerializeField] private int maxBuildingPriority = 1;

    [Header("Path Network Settings")]
    [SerializeField] private float maxMainRoadDistance = 30f; // Max distance for main connections
    [SerializeField] private float maxTrailDistance = 15f; // Max distance for secondary trails
    [SerializeField] private int maxTrailsPerSettlement = 3; // Random trails extending outward
    [SerializeField] private float trailLength = 20f; // How far trails extend

    [Header("Mesh")]
    [SerializeField] private Material pathMaterial;
    [SerializeField] private float mainRoadWidth = 6f;
    [SerializeField] private float trailWidth = 3f;
    [SerializeField] private float heightOffset = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private bool logDetails = true;

    private GridNode[,] grid;
    private int gridWidth, gridHeight;
    private Vector3 gridOrigin;
    private List<Vector3> settlements = new List<Vector3>();
    private List<GameObject> pathObjects = new List<GameObject>();

    private class GridNode
    {
        public Vector3 worldPos;
        public bool walkable;
        public float gCost, hCost, fCost;
        public GridNode parent;
        public int x, y;
        public bool nearBuilding; // Track if close to buildings without blocking
    }

    void Start()
    {
        Invoke(nameof(GeneratePaths), 1f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
            GeneratePaths();
    }

    public void GeneratePaths()
    {
        ClearPaths();

        if (logDetails)
        {
            Debug.Log("=== Starting Path Generation ===");
        }

        // Step 1: Find settlement clusters instead of individual buildings
        FindSettlements();

        if (settlements.Count == 0)
        {
            Debug.LogWarning("No settlements found");
            return;
        }

        // Step 2: Build grid around settlements only
        BuildGridAroundSettlements();

        // Step 3: Mark building areas (but don't block completely)
        MarkBuildingAreas();

        // Step 4: Generate intelligent path network
        GeneratePathNetwork();

        Debug.Log($"Generated path network for {settlements.Count} settlements");
    }

    void FindSettlements()
    {
        settlements.Clear();
        List<Vector3> allBuildings = new List<Vector3>();

        // Collect all building positions
        TerrainObjectPlacer[] scatterers = FindObjectsByType<TerrainObjectPlacer>(FindObjectsSortMode.None);
        foreach (var scatterer in scatterers)
        {
            if (!scatterer.IsGenerated) continue;

            var priorityField = typeof(TerrainObjectPlacer).GetField("generationPriority",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (priorityField != null)
            {
                int priority = (int)priorityField.GetValue(scatterer);
                if (priority <= maxBuildingPriority)
                {
                    foreach (var obj in scatterer.GetSpawnedObjects())
                    {
                        if (obj != null)
                        {
                            allBuildings.Add(obj.transform.position);
                        }
                    }
                }
            }
        }

        // Cluster buildings into settlements
        List<Vector3> processedBuildings = new List<Vector3>(allBuildings);

        while (processedBuildings.Count > 0)
        {
            Vector3 seedBuilding = processedBuildings[0];
            List<Vector3> cluster = new List<Vector3> { seedBuilding };
            processedBuildings.RemoveAt(0);

            // Find all buildings within settlement radius
            for (int i = processedBuildings.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(seedBuilding, processedBuildings[i]) <= settlementRadius)
                {
                    cluster.Add(processedBuildings[i]);
                    processedBuildings.RemoveAt(i);
                }
            }

            // Calculate settlement center
            Vector3 center = Vector3.zero;
            foreach (var building in cluster)
            {
                center += building;
            }
            center /= cluster.Count;

            settlements.Add(center);

            if (logDetails)
                Debug.Log($"Found settlement with {cluster.Count} buildings at {center}");
        }
    }

    void BuildGridAroundSettlements()
    {
        if (settlements.Count == 0) return;

        // Calculate bounds that encompass all settlements plus some padding
        Vector3 minBounds = settlements[0];
        Vector3 maxBounds = settlements[0];

        foreach (var settlement in settlements)
        {
            minBounds = Vector3.Min(minBounds, settlement - Vector3.one * (settlementRadius + trailLength));
            maxBounds = Vector3.Max(maxBounds, settlement + Vector3.one * (settlementRadius + trailLength));
        }

        Vector3 size = maxBounds - minBounds;
        gridOrigin = minBounds;
        gridWidth = Mathf.CeilToInt(size.x / cellSize);
        gridHeight = Mathf.CeilToInt(size.z / cellSize);

        grid = new GridNode[gridWidth, gridHeight];
        int walkableCount = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode node = new GridNode();
                node.x = x;
                node.y = y;
                node.worldPos = gridOrigin + new Vector3(x * cellSize, 0, y * cellSize);

                // Raycast to terrain
                Vector3 rayStart = node.worldPos + Vector3.up * 100f;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, terrainMask))
                {
                    node.worldPos.y = hit.point.y;

                    // Check slope
                    float slope = Mathf.Acos(Vector3.Dot(hit.normal, Vector3.up)) * Mathf.Rad2Deg;
                    node.walkable = slope <= maxSlope;

                    if (node.walkable) walkableCount++;
                }
                else
                {
                    node.walkable = false;
                }

                grid[x, y] = node;
            }
        }

        if (logDetails)
            Debug.Log($"Grid: {gridWidth}x{gridHeight}, {walkableCount} walkable cells");
    }

    void MarkBuildingAreas()
    {
        // Get all building positions
        TerrainObjectPlacer[] scatterers = FindObjectsByType<TerrainObjectPlacer>(FindObjectsSortMode.None);
        List<Vector3> buildingPositions = new List<Vector3>();

        foreach (var scatterer in scatterers)
        {
            if (!scatterer.IsGenerated) continue;

            var priorityField = typeof(TerrainObjectPlacer).GetField("generationPriority",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (priorityField != null)
            {
                int priority = (int)priorityField.GetValue(scatterer);
                if (priority <= maxBuildingPriority)
                {
                    foreach (var obj in scatterer.GetSpawnedObjects())
                    {
                        if (obj != null)
                        {
                            buildingPositions.Add(obj.transform.position);
                        }
                    }
                }
            }
        }

        // Mark cells near buildings but don't block them completely
        // This makes paths prefer to go around buildings but can still path near them
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode node = grid[x, y];
                if (!node.walkable) continue;

                foreach (var buildingPos in buildingPositions)
                {
                    float distance = Vector3.Distance(node.worldPos, buildingPos);
                    if (distance <= buildingClearance)
                    {
                        node.nearBuilding = true;
                        break;
                    }
                }
            }
        }
    }

    void GeneratePathNetwork()
    {
        // 1. Connect settlements with main roads
        ConnectSettlements();

        // 2. Add random trails extending outward from settlements
        AddRandomTrails();
    }

    void ConnectSettlements()
    {
        if (settlements.Count < 2) return;

        // Create minimum spanning tree to avoid connecting everything to everything
        List<(int from, int to, float distance)> connections = new List<(int, int, float)>();

        for (int i = 0; i < settlements.Count; i++)
        {
            for (int j = i + 1; j < settlements.Count; j++)
            {
                float distance = Vector3.Distance(settlements[i], settlements[j]);
                if (distance <= maxMainRoadDistance)
                {
                    connections.Add((i, j, distance));
                }
            }
        }

        // Sort by distance and connect closest settlements first
        connections = connections.OrderBy(c => c.distance).ToList();
        HashSet<int> connected = new HashSet<int>();

        foreach (var connection in connections)
        {
            // Only connect if it creates a useful connection
            if (connected.Count == 0 ||
                !connected.Contains(connection.from) ||
                !connected.Contains(connection.to))
            {
                var path = FindPath(settlements[connection.from], settlements[connection.to]);
                if (path != null && path.Count > 1)
                {
                    CreatePathMesh(path, mainRoadWidth);
                    connected.Add(connection.from);
                    connected.Add(connection.to);

                    if (logDetails)
                        Debug.Log($"✓ Main road: {connection.distance:F1}m");
                }
            }

            // Stop when we have a connected network
            if (connected.Count >= settlements.Count) break;
        }
    }

    void AddRandomTrails()
    {
        foreach (var settlement in settlements)
        {
            int trailCount = Random.Range(1, maxTrailsPerSettlement + 1);

            for (int i = 0; i < trailCount; i++)
            {
                // Pick a random direction
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 trailEnd = settlement + direction * trailLength;

                var path = FindPath(settlement, trailEnd);
                if (path != null && path.Count > 3) // Only create if reasonably long
                {
                    CreatePathMesh(path, trailWidth);
                    if (logDetails)
                        Debug.Log($"✓ Trail: {path.Count} points");
                }
            }
        }
    }

    List<Vector3> FindPath(Vector3 start, Vector3 end)
    {
        Vector2Int startGrid = WorldToGrid(start);
        Vector2Int endGrid = WorldToGrid(end);

        if (!IsValidGrid(startGrid) || !IsValidGrid(endGrid)) return null;

        // Find nearest walkable cells if start/end are not walkable
        startGrid = FindNearestWalkableCell(startGrid);
        endGrid = FindNearestWalkableCell(endGrid);

        if (startGrid.x == -1 || endGrid.x == -1) return null;

        // Reset grid
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y].gCost = float.MaxValue;
                grid[x, y].parent = null;
            }
        }

        List<GridNode> openSet = new List<GridNode>();
        HashSet<GridNode> closedSet = new HashSet<GridNode>();

        GridNode startNode = grid[startGrid.x, startGrid.y];
        GridNode endNode = grid[endGrid.x, endGrid.y];

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, endNode);
        startNode.fCost = startNode.gCost + startNode.hCost;

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            GridNode current = openSet.OrderBy(n => n.fCost).ThenBy(n => n.hCost).First();
            openSet.Remove(current);
            closedSet.Add(current);

            if (current == endNode)
            {
                return RetracePath(startNode, endNode, start, end);
            }

            foreach (GridNode neighbor in GetNeighbors(current))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;

                // Add cost penalty for being near buildings to encourage paths around them
                float moveCost = GetDistance(current, neighbor);
                if (neighbor.nearBuilding) moveCost *= 2f; // Prefer paths away from buildings

                float newCost = current.gCost + moveCost;

                if (newCost < neighbor.gCost)
                {
                    neighbor.gCost = newCost;
                    neighbor.hCost = GetDistance(neighbor, endNode);
                    neighbor.fCost = neighbor.gCost + neighbor.hCost;
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null;
    }

    Vector2Int FindNearestWalkableCell(Vector2Int gridPos)
    {
        if (IsValidGrid(gridPos) && grid[gridPos.x, gridPos.y].walkable)
            return gridPos;

        // Search in expanding squares
        for (int radius = 1; radius <= 10; radius++)
        {
            for (int x = gridPos.x - radius; x <= gridPos.x + radius; x++)
            {
                for (int y = gridPos.y - radius; y <= gridPos.y + radius; y++)
                {
                    Vector2Int testPos = new Vector2Int(x, y);
                    if (IsValidGrid(testPos) && grid[x, y].walkable)
                    {
                        return testPos;
                    }
                }
            }
        }

        return new Vector2Int(-1, -1); // Not found
    }

    List<Vector3> RetracePath(GridNode startNode, GridNode endNode, Vector3 realStart, Vector3 realEnd)
    {
        List<Vector3> path = new List<Vector3>();
        GridNode current = endNode;

        while (current != startNode)
        {
            path.Add(current.worldPos + Vector3.up * heightOffset);
            current = current.parent;
        }

        path.Add(startNode.worldPos + Vector3.up * heightOffset);
        path.Reverse();

        return path;
    }

    List<GridNode> GetNeighbors(GridNode node)
    {
        List<GridNode> neighbors = new List<GridNode>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.x + x;
                int checkY = node.y + y;

                if (checkX >= 0 && checkX < gridWidth && checkY >= 0 && checkY < gridHeight)
                {
                    neighbors.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbors;
    }

    float GetDistance(GridNode a, GridNode b)
    {
        int dstX = Mathf.Abs(a.x - b.x);
        int dstY = Mathf.Abs(a.y - b.y);

        if (dstX > dstY)
            return 14f * dstY + 10f * (dstX - dstY);
        return 14f * dstX + 10f * (dstY - dstX);
    }

    void CreatePathMesh(List<Vector3> path, float width)
    {
        if (path.Count < 2) return;

        GameObject pathObj = new GameObject($"Path_{pathObjects.Count}");
        pathObj.transform.position = Vector3.zero;

        MeshFilter filter = pathObj.AddComponent<MeshFilter>();
        MeshRenderer renderer = pathObj.AddComponent<MeshRenderer>();
        renderer.material = pathMaterial;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 direction;
            if (i == 0)
                direction = (path[i + 1] - path[i]).normalized;
            else if (i == path.Count - 1)
                direction = (path[i] - path[i - 1]).normalized;
            else
                direction = ((path[i + 1] - path[i]) + (path[i] - path[i - 1])).normalized;

            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 left = path[i] - perpendicular * (width * 0.5f);
            Vector3 right = path[i] + perpendicular * (width * 0.5f);

            vertices.Add(left);
            vertices.Add(right);

            float u = i / (float)(path.Count - 1);
            uvs.Add(new Vector2(0, u));
            uvs.Add(new Vector2(1, u));

            if (i > 0)
            {
                int baseIndex = (i - 1) * 2;

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        filter.mesh = mesh;
        pathObjects.Add(pathObj);
    }

    void ClearPaths()
    {
        foreach (var obj in pathObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        pathObjects.Clear();
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - gridOrigin;
        int x = Mathf.Clamp(Mathf.RoundToInt(localPos.x / cellSize), 0, gridWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(localPos.z / cellSize), 0, gridHeight - 1);
        return new Vector2Int(x, y);
    }

    bool IsValidGrid(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridWidth && gridPos.y >= 0 && gridPos.y < gridHeight;
    }

    void OnDrawGizmos()
    {
        if (!showGrid) return;

        // Draw settlements
        Gizmos.color = Color.yellow;
        foreach (var settlement in settlements)
        {
            Gizmos.DrawSphere(settlement, 3f);
            Gizmos.DrawWireSphere(settlement, settlementRadius);
        }

        // Draw grid if available
        if (grid == null) return;

        for (int x = 0; x < gridWidth; x += 2)
        {
            for (int y = 0; y < gridHeight; y += 2)
            {
                GridNode node = grid[x, y];

                if (node.walkable)
                {
                    if (node.nearBuilding)
                        Gizmos.color = Color.yellow * 0.5f; // Near building
                    else
                        Gizmos.color = Color.green * 0.3f; // Normal walkable
                }
                else
                {
                    Gizmos.color = Color.red * 0.3f; // Unwalkable
                }

                Gizmos.DrawCube(node.worldPos, Vector3.one * cellSize * 0.5f);
            }
        }
    }
}