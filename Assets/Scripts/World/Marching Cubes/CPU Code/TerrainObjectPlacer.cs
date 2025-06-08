using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry to prevent overlapping between different scatterer types.
/// </summary>
public static class ScattererRegistry
{
    private static List<Vector3> occupiedPositions = new List<Vector3>();
    private static List<float> exclusionRadii = new List<float>();

    /// <summary>
    /// Clears all registered positions. Call this when regenerating all scatterers.
    /// </summary>
    public static void Clear()
    {
        occupiedPositions.Clear();
        exclusionRadii.Clear();
    }

    /// <summary>
    /// Checks if a position conflicts with any registered positions.
    /// </summary>
    public static bool IsPositionBlocked(Vector3 pos, float checkRadius)
    {
        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            float totalRadius = checkRadius + exclusionRadii[i];
            if (Vector3.Distance(pos, occupiedPositions[i]) < totalRadius)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Registers a position as occupied with the given exclusion radius.
    /// </summary>
    public static void RegisterPosition(Vector3 pos, float exclusionRadius)
    {
        occupiedPositions.Add(pos);
        exclusionRadii.Add(exclusionRadius);
    }

    /// <summary>
    /// Gets the count of registered positions (for debugging).
    /// </summary>
    public static int GetRegisteredCount()
    {
        return occupiedPositions.Count;
    }
}

/// <summary>
/// Scatters decorative objects on terrain using Poisson disk sampling, configurable slope values and density.
/// Based off Bridson's Algorithm
/// https://sighack.com/post/poisson-disk-sampling-bridsons-algorithm
/// </summary>
public class TerrainObjectPlacer : MonoBehaviour
{
    [Header("Scatterer Priority")]
    [SerializeField]
    [Tooltip("Lower numbers generate first (0=highest priority). Castles=0, Houses=1, Vegetation=2")]
    [Range(0, 10)] private int generationPriority = 0;

    [SerializeField]
    [Tooltip("Distance to keep other object types away from this scatterer's objects")]
    [Range(1f, 50f)] private float exclusionRadius = 10f;

    [Header("Poisson Disk Sampling")]
    [SerializeField]
    [Tooltip("Minimum distance between decoration objects")]
    [Range(1f, 50f)] private float minDistance = 8f;

    [SerializeField]
    [Tooltip("Max attempts to place a point before giving up")]
    [Range(10, 50)] private int maxSamplesPerPoint = 30;

    [SerializeField]
    [Tooltip("Total area to scatter objects (centered on this transform)")]
    [Range(100f, 1000f)] private float scatterRadius = 400f;

    [Header("Slope Filtering")]
    [SerializeField]
    [Tooltip("Minimum slope in degrees (0 = flat)")]
    [Range(0f, 90f)] private float minSlope = 0f;

    [SerializeField]
    [Tooltip("Maximum slope in degrees (90 = vertical)")]
    [Range(0f, 90f)] private float maxSlope = 25f;

    [Header("Raycast Settings")]
    [SerializeField]
    [Tooltip("Height above terrain to start raycasts")]
    [Range(50f, 200f)] private float raycastHeight = 100f;

    [SerializeField]
    [Tooltip("Layer mask for terrain (should match your ground layer)")]
    private LayerMask terrainLayerMask = 1;

    [Header("Decoration Objects")]
    [SerializeField]
    [Tooltip("Prefabs to scatter")]
    private GameObject[] decorationPrefabs;

    [SerializeField]
    [Tooltip("Random rotation on Y axis")]
    private bool randomRotation = true;

    [SerializeField]
    [Tooltip("Base scale multiplier for all objects")]
    [Range(0.1f, 10f)] private float baseScale = 1f;

    [SerializeField]
    [Tooltip("Random scale variation (0 = no variation, 0.2 = ±20%)")]
    [Range(0f, 0.5f)] private float scaleVariation = 0.1f;

    [SerializeField]
    [Tooltip("Adjust Y position in the event where the objects origin is not at ground level")]
    [Range(0f, 100.0f)] private float adjustYPos = 0f;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSpheres = false;
    [SerializeField] private Material debugSphereMaterial;
    [SerializeField] private float debugSphereSize = 1f;

    // Runtime data
    private List<Vector3> _validPoints = new List<Vector3>();
    private List<GameObject> _debugSpheres = new List<GameObject>();
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private bool _isGenerated = false;

