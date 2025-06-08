using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathNetworkGenerator : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private float maxConnectionDistance = 25f;
    [SerializeField] private float pathWidth = 3f;
    [SerializeField] private float heightOffset = 0.1f;
    [SerializeField] private int maxConnectionsPerHouse = 3;

    [Header("Path Variation")]
    [SerializeField] private bool addPathVariation = true;
    [SerializeField] private float pathWanderStrength = 2f;
    [SerializeField] private float pathWanderFrequency = 0.3f;
    [SerializeField] private int randomWaypointsPerPath = 2;

    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float maxSlope = 35f;
    [SerializeField] private LayerMask terrainMask = 1;
    [SerializeField] private int maxGridSize = 500;

    [Header("Terrain Bounds")]
    [SerializeField] private Vector2 terrainSize = new Vector2(500f, 500f);

    [Header("Materials")]
    [SerializeField] private Material pathMaterial;

    [Header("Terrain Deformation")]
    [SerializeField] private bool enableTerrainTracking = true;
    [SerializeField] private float pathUpdateRadius = 15f;
    [SerializeField] private bool showAffectedPaths = false;
    [SerializeField] private bool smoothReconformedPaths = true;
    [SerializeField] private int terrainSampleRadius = 2;
    [SerializeField] private int maxPathUpdatesPerFrame = 5;
    [SerializeField] private bool enablePerformanceLogging = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool showGrid = true;
    [SerializeField] private bool showGridBounds = true;
    [SerializeField] private int gridVisualizationStep = 5;
    [SerializeField] private bool showWalkableOnly = false;
    [SerializeField] private float gridCubeSize = 0.8f;

    private List<Vector3> housePositions = new List<Vector3>();
    private List<GameObject> pathObjects = new List<GameObject>();
    private List<PathData> pathDataList = new List<PathData>();
    private GridNode[,] grid;
    private int gridWidth, gridHeight;
    private Vector3 gridOrigin;

    private class PathData
    {
        public GameObject pathObject;
        public List<Vector3> originalWaypoints;
        public Vector3 center;
        public float radius;
        public float lastUpdateTime;
    }

    private class GridNode
    {
        public Vector3 worldPos;
        public bool walkable;
        public float gCost, hCost;
        public GridNode parent;
        public int x, y;
    }

    void Start()
    {
        Invoke(nameof(GeneratePaths), 1.5f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            GeneratePaths();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            GeneratePaths();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            showGrid = !showGrid;
            Debug.Log($"Grid visualization: {(showGrid ? "ON" : "OFF")}");
        }
    }

    public void GeneratePaths()
    {
        Debug.Log("=== Starting Simple House Path Generation ===");
        Debug.Log($"Max connection distance: {maxConnectionDistance}m");

        ClearPaths();
        FindHouses();

        if (housePositions.Count < 2)
        {
            Debug.LogWarning($"Only found {housePositions.Count} houses. Need at least 2 to create paths.");
            return;
        }

        CreateGridAroundHouses();

        if (grid == null)
        {
            Debug.LogError("Grid creation failed - cannot generate paths");
            return;
        }

        ConnectNearbyHouses();

        Debug.Log($"Finished generating paths for {housePositions.Count} houses");
    }

    void FindHouses()
    {
        housePositions.Clear();

        TerrainObjectPlacer[] placers = FindObjectsByType<TerrainObjectPlacer>(FindObjectsSortMode.None);

        foreach (var placer in placers)
        {
            if (!placer.IsGenerated) continue;

            var priorityField = typeof(TerrainObjectPlacer).GetField("generationPriority",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (priorityField != null)
            {
                int priority = (int)priorityField.GetValue(placer);

                if (priority == 1)
                {
                    var spawnedObjects = placer.GetSpawnedObjects();
                    foreach (var obj in spawnedObjects)
                    {
                        if (obj != null)
                        {
                            housePositions.Add(obj.transform.position);
                        }
                    }

                    Debug.Log($"Found {spawnedObjects.Count} houses from placer with priority {priority}");
                }
            }
        }

        Debug.Log($"Total houses found: {housePositions.Count}");
    }

    void CreateGridAroundHouses()
    {
        if (housePositions.Count == 0) return;

        Vector3 halfSize = new Vector3(terrainSize.x * 0.5f, 0, terrainSize.y * 0.5f);
        Vector3 minBounds = Vector3.zero - halfSize;
        Vector3 maxBounds = Vector3.zero + halfSize;

        Vector3 size = maxBounds - minBounds;

        float adjustedCellSize = cellSize;
        int estimatedWidth = Mathf.CeilToInt(size.x / adjustedCellSize);
        int estimatedHeight = Mathf.CeilToInt(size.z / adjustedCellSize);

        while (estimatedWidth > maxGridSize || estimatedHeight > maxGridSize)
        {
            adjustedCellSize *= 1.5f;
            estimatedWidth = Mathf.CeilToInt(size.x / adjustedCellSize);
            estimatedHeight = Mathf.CeilToInt(size.z / adjustedCellSize);
        }

        if (adjustedCellSize != cellSize)
        {
            Debug.LogWarning($"⚠️ AUTO-ADJUSTED: Cell size changed from {cellSize:F1}m to {adjustedCellSize:F1}m to prevent oversized grid. This new value will be saved to prevent re-adjustment.");
        }

        gridOrigin = minBounds;
        gridWidth = estimatedWidth;
        gridHeight = estimatedHeight;

        if (gridWidth * gridHeight > maxGridSize * maxGridSize)
        {
            Debug.LogError($"Grid too large ({gridWidth}x{gridHeight}). Increase maxGridSize or reduce terrain size.");
            return;
        }

        grid = new GridNode[gridWidth, gridHeight];
        int walkableCount = 0;

        Debug.Log($"Creating grid: {gridWidth} x {gridHeight} (terrain: {terrainSize.x}x{terrainSize.y})");
        Debug.Log($"Grid origin: {gridOrigin}");
        Debug.Log($"Grid end: {gridOrigin + new Vector3(gridWidth * adjustedCellSize, 0, gridHeight * adjustedCellSize)}");

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode node = new GridNode();
                node.x = x;
                node.y = y;
                node.worldPos = gridOrigin + new Vector3(x * adjustedCellSize, 0, y * adjustedCellSize);

                Vector3 rayStart = node.worldPos + Vector3.up * 100f;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, terrainMask))
                {
                    node.worldPos.y = hit.point.y;

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

        cellSize = adjustedCellSize;

        Debug.Log($"Grid created with {walkableCount} walkable cells out of {gridWidth * gridHeight} total");
    }

    void ConnectNearbyHouses()
    {
        int pathsCreated = 0;

        for (int i = 0; i < housePositions.Count; i++)
        {
            Vector3 currentHouse = housePositions[i];
            int connectionsForThisHouse = 0;

            var nearbyHouses = housePositions
                .Where((house, index) => index != i)
                .Where(house => Vector3.Distance(currentHouse, house) <= maxConnectionDistance)
                .OrderBy(house => Vector3.Distance(currentHouse, house))
                .Take(maxConnectionsPerHouse)
                .ToList();

            foreach (var nearbyHouse in nearbyHouses)
            {
                int nearbyIndex = housePositions.IndexOf(nearbyHouse);
                if (i >= nearbyIndex) continue;

                List<Vector3> path = FindPath(currentHouse, nearbyHouse);
                if (path != null && path.Count > 1)
                {
                    if (addPathVariation)
                    {
                        path = AddPathVariation(path);
                        path = SmoothAndResamplePath(path);
                    }

                    CreatePathMesh(path);

                    // Store path data for terrain tracking
                    if (enableTerrainTracking)
                    {
                        StorePathData(pathObjects[pathObjects.Count - 1], path);
                    }

                    pathsCreated++;
                    connectionsForThisHouse++;

                    float distance = Vector3.Distance(currentHouse, nearbyHouse);
                    Debug.Log($"✓ Connected house {i} to house {nearbyIndex} - Distance: {distance:F1}m, Path points: {path.Count}");
                }
                else
                {
                    Debug.LogWarning($"✗ Failed to find path from house {i} to house {nearbyIndex}");
                }
            }

            if (showDebug)
            {
                Debug.Log($"House {i}: {connectionsForThisHouse} connections, {nearbyHouses.Count} nearby houses");
            }
        }

        Debug.Log($"Created {pathsCreated} paths total");
    }

    List<Vector3> FindPath(Vector3 start, Vector3 end)
    {
        List<Vector3> waypoints = new List<Vector3> { start };

        if (addPathVariation && randomWaypointsPerPath > 0)
        {
            for (int i = 0; i < randomWaypointsPerPath; i++)
            {
                float t = (i + 1f) / (randomWaypointsPerPath + 1f);
                Vector3 baseWaypoint = Vector3.Lerp(start, end, t);

                Vector3 direction = (end - start).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

                float randomOffset = Random.Range(-pathWanderStrength, pathWanderStrength);
                Vector3 waypoint = baseWaypoint + perpendicular * randomOffset;

                waypoints.Add(waypoint);
            }
        }

        waypoints.Add(end);

        List<Vector3> fullPath = new List<Vector3>();

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            List<Vector3> segmentPath = FindDirectPath(waypoints[i], waypoints[i + 1]);
            if (segmentPath == null)
            {
                Debug.LogWarning($"Waypoint path failed, trying direct path as fallback");
                List<Vector3> directPath = FindDirectPath(start, end);
                if (directPath == null)
                {
                    Debug.LogWarning("All pathfinding failed, creating simple straight line");
                    return CreateStraightLinePath(start, end);
                }
                return directPath;
            }

            int startIndex = (i == 0) ? 0 : 1;
            for (int j = startIndex; j < segmentPath.Count; j++)
            {
                fullPath.Add(segmentPath[j]);
            }
        }

        return fullPath.Count > 1 ? fullPath : null;
    }

    List<Vector3> CreateStraightLinePath(Vector3 start, Vector3 end)
    {
        Debug.Log("Creating fallback straight line path");
        List<Vector3> path = new List<Vector3>();

        float distance = Vector3.Distance(start, end);
        int points = Mathf.Max(2, Mathf.CeilToInt(distance / 3f));

        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point = ConformToTerrain(point);
            path.Add(point);
        }

        return path;
    }

    List<Vector3> FindDirectPath(Vector3 start, Vector3 end)
    {
        Vector2Int startGrid = WorldToGrid(start);
        Vector2Int endGrid = WorldToGrid(end);

        if (!IsValidGrid(startGrid) || !IsValidGrid(endGrid))
        {
            Debug.LogWarning($"Invalid grid positions: start {startGrid}, end {endGrid}");
            return null;
        }

        startGrid = FindNearestWalkable(startGrid);
        endGrid = FindNearestWalkable(endGrid);

        if (startGrid.x == -1 || endGrid.x == -1)
        {
            Debug.LogWarning("Could not find walkable start or end positions");
            return null;
        }

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

        openSet.Add(startNode);

        int iterations = 0;
        int maxIterations = Mathf.Min(gridWidth * gridHeight, 50000);

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            GridNode current = openSet[0];
            int currentIndex = 0;

            for (int i = 1; i < openSet.Count; i++)
            {
                float currentF = current.gCost + current.hCost;
                float testF = openSet[i].gCost + openSet[i].hCost;

                if (testF < currentF || (testF == currentF && openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                    currentIndex = i;
                }
            }

            openSet.RemoveAt(currentIndex);
            closedSet.Add(current);

            if (current == endNode)
            {
                Debug.Log($"Path found in {iterations} iterations");
                return RetracePath(startNode, endNode);
            }

            foreach (GridNode neighbor in GetNeighbors(current))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;

                float newCost = current.gCost + GetDistance(current, neighbor);

                if (newCost < neighbor.gCost)
                {
                    neighbor.gCost = newCost;
                    neighbor.hCost = GetDistance(neighbor, endNode);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }

            if (iterations > 10000 && iterations % 1000 == 0)
            {
                Debug.Log($"Pathfinding taking a long time... {iterations} iterations so far");
            }
        }

        Debug.LogWarning($"Direct pathfinding failed after {iterations} iterations (max: {maxIterations})");
        return null;
    }

    List<Vector3> AddPathVariation(List<Vector3> originalPath)
    {
        if (originalPath.Count < 3) return originalPath;

        List<Vector3> variedPath = new List<Vector3>();
        variedPath.Add(originalPath[0]);

        for (int i = 1; i < originalPath.Count - 1; i++)
        {
            Vector3 currentPoint = originalPath[i];

            float noiseValue = Mathf.PerlinNoise(i * pathWanderFrequency, 0f) * 2f - 1f;

            Vector3 forward = (originalPath[i + 1] - originalPath[i - 1]).normalized;
            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up).normalized;

            Vector3 offset = perpendicular * (noiseValue * pathWanderStrength);
            Vector3 variedPoint = currentPoint + offset;

            variedPoint = ConformToTerrain(variedPoint);

            variedPath.Add(variedPoint);
        }

        variedPath.Add(originalPath[originalPath.Count - 1]);
        return variedPath;
    }

    List<Vector3> SmoothAndResamplePath(List<Vector3> path)
    {
        if (path.Count < 3) return path;

        List<Vector3> smoothedPath = new List<Vector3>();
        smoothedPath.Add(path[0]);

        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 smoothed = (path[i - 1] + path[i] * 2f + path[i + 1]) / 4f;
            smoothed = ConformToTerrain(smoothed);
            smoothedPath.Add(smoothed);
        }

        smoothedPath.Add(path[path.Count - 1]);

        List<Vector3> resampledPath = new List<Vector3>();
        float targetSpacing = 1.5f;

        resampledPath.Add(smoothedPath[0]);

        for (int i = 0; i < smoothedPath.Count - 1; i++)
        {
            Vector3 start = smoothedPath[i];
            Vector3 end = smoothedPath[i + 1];
            float segmentLength = Vector3.Distance(start, end);

            if (segmentLength > targetSpacing)
            {
                int subdivisions = Mathf.CeilToInt(segmentLength / targetSpacing);

                for (int j = 1; j < subdivisions; j++)
                {
                    float t = j / (float)subdivisions;
                    Vector3 interpolated = Vector3.Lerp(start, end, t);
                    interpolated = ConformToTerrain(interpolated);
                    resampledPath.Add(interpolated);
                }
            }

            resampledPath.Add(end);
        }

        return resampledPath;
    }

    Vector2Int FindNearestWalkable(Vector2Int gridPos)
    {
        if (IsValidGrid(gridPos) && grid[gridPos.x, gridPos.y].walkable)
            return gridPos;

        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = gridPos.x - radius; x <= gridPos.x + radius; x++)
            {
                for (int y = gridPos.y - radius; y <= gridPos.y + radius; y++)
                {
                    if (IsValidGrid(new Vector2Int(x, y)) && grid[x, y].walkable)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    List<Vector3> RetracePath(GridNode startNode, GridNode endNode)
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

                if (IsValidGrid(new Vector2Int(checkX, checkY)))
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

    void CreatePathMesh(List<Vector3> path)
    {
        if (path.Count < 2) return;

        GameObject pathObj = new GameObject($"HousePath_{pathObjects.Count}");
        pathObj.transform.position = Vector3.zero;

        MeshFilter filter = pathObj.AddComponent<MeshFilter>();
        MeshRenderer renderer = pathObj.AddComponent<MeshRenderer>();
        renderer.material = pathMaterial;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float totalDistance = 0f;
        List<float> distances = new List<float> { 0f };

        for (int i = 1; i < path.Count; i++)
        {
            totalDistance += Vector3.Distance(path[i - 1], path[i]);
            distances.Add(totalDistance);
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 direction;

            if (i == 0)
            {
                direction = (path[i + 1] - path[i]).normalized;
            }
            else if (i == path.Count - 1)
            {
                direction = (path[i] - path[i - 1]).normalized;
            }
            else
            {
                Vector3 dirBefore = (path[i] - path[i - 1]).normalized;
                Vector3 dirAfter = (path[i + 1] - path[i]).normalized;
                direction = (dirBefore + dirAfter).normalized;
            }

            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 leftPos = path[i] - perpendicular * (pathWidth * 0.5f);
            Vector3 rightPos = path[i] + perpendicular * (pathWidth * 0.5f);

            Vector3 left = ConformToTerrain(leftPos);
            Vector3 right = ConformToTerrain(rightPos);

            vertices.Add(left);
            vertices.Add(right);

            float u = distances[i] / totalDistance;
            uvs.Add(new Vector2(0, u));
            uvs.Add(new Vector2(1, u));

            if (i > 0)
            {
                int baseIndex = (i - 1) * 2;

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "PathMesh";
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        filter.mesh = mesh;
        pathObjects.Add(pathObj);
    }

    void StorePathData(GameObject pathObject, List<Vector3> waypoints)
    {
        PathData data = new PathData();
        data.pathObject = pathObject;
        data.originalWaypoints = new List<Vector3>(waypoints);
        data.lastUpdateTime = Time.time;

        // Calculate bounding info for spatial queries
        Vector3 min = waypoints[0];
        Vector3 max = waypoints[0];
        foreach (var point in waypoints)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        data.center = (min + max) * 0.5f;
        data.radius = Vector3.Distance(min, max) * 0.5f;

        pathDataList.Add(data);
    }

    // Call this from your terrain deformation system when chunks are updated
    public void OnTerrainDeformed(Vector3 explosionCenter, float explosionRadius)
    {
        if (!enableTerrainTracking || pathDataList.Count == 0) return;

        System.Diagnostics.Stopwatch timer = null;
        if (enablePerformanceLogging)
        {
            timer = System.Diagnostics.Stopwatch.StartNew();
        }

        List<PathData> pathsToUpdate = new List<PathData>();

        foreach (var pathData in pathDataList)
        {
            if (pathData.pathObject == null) continue;

            // More aggressive detection - check if ANY part of the path could be affected
            float distance = Vector3.Distance(pathData.center, explosionCenter);
            float combinedRadius = pathData.radius + explosionRadius + pathUpdateRadius;

            // Also check if explosion center is close to any part of the path
            bool shouldUpdate = distance <= combinedRadius;

            // Additional check: see if explosion overlaps with path bounding box
            if (!shouldUpdate)
            {
                Vector3 pathMin = pathData.center - Vector3.one * pathData.radius;
                Vector3 pathMax = pathData.center + Vector3.one * pathData.radius;
                Vector3 explosionMin = explosionCenter - Vector3.one * (explosionRadius + pathUpdateRadius);
                Vector3 explosionMax = explosionCenter + Vector3.one * (explosionRadius + pathUpdateRadius);

                // Check if bounding boxes overlap
                shouldUpdate = !(pathMax.x < explosionMin.x || pathMin.x > explosionMax.x ||
                               pathMax.y < explosionMin.y || pathMin.y > explosionMax.y ||
                               pathMax.z < explosionMin.z || pathMin.z > explosionMax.z);
            }

            if (shouldUpdate)
            {
                // Don't update the same path too frequently (0.5 second cooldown)
                if (Time.time - pathData.lastUpdateTime > 0.5f)
                {
                    pathsToUpdate.Add(pathData);
                }
            }
        }

        if (timer != null)
        {
            timer.Stop();
            Debug.Log($"Path detection took {timer.ElapsedMilliseconds}ms for {pathsToUpdate.Count} paths");
        }

        if (pathsToUpdate.Count > 0)
        {
            // Spread updates across multiple frames if there are many paths
            if (pathsToUpdate.Count > maxPathUpdatesPerFrame)
            {
                StartCoroutine(UpdatePathsProgressively(pathsToUpdate, explosionCenter, explosionRadius));
            }
            else
            {
                // Update immediately if just a few paths
                foreach (var pathData in pathsToUpdate)
                {
                    ReconformPath(pathData);
                }
                Debug.Log($"Updated {pathsToUpdate.Count} paths immediately due to terrain deformation");
            }
        }
    }

    private System.Collections.IEnumerator UpdatePathsProgressively(List<PathData> pathsToUpdate, Vector3 explosionCenter, float explosionRadius)
    {
        int updatedCount = 0;
        System.Diagnostics.Stopwatch frameTimer = enablePerformanceLogging ? System.Diagnostics.Stopwatch.StartNew() : null;

        for (int i = 0; i < pathsToUpdate.Count; i += maxPathUpdatesPerFrame)
        {
            int count = Mathf.Min(maxPathUpdatesPerFrame, pathsToUpdate.Count - i);

            for (int j = 0; j < count; j++)
            {
                ReconformPath(pathsToUpdate[i + j]);
                updatedCount++;
            }

            if (frameTimer != null)
            {
                Debug.Log($"Updated {count} paths in {frameTimer.ElapsedMilliseconds}ms");
                frameTimer.Restart();
            }

            yield return null; // Wait one frame
        }

        Debug.Log($"Finished updating {updatedCount} paths progressively due to terrain deformation at {explosionCenter}");
    }

    void ReconformPath(PathData pathData)
    {
        System.Diagnostics.Stopwatch timer = null;
        if (enablePerformanceLogging)
        {
            timer = System.Diagnostics.Stopwatch.StartNew();
        }

        // Re-conform original waypoints to current terrain
        List<Vector3> reconformedPath = new List<Vector3>();

        foreach (var waypoint in pathData.originalWaypoints)
        {
            Vector3 conformedPoint;

            if (smoothReconformedPaths)
            {
                conformedPoint = ConformToTerrainSmooth(waypoint);
            }
            else
            {
                conformedPoint = ConformToTerrain(waypoint);
            }

            reconformedPath.Add(conformedPoint);
        }

        // Apply additional smoothing to the entire path if enabled
        if (smoothReconformedPaths && reconformedPath.Count > 2)
        {
            reconformedPath = SmoothPathPoints(reconformedPath);
        }

        // Regenerate the mesh with updated points
        MeshFilter filter = pathData.pathObject.GetComponent<MeshFilter>();
        if (filter != null)
        {
            RegeneratePathMesh(filter, reconformedPath);
        }

        pathData.lastUpdateTime = Time.time;

        if (timer != null)
        {
            timer.Stop();
            if (timer.ElapsedMilliseconds > 5) // Only log slow updates
            {
                Debug.Log($"Path reconform took {timer.ElapsedMilliseconds}ms ({pathData.originalWaypoints.Count} waypoints)");
            }
        }
    }

    Vector3 ConformToTerrainSmooth(Vector3 position)
    {
        // Optimized version - reduce raycast count while maintaining quality
        Vector3 centerPoint = ConformToTerrain(position);

        if (terrainSampleRadius <= 1)
        {
            return centerPoint; // Skip smoothing for minimal quality settings
        }

        List<Vector3> sampledPoints = new List<Vector3> { centerPoint };
        List<float> weights = new List<float> { 2f };

        // Use fewer, smarter-placed samples
        float sampleRadius = 1.5f;
        int samples = Mathf.Min(terrainSampleRadius * 2, 6); // Cap at 6 for performance

        for (int i = 0; i < samples; i++)
        {
            float angle = (i / (float)samples) * 2f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * sampleRadius;
            Vector3 samplePos = position + offset;

            Vector3 sampledPoint = ConformToTerrain(samplePos);

            // Only include points that aren't too different from center (noise reduction)
            float heightDiff = Mathf.Abs(sampledPoint.y - centerPoint.y);
            if (heightDiff < 5f) // Ignore extreme outliers
            {
                sampledPoints.Add(sampledPoint);
                weights.Add(1f);
            }
        }

        // Weighted average
        Vector3 averagePoint = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < sampledPoints.Count; i++)
        {
            averagePoint += sampledPoints[i] * weights[i];
            totalWeight += weights[i];
        }

        return averagePoint / totalWeight;
    }

    List<Vector3> SmoothPathPoints(List<Vector3> path)
    {
        if (path.Count < 3) return path;

        List<Vector3> smoothedPath = new List<Vector3>();
        smoothedPath.Add(path[0]); // Keep first point unchanged

        // Apply simple smoothing filter to interior points
        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 smoothed = (path[i - 1] + path[i] * 2f + path[i + 1]) / 4f;
            smoothed = ConformToTerrain(smoothed); // Ensure it still follows terrain
            smoothedPath.Add(smoothed);
        }

        smoothedPath.Add(path[path.Count - 1]); // Keep last point unchanged
        return smoothedPath;
    }

    void RegeneratePathMesh(MeshFilter filter, List<Vector3> path)
    {
        if (path.Count < 2) return;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float totalDistance = 0f;
        List<float> distances = new List<float> { 0f };

        for (int i = 1; i < path.Count; i++)
        {
            totalDistance += Vector3.Distance(path[i - 1], path[i]);
            distances.Add(totalDistance);
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 direction;

            if (i == 0)
            {
                direction = (path[i + 1] - path[i]).normalized;
            }
            else if (i == path.Count - 1)
            {
                direction = (path[i] - path[i - 1]).normalized;
            }
            else
            {
                Vector3 dirBefore = (path[i] - path[i - 1]).normalized;
                Vector3 dirAfter = (path[i + 1] - path[i]).normalized;
                direction = (dirBefore + dirAfter).normalized;
            }

            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 leftPos = path[i] - perpendicular * (pathWidth * 0.5f);
            Vector3 rightPos = path[i] + perpendicular * (pathWidth * 0.5f);

            Vector3 left = ConformToTerrain(leftPos);
            Vector3 right = ConformToTerrain(rightPos);

            vertices.Add(left);
            vertices.Add(right);

            float u = distances[i] / totalDistance;
            uvs.Add(new Vector2(0, u));
            uvs.Add(new Vector2(1, u));

            if (i > 0)
            {
                int baseIndex = (i - 1) * 2;

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        Mesh mesh = filter.mesh;
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
    }

    Vector3 ConformToTerrain(Vector3 position)
    {
        Vector3 rayStart = new Vector3(position.x, position.y + 50f, position.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainMask))
        {
            // Check if this terrain point respects our slope limit
            float slope = Mathf.Acos(Vector3.Dot(hit.normal, Vector3.up)) * Mathf.Rad2Deg;

            if (slope <= maxSlope)
            {
                // Safe slope - use this terrain point
                return hit.point + Vector3.up * heightOffset;
            }
            else
            {
                // Too steep! Try to find nearby walkable terrain
                Vector3 walkablePoint = FindNearbyWalkableTerrain(position, 3f);
                if (walkablePoint != Vector3.zero)
                {
                    return walkablePoint;
                }

                // No walkable terrain nearby - keep path elevated above steep area
                Debug.LogWarning($"Path elevated above steep terrain (slope: {slope:F1}°) at {position}");
                return hit.point + Vector3.up * (heightOffset + 2f); // Elevated path
            }
        }

        return position;
    }

    Vector3 FindNearbyWalkableTerrain(Vector3 center, float searchRadius)
    {
        // Try points in a circle around the original position
        int samples = 8;
        for (int i = 0; i < samples; i++)
        {
            float angle = (i / (float)samples) * 2f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * searchRadius;
            Vector3 testPos = center + offset;

            Vector3 rayStart = new Vector3(testPos.x, testPos.y + 50f, testPos.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainMask))
            {
                float slope = Mathf.Acos(Vector3.Dot(hit.normal, Vector3.up)) * Mathf.Rad2Deg;
                if (slope <= maxSlope)
                {
                    return hit.point + Vector3.up * heightOffset;
                }
            }
        }

        return Vector3.zero; // No walkable terrain found
    }

    void ClearPaths()
    {
        foreach (var obj in pathObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        pathObjects.Clear();
        pathDataList.Clear();
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
        // Draw path bounds if terrain tracking is enabled
        if (showAffectedPaths && enableTerrainTracking && pathDataList != null)
        {
            Gizmos.color = Color.cyan * 0.3f;
            foreach (var pathData in pathDataList)
            {
                if (pathData.pathObject != null)
                {
                    Gizmos.DrawWireSphere(pathData.center, pathData.radius);
                }
            }
        }

        // Draw grid visualization
        if (showGrid && grid != null)
        {
            // Draw grid bounds
            if (showGridBounds)
            {
                Gizmos.color = Color.yellow;
                Vector3 center = gridOrigin + new Vector3(gridWidth * cellSize * 0.5f, 0, gridHeight * cellSize * 0.5f);
                Vector3 size = new Vector3(gridWidth * cellSize, 0.5f, gridHeight * cellSize);
                Gizmos.DrawWireCube(center, size);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(gridOrigin, 2f);
                Gizmos.DrawSphere(gridOrigin + new Vector3(gridWidth * cellSize, 0, gridHeight * cellSize), 2f);
            }

            int step = Mathf.Max(1, gridVisualizationStep);
            for (int x = 0; x < gridWidth; x += step)
            {
                for (int y = 0; y < gridHeight; y += step)
                {
                    GridNode node = grid[x, y];

                    if (showWalkableOnly && !node.walkable) continue;

                    if (node.walkable)
                    {
                        Gizmos.color = Color.green * 0.4f;
                    }
                    else
                    {
                        Gizmos.color = Color.red * 0.3f;
                    }

                    Vector3 cubeSize = Vector3.one * cellSize * gridCubeSize;
                    Gizmos.DrawCube(node.worldPos, cubeSize);

                    Gizmos.color = node.walkable ? Color.green * 0.8f : Color.red * 0.6f;
                    Gizmos.DrawWireCube(node.worldPos, cubeSize);
                }
            }

#if UNITY_EDITOR
            if (showDebug)
            {
                string gridInfo = $"Grid: {gridWidth}x{gridHeight}\nCell Size: {cellSize:F1}m\nTotal Cells: {gridWidth * gridHeight}";
                gridInfo += $"\nTerrain Bounds: {terrainSize.x}x{terrainSize.y}";
                UnityEditor.Handles.Label(gridOrigin + Vector3.up * 5f, gridInfo);
            }
#endif
        }
    }
}