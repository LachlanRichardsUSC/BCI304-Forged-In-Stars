using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding system specifically designed for individual enemy navigation on marching cubes terrain
/// </summary>
public class EnemyPathfinder : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1.5f;
    [SerializeField] private float maxSlope = 35f;
    [SerializeField] private LayerMask terrainMask = 1;
    [SerializeField] private int localGridSize = 100; // Smaller grid around enemy

    [Header("Pathfinding")]
    [SerializeField] private float pathfindingRange = 50f; // Max distance for pathfinding
    [SerializeField] private int maxIterations = 5000;
    [SerializeField] private float heightOffset = 0.2f;

    [Header("Performance")]
    [SerializeField] private bool enablePerformanceLogging = false;

    [Header("Debug")]
    [SerializeField] private bool showGrid = false;
    [SerializeField] private bool showPath = false;
    [SerializeField] private int gridVisualizationStep = 3;

    private GridNode[,] localGrid;
    private int gridWidth, gridHeight;
    private Vector3 gridCenter;
    private List<Vector3> lastCalculatedPath;

    private class GridNode
    {
        public Vector3 worldPos;
        public bool walkable;
        public float gCost, hCost;
        public GridNode parent;
        public int x, y;

        public float FCost => gCost + hCost;
    }

    /// <summary>
    /// Find a path from start to end position. Returns null if no path found.
    /// </summary>
    public List<Vector3> FindPath(Vector3 start, Vector3 end)
    {
        System.Diagnostics.Stopwatch timer = null;
        if (enablePerformanceLogging)
        {
            timer = System.Diagnostics.Stopwatch.StartNew();
        }

        // Check if target is within pathfinding range
        if (Vector3.Distance(start, end) > pathfindingRange)
        {
            Debug.LogWarning($"Target too far for pathfinding: {Vector3.Distance(start, end):F1}m (max: {pathfindingRange}m)");
            return null;
        }

        // Create local grid centered between start and end
        Vector3 midpoint = (start + end) * 0.5f;
        CreateLocalGrid(midpoint);

        if (localGrid == null)
        {
            Debug.LogError("Failed to create pathfinding grid");
            return null;
        }

        // Convert world positions to grid coordinates
        Vector2Int startGrid = WorldToGrid(start);
        Vector2Int endGrid = WorldToGrid(end);

        if (!IsValidGrid(startGrid) || !IsValidGrid(endGrid))
        {
            if (enablePerformanceLogging)
            {
                Debug.LogWarning($"Invalid grid positions: start {startGrid}, end {endGrid}");
            }
            return CreateDirectPath(start, end); // Fallback
        }

        // Find nearest walkable cells
        startGrid = FindNearestWalkable(startGrid);
        endGrid = FindNearestWalkable(endGrid);

        if (startGrid.x == -1 || endGrid.x == -1)
        {
            Debug.LogWarning("Could not find walkable start or end positions");
            return CreateDirectPath(start, end); // Fallback
        }

        // Perform A* pathfinding
        List<Vector3> path = PerformAStar(startGrid, endGrid);

        if (timer != null)
        {
            timer.Stop();
            Debug.Log($"Enemy pathfinding took {timer.ElapsedMilliseconds}ms");
        }

        lastCalculatedPath = path;
        return path;
    }

    /// <summary>
    /// Get a random walkable position within a radius from the center point
    /// </summary>
    public Vector3 GetRandomWalkablePosition(Vector3 center, float radius)
    {
        CreateLocalGrid(center);

        if (localGrid == null) return center;

        List<Vector3> walkablePositions = new List<Vector3>();
        Vector2Int centerGrid = WorldToGrid(center);
        int searchRadius = Mathf.RoundToInt(radius / cellSize);

        for (int x = centerGrid.x - searchRadius; x <= centerGrid.x + searchRadius; x++)
        {
            for (int y = centerGrid.y - searchRadius; y <= centerGrid.y + searchRadius; y++)
            {
                if (IsValidGrid(new Vector2Int(x, y)) && localGrid[x, y].walkable)
                {
                    Vector3 worldPos = localGrid[x, y].worldPos;
                    if (Vector3.Distance(worldPos, center) <= radius)
                    {
                        walkablePositions.Add(worldPos);
                    }
                }
            }
        }

        if (walkablePositions.Count > 0)
        {
            return walkablePositions[Random.Range(0, walkablePositions.Count)];
        }

        return center; // Fallback to original position
    }

    private void CreateLocalGrid(Vector3 center)
    {
        gridCenter = center;
        gridWidth = localGridSize;
        gridHeight = localGridSize;

        float halfSize = (localGridSize * cellSize) * 0.5f;
        Vector3 gridOrigin = center - new Vector3(halfSize, 0, halfSize);

        localGrid = new GridNode[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode node = new GridNode();
                node.x = x;
                node.y = y;
                node.worldPos = gridOrigin + new Vector3(x * cellSize, 0, y * cellSize);

                // Raycast to find terrain height and check walkability
                Vector3 rayStart = node.worldPos + Vector3.up * 100f;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, terrainMask))
                {
                    node.worldPos.y = hit.point.y + heightOffset;

                    float slope = Mathf.Acos(Vector3.Dot(hit.normal, Vector3.up)) * Mathf.Rad2Deg;
                    node.walkable = slope <= maxSlope;
                }
                else
                {
                    node.walkable = false;
                }

                localGrid[x, y] = node;
            }
        }
    }

    private List<Vector3> PerformAStar(Vector2Int startGrid, Vector2Int endGrid)
    {
        // Reset grid costs
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                localGrid[x, y].gCost = float.MaxValue;
                localGrid[x, y].parent = null;
            }
        }

        List<GridNode> openSet = new List<GridNode>();
        HashSet<GridNode> closedSet = new HashSet<GridNode>();

        GridNode startNode = localGrid[startGrid.x, startGrid.y];
        GridNode endNode = localGrid[endGrid.x, endGrid.y];

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, endNode);

        openSet.Add(startNode);

        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // Find node with lowest F cost
            GridNode current = openSet[0];
            int currentIndex = 0;

            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < current.FCost ||
                    (openSet[i].FCost == current.FCost && openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                    currentIndex = i;
                }
            }

            openSet.RemoveAt(currentIndex);
            closedSet.Add(current);

            // Found target
            if (current == endNode)
            {
                if (enablePerformanceLogging)
                {
                    Debug.Log($"Path found in {iterations} iterations");
                }
                return RetracePath(startNode, endNode);
            }

            // Check neighbors
            foreach (GridNode neighbor in GetNeighbors(current))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                    continue;

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
        }

        Debug.LogWarning($"Pathfinding failed after {iterations} iterations");
        return null;
    }

    private List<Vector3> CreateDirectPath(Vector3 start, Vector3 end)
    {
        List<Vector3> path = new List<Vector3>();

        float distance = Vector3.Distance(start, end);
        int points = Mathf.Max(2, Mathf.CeilToInt(distance / cellSize));

        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point = ConformToTerrain(point);
            path.Add(point);
        }

        return path;
    }

    private Vector3 ConformToTerrain(Vector3 position)
    {
        Vector3 rayStart = new Vector3(position.x, position.y + 50f, position.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainMask))
        {
            return hit.point + Vector3.up * heightOffset;
        }

        return position;
    }

    private Vector2Int FindNearestWalkable(Vector2Int gridPos)
    {
        if (IsValidGrid(gridPos) && localGrid[gridPos.x, gridPos.y].walkable)
            return gridPos;

        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = gridPos.x - radius; x <= gridPos.x + radius; x++)
            {
                for (int y = gridPos.y - radius; y <= gridPos.y + radius; y++)
                {
                    if (IsValidGrid(new Vector2Int(x, y)) && localGrid[x, y].walkable)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    private List<Vector3> RetracePath(GridNode startNode, GridNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        GridNode current = endNode;

        while (current != startNode)
        {
            path.Add(current.worldPos);
            current = current.parent;
        }

        path.Add(startNode.worldPos);
        path.Reverse();

        return path;
    }

    private List<GridNode> GetNeighbors(GridNode node)
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
                    neighbors.Add(localGrid[checkX, checkY]);
                }
            }
        }

        return neighbors;
    }

    private float GetDistance(GridNode a, GridNode b)
    {
        int dstX = Mathf.Abs(a.x - b.x);
        int dstY = Mathf.Abs(a.y - b.y);

        if (dstX > dstY)
            return 14f * dstY + 10f * (dstX - dstY);
        return 14f * dstX + 10f * (dstY - dstX);
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - gridCenter;
        float halfSize = (localGridSize * cellSize) * 0.5f;
        localPos += new Vector3(halfSize, 0, halfSize);

        int x = Mathf.Clamp(Mathf.RoundToInt(localPos.x / cellSize), 0, gridWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(localPos.z / cellSize), 0, gridHeight - 1);
        return new Vector2Int(x, y);
    }

    private bool IsValidGrid(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridWidth && gridPos.y >= 0 && gridPos.y < gridHeight;
    }

    void OnDrawGizmos()
    {
        // Draw calculated path
        if (showPath && lastCalculatedPath != null && lastCalculatedPath.Count > 1)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < lastCalculatedPath.Count - 1; i++)
            {
                Gizmos.DrawLine(lastCalculatedPath[i], lastCalculatedPath[i + 1]);
            }

            // Draw waypoints
            Gizmos.color = Color.yellow;
            foreach (var point in lastCalculatedPath)
            {
                Gizmos.DrawSphere(point, 0.3f);
            }
        }

        // Draw grid
        if (showGrid && localGrid != null)
        {
            int step = Mathf.Max(1, gridVisualizationStep);
            for (int x = 0; x < gridWidth; x += step)
            {
                for (int y = 0; y < gridHeight; y += step)
                {
                    GridNode node = localGrid[x, y];

                    Gizmos.color = node.walkable ? Color.green * 0.3f : Color.red * 0.3f;
                    Gizmos.DrawCube(node.worldPos, Vector3.one * cellSize * 0.8f);
                }
            }
        }
    }
}