    // Poisson disk sampling grid
    private float _cellSize;
    private int _gridWidth, _gridHeight;
    private Vector2[,] _grid;
    private List<Vector2> _activeList = new List<Vector2>();

    private void Start()
    {
        // Clear registry if this is the highest priority scatterer
        if (generationPriority == 0)
        {
            ScattererRegistry.Clear();
            Debug.Log("Cleared ScattererRegistry (highest priority scatterer)");
        }

        // Wait a frame to ensure terrain is generated, then wait for priority order
        float delay = 0.1f + (generationPriority * 0.1f);
        Invoke(nameof(GenerateDecorationPoints), delay);
    }

    private void Update()
    {
        // Regenerate on R key (same as terrain)
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Regenerating decoration points...");

            // Clear registry if this is the highest priority scatterer
            if (generationPriority == 0)
            {
                ScattererRegistry.Clear();
            }

            GenerateDecorationPoints();
        }

        // Debug key to check registry status
        if (Input.GetKeyDown(KeyCode.T))
        {
            DebugRegistry();
        }
    }

    /// <summary>
    /// Generates decoration points using Poisson disk sampling with slope filtering.
    /// </summary>
    public void GenerateDecorationPoints()
    {
        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        ClearPreviousPoints();

        Debug.Log($"[Priority {generationPriority}] Starting generation. Registry has {ScattererRegistry.GetRegisteredCount()} existing positions.");

        // Generate 2D Poisson disk samples
        List<Vector2> poissonSamples = GeneratePoissonDiskSamples();
        Debug.Log($"Generated {poissonSamples.Count} 2D Poisson samples");

        // Convert 2D samples to 3D world positions with slope filtering and registry checking
        FilterSamplesOnTerrain(poissonSamples);

        // Register our valid points in the global registry
        RegisterValidPoints();

        // Create decorations
        SpawnPrefabObjects();

        // Create debug visualization if enabled
        if (showDebugSpheres)
        {
            CreateDebugVisualization();
        }

        timer.Stop();
        Debug.Log($"[Priority {generationPriority}] Decoration generation completed in {timer.ElapsedMilliseconds}ms");
        Debug.Log($"Final decoration points: {_validPoints.Count}");
        Debug.Log($"Registry now has {ScattererRegistry.GetRegisteredCount()} total positions.");

        _isGenerated = true;
    }

    /// <summary>
    /// Generates 2D Poisson disk samples using Bridson's algorithm.
    /// </summary>
    private List<Vector2> GeneratePoissonDiskSamples()
    {
        List<Vector2> samples = new List<Vector2>();
        _activeList.Clear();

        // Grid setup for fast spatial queries
        _cellSize = minDistance / Mathf.Sqrt(2f);
        _gridWidth = Mathf.CeilToInt(scatterRadius * 2f / _cellSize);
        _gridHeight = Mathf.CeilToInt(scatterRadius * 2f / _cellSize);
        _grid = new Vector2[_gridWidth, _gridHeight];

        // Initialize grid with invalid values
        for (int i = 0; i < _gridWidth; i++)
        {
            for (int j = 0; j < _gridHeight; j++)
            {
                _grid[i, j] = Vector2.one * -1f;
            }
        }

        // Start with initial random point
        Vector2 initialPoint = Vector2.zero; // Center of scatter area
        samples.Add(initialPoint);
        _activeList.Add(initialPoint);
        _grid[_gridWidth / 2, _gridHeight / 2] = initialPoint;

        // Process active list
        while (_activeList.Count > 0)
        {
            int randomIndex = Random.Range(0, _activeList.Count);
            Vector2 currentPoint = _activeList[randomIndex];
            bool foundValidPoint = false;

            // Try to generate new points around current point
            for (int attempt = 0; attempt < maxSamplesPerPoint; attempt++)
            {
                Vector2 newPoint = GenerateRandomPointAround(currentPoint);

                // Check if point is within scatter area
                if (newPoint.magnitude > scatterRadius)
                    continue;

                // Check if point is valid (not too close to existing points)
                if (IsValidPoint(newPoint, samples))
                {
                    samples.Add(newPoint);
                    _activeList.Add(newPoint);

                    // Add to grid
                    Vector2Int gridPos = PointToGrid(newPoint);
                    if (gridPos.x >= 0 && gridPos.x < _gridWidth && gridPos.y >= 0 && gridPos.y < _gridHeight)
                    {
                        _grid[gridPos.x, gridPos.y] = newPoint;
                    }

                    foundValidPoint = true;
                    break;
                }
            }

            // Remove point from active list if no valid neighbors found
            if (!foundValidPoint)
            {
                _activeList.RemoveAt(randomIndex);
            }
        }

        return samples;
    }

    /// <summary>
    /// Generates a random point in an annulus around the given point.
    /// </summary>
    private Vector2 GenerateRandomPointAround(Vector2 center)
    {
        float angle = Random.Range(0f, 2f * Mathf.PI);
        float distance = Random.Range(minDistance, 2f * minDistance);

        return center + new Vector2(
            Mathf.Cos(angle) * distance,
            Mathf.Sin(angle) * distance
        );
    }

    /// <summary>
    /// Checks if a point is valid (not too close to existing points).
    /// </summary>
    private bool IsValidPoint(Vector2 point, List<Vector2> existingSamples)
    {
        Vector2Int gridPos = PointToGrid(point);

        // Check surrounding grid cells
        int searchRadius = 2; // Should be enough for minDistance check
        for (int x = gridPos.x - searchRadius; x <= gridPos.x + searchRadius; x++)
        {
            for (int y = gridPos.y - searchRadius; y <= gridPos.y + searchRadius; y++)
            {
                if (x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight)
                {
                    Vector2 gridPoint = _grid[x, y];
                    if (gridPoint.x != -1f && gridPoint.y != -1f) // Valid grid point
                    {
                        float distSqr = (point - gridPoint).sqrMagnitude;
                        if (distSqr < minDistance * minDistance)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Converts world point to grid coordinates.
    /// </summary>
    private Vector2Int PointToGrid(Vector2 point)
    {
        // Offset by scatter radius to handle negative coordinates
        Vector2 offsetPoint = point + Vector2.one * scatterRadius;
        return new Vector2Int(
            Mathf.FloorToInt(offsetPoint.x / _cellSize),
            Mathf.FloorToInt(offsetPoint.y / _cellSize)
        );
    }

    /// <summary>
    /// Filters 2D samples by raycasting onto terrain and checking slopes + registry conflicts.
    /// </summary>
    private void FilterSamplesOnTerrain(List<Vector2> samples)
    {
        Vector3 scatterCenter = transform.position;
        int validCount = 0;
        int totalRaycasts = 0;
        int registryRejections = 0;

        foreach (Vector2 sample in samples)
        {
            // Convert 2D sample to 3D world position
            Vector3 sampleWorldPos = new Vector3(
                scatterCenter.x + sample.x,
                scatterCenter.y + raycastHeight,
                scatterCenter.z + sample.y
            );

            // Raycast down to find terrain
            if (Physics.Raycast(sampleWorldPos, Vector3.down, out RaycastHit hit, raycastHeight * 2f, terrainLayerMask))
            {
                totalRaycasts++;

                // Calculate slope from surface normal
                float slope = Mathf.Acos(Vector3.Dot(hit.normal, Vector3.up)) * Mathf.Rad2Deg;

                // Check if slope is within valid range
                if (slope >= minSlope && slope <= maxSlope)
                {
                    // Check if position conflicts with existing registered positions
                    if (!ScattererRegistry.IsPositionBlocked(hit.point, exclusionRadius))
                    {
                        _validPoints.Add(hit.point);
                        validCount++;
                    }
                    else
                    {
                        registryRejections++;
                    }
                }
            }
        }

        Debug.Log($"Raycast stats: {totalRaycasts} hits from {samples.Count} samples, {validCount} valid slopes, {registryRejections} registry rejections");
    }

    /// <summary>
    /// Registers all valid points in the global registry to prevent other scatterers from overlapping.
    /// </summary>
    private void RegisterValidPoints()
    {
        foreach (Vector3 point in _validPoints)
        {
            ScattererRegistry.RegisterPosition(point, exclusionRadius);
        }

        Debug.Log($"Registered {_validPoints.Count} positions with exclusion radius {exclusionRadius}m");
    }

    /// <summary>
    /// Spawns prefab GameObjects at all valid points.
    /// </summary>
    private void SpawnPrefabObjects()
    {
        if (_validPoints.Count == 0 || decorationPrefabs == null || decorationPrefabs.Length == 0)
        {
            Debug.LogWarning("Cannot spawn prefabs: no prefabs assigned or no valid points");
            return;
        }

        foreach (Vector3 position in _validPoints)
        {
            // Choose random prefab
            GameObject prefab = decorationPrefabs[Random.Range(0, decorationPrefabs.Length)];
            if (prefab == null) continue;

            // Random rotation on Y axis if enabled
            Quaternion rotation = Quaternion.identity;
            if (randomRotation)
            {
                rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }

            // Apply Y adjustment for wonky pivot points
            Vector3 adjustedPosition = position + Vector3.up * adjustYPos;

            // Spawn object
            GameObject spawnedObject = Instantiate(prefab, adjustedPosition, rotation, transform);

            // Calculate final scale: base scale + random variation
            float scale = baseScale;
            if (scaleVariation > 0)
            {
                float variation = Random.Range(-scaleVariation, scaleVariation);
                scale = baseScale * (1f + variation);
            }
            spawnedObject.transform.localScale = Vector3.one * scale;

            _spawnedObjects.Add(spawnedObject);
        }

        Debug.Log($"Spawned {_spawnedObjects.Count} prefab objects");
    }

    /// <summary>
    /// Creates debug sphere visualization for valid points.
    /// </summary>
    private void CreateDebugVisualization()
    {
        foreach (Vector3 point in _validPoints)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = point + Vector3.up * (debugSphereSize * 0.5f); // Offset slightly above terrain
            sphere.transform.localScale = Vector3.one * debugSphereSize;
            sphere.name = "Debug Decoration Point";

            // Set material if provided
            if (debugSphereMaterial != null)
            {
                sphere.GetComponent<MeshRenderer>().material = debugSphereMaterial;
            }
            else
            {
                // Create green material if none provided
                Material greenMat = new Material(Shader.Find("Standard"));
                greenMat.color = Color.green;
                sphere.GetComponent<MeshRenderer>().material = greenMat;
            }

            // Remove collider for debug objects
            DestroyImmediate(sphere.GetComponent<SphereCollider>());

            // Parent to this object for organization
            sphere.transform.SetParent(transform);

            _debugSpheres.Add(sphere);
        }
    }

    /// <summary>
    /// Clears previous decoration points and debug objects.
    /// </summary>
    private void ClearPreviousPoints()
    {
        _validPoints.Clear();

        // Destroy previous debug spheres
        foreach (GameObject sphere in _debugSpheres)
        {
            if (sphere != null)
            {
                DestroyImmediate(sphere);
            }
        }
        _debugSpheres.Clear();

        // Destroy previous spawned objects
        foreach (GameObject obj in _spawnedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        _spawnedObjects.Clear();

        _isGenerated = false;
    }

    /// <summary>
    /// Gets the list of valid decoration points.
    /// </summary>
    public List<Vector3> GetValidPoints()
    {
        return new List<Vector3>(_validPoints);
    }

    /// <summary>
    /// Gets whether decoration points have been generated.
    /// </summary>
    public bool IsGenerated => _isGenerated;

    /// <summary>
    /// Gets the list of spawned GameObjects (read-only).
    /// </summary>
    public List<GameObject> GetSpawnedObjects()
    {
        return new List<GameObject>(_spawnedObjects);
    }

    /// <summary>
    /// Debug registry status (press T key).
    /// </summary>
    private void DebugRegistry()
    {
        Debug.Log("=== ScattererRegistry Debug ===");
        Debug.Log($"Registry has {ScattererRegistry.GetRegisteredCount()} registered positions");
        Debug.Log($"This scatterer priority: {generationPriority}");
        Debug.Log($"This scatterer exclusion radius: {exclusionRadius}m");
        Debug.Log($"This scatterer valid points: {_validPoints.Count}");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw scatter area
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scatterRadius);

        // Draw raycast area
        Gizmos.color = Color.blue;
        Vector3 raycastOrigin = transform.position + Vector3.up * raycastHeight;
        Gizmos.DrawWireCube(raycastOrigin, new Vector3(scatterRadius * 2f, 0.1f, scatterRadius * 2f));

        // Draw exclusion radius for valid points
        if (_validPoints != null && _validPoints.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in _validPoints)
            {
                Gizmos.DrawWireSphere(point, exclusionRadius);
            }
        }
    }
}