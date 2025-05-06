using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// Manages the generation of procedural terrain using marching cubes and compute shaders.
/// </summary>
/// <remarks>
/// This class coordinates density field computation, mesh generation via marching cubes,
/// and chunk management for efficient terrain rendering and modification.
/// </remarks>
public class TerrainGenerator : MonoBehaviour
{
    #region Serialized Fields

    [Header("Generation Settings")]
    [SerializeField] private int numChunks = 5;
    [SerializeField] private int numPointsPerAxis = 32;
    [SerializeField] private float boundsSize = 600;
    [SerializeField] private int borderWidth = 1;
    [SerializeField] private float isoLevel = 0.0f;
    [SerializeField] private bool useFlatShading = false;

    [Header("Terrain Sharpness")]
    [SerializeField][Range(0.01f, 0.1f)] private float terrainGradient = 0.03f;
    [Tooltip("Lower values create sharper terrain features, higher values create smoother transitions")]

    [Header("Noise Settings")]
    [SerializeField] private float noiseScale = 1.5f;
    [SerializeField] private float noiseHeightMultiplier = 0.5f;
    [SerializeField] private int terrainSeed = 0;
    [SerializeField] private bool randomizeSeed = false;

    [Header("Blur Settings")]
    [SerializeField] private bool useBlur = true;
    [SerializeField][Range(1, 5)] private int blurRadius = 1;

    [Header("References")]
    [SerializeField] private ComputeShader meshCompute;
    [SerializeField] private ComputeShader densityCompute;
    [SerializeField] private ComputeShader blurCompute;
    [SerializeField] private ComputeShader explosionCompute;
    [SerializeField] private Material material;
    [SerializeField] private string groundLayerName = "Ground";

    [Header("Terrain Zones")]
    [SerializeField][Range(0.0f, 1.0f)] private float flatnessThreshold = 0.4f;
    [SerializeField][Range(0.0f, 1.0f)] private float detail3DStrength = 0.2f;

    [Header("Border Falloff")]
    [SerializeField][Range(0.0f, 1.0f)] private float borderFalloffStart = 0.3f;
    [SerializeField][Range(1.0f, 5.0f)] private float borderFalloffSteepness = 2.0f;
    [SerializeField] private bool enableBorderFalloff = true;

    [Header("Performance Tuning")]
    [SerializeField][Range(1, 10)] private int chunksPerFrame = 3;
    [Tooltip("Higher values = faster terrain updates but potential frame drops")]

    #endregion

    #region Public Properties

    /// <summary>Gets the number of points per chunk axis.</summary>
    public int NumPointsPerAxis => numPointsPerAxis;

    /// <summary>Gets the total size of the terrain bounds.</summary>
    public float BoundsSize => boundsSize;

    /// <summary>Gets the density texture used for terrain generation.</summary>
    public RenderTexture DensityTexture => _densityTexture;

    /// <summary>Gets the blurred density texture if blur is enabled.</summary>
    public RenderTexture BlurredDensityTexture => _blurredDensityTexture;

    /// <summary>Gets all terrain chunks.</summary>
    public Chunk[] Chunks => _chunks;

    /// <summary>Gets the current terrain seed value.</summary>
    public int CurrentSeed => terrainSeed;

    #endregion

    #region Private Fields

    // Compute buffers
    private ComputeBuffer _triangleBuffer;
    private ComputeBuffer _triCountBuffer;

    // Render textures
    private RenderTexture _densityTexture;
    private RenderTexture _blurredDensityTexture;

    // Terrain data
    private Chunk[] _chunks;
    private VertexData[] _vertexDataArray;
    private int _totalVerts;
    private int _textureSize;
    private int _groundLayer;

    // Generation tracking
    private System.Diagnostics.Stopwatch _timerGeneration;
    private bool _initialized = false;

    // Spatial partitioning for efficient chunk lookup
    private Dictionary<Vector3Int, Chunk> _chunkLookup;

    // Spatial hash for fast radius queries
    private Dictionary<Vector3Int, List<Chunk>> _spatialHash;
    private float _spatialCellSize = 100f; // Size of each spatial hash cell

    // Debug visualization
    [Header("Debug")]
    [SerializeField] private bool debugVisualization = false;
    private HashSet<Vector3Int> _lastCheckedCells;
    private int _lastTotalChunksChecked;

    #endregion

    #region Unity Lifecycle Methods

    /// <summary>
    /// Initializes and generates terrain when the component starts.
    /// </summary>
    private void Start()
    {
        ValidateComponents();
        Initialize();
        GenerateTerrain();
    }

    /// <summary>
    /// Handles input for terrain regeneration.
    /// </summary>
    private void Update()
    {
        // Press R key to regenerate terrain during play mode
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Regenerating terrain via keypress...");
            RegenerateTerrainRuntime();
        }
    }

    /// <summary>
    /// Releases all resources when the component is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        ReleaseResources();
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Validates that all required components are assigned.
    /// </summary>
    private void ValidateComponents()
    {
        if (meshCompute == null)
            Debug.LogError("Missing Marching Cubes compute shader reference!");

        if (densityCompute == null)
            Debug.LogError("Missing Terrain Density compute shader reference!");

        if (useBlur && blurCompute == null)
            Debug.LogError("Blur is enabled but Blur compute shader is not assigned!");

        _groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (_groundLayer < 0)
            Debug.LogWarning($"Layer '{groundLayerName}' not found. Using default layer.");
    }

    /// <summary>
    /// Initializes all resources needed for terrain generation.
    /// </summary>
    private void Initialize()
    {
        if (_initialized)
            ReleaseResources();

        // Calculate texture size based on chunk count and resolution
        _textureSize = numChunks * (numPointsPerAxis - 1) + 1;

        // Randomize seed if enabled
        if (randomizeSeed)
        {
            terrainSeed = Random.Range(0, 10000);
            Debug.Log($"Using random terrain seed: {terrainSeed}");
        }

        // Initialize spatial hash
        _spatialCellSize = boundsSize / (numChunks * 0.5f); // Size calibrated to chunk dimensions
        _spatialHash = new Dictionary<Vector3Int, List<Chunk>>();

        InitTextures();
        CreateBuffers();
        CreateChunks();

        _initialized = true;
    }

    /// <summary>
    /// Initializes the 3D textures for density and optional blur.
    /// </summary>
    private void InitTextures()
    {
        // Create primary density texture
        Create3DTexture(ref _densityTexture, _textureSize, "Density Texture");

        // Create blur texture if enabled
        if (useBlur)
        {
            Create3DTexture(ref _blurredDensityTexture, _textureSize, "Blurred Density Texture");
        }

        // Set up compute shader textures
        densityCompute.SetTexture(0, "DensityTexture", _densityTexture);

        if (useBlur)
        {
            blurCompute.SetTexture(0, "Source", _densityTexture);
            blurCompute.SetTexture(0, "Result", _blurredDensityTexture);
            meshCompute.SetTexture(0, "DensityTexture", _blurredDensityTexture);
        }
        else
        {
            meshCompute.SetTexture(0, "DensityTexture", _densityTexture);
        }
    }

    /// <summary>
    /// Creates the necessary compute buffers for vertex and triangle data.
    /// </summary>
    private void CreateBuffers()
    {
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5; // Conservative estimate of max triangles per voxel
        int maxVertexCount = maxTriangleCount * 3;

        _triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
        _vertexDataArray = new VertexData[maxVertexCount];
    }

    /// <summary>
    /// Creates the terrain chunks with appropriate sizes and positions.
    /// </summary>
    private void CreateChunks()
    {
        int chunksPerAxis = numChunks;
        _chunkLookup = new Dictionary<Vector3Int, Chunk>();

        // Allocate array for all chunks
        _chunks = new Chunk[chunksPerAxis * chunksPerAxis * chunksPerAxis];
        float chunkSize = boundsSize / chunksPerAxis;
        int index = 0;

        // Create chunks in a 3D grid
        for (int y = 0; y < chunksPerAxis; y++)
        {
            for (int x = 0; x < chunksPerAxis; x++)
            {
                for (int z = 0; z < chunksPerAxis; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);

                    // Center chunks around origin (0,0,0)
                    Vector3 centre = new Vector3(
                        (-(chunksPerAxis - 1f) / 2 + x) * chunkSize,
                        (-(chunksPerAxis - 1f) / 2 + y) * chunkSize,
                        (-(chunksPerAxis - 1f) / 2 + z) * chunkSize
                    );

                    // Create game object to hold the chunk
                    GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})")
                    {
                        transform = { parent = transform },
                        layer = _groundLayer
                    };

                    // Initialize chunk and set material
                    Chunk chunk = new Chunk(coord, centre, chunkSize, meshHolder);
                    chunk.SetMaterial(material);
                    chunk.SetLayer(_groundLayer);

                    // Store chunk references
                    _chunks[index] = chunk;
                    _chunkLookup[coord] = chunk;

                    // Add to spatial hash
                    AddChunkToSpatialHash(chunk);

                    index++;
                }
            }
        }
    }

    /// <summary>
    /// Adds a chunk to the spatial hash system for efficient spatial queries.
    /// </summary>
    /// <param name="chunk">The chunk to add to the spatial hash.</param>
    private void AddChunkToSpatialHash(Chunk chunk)
    {
        // Get the spatial cell coordinate for this chunk
        Vector3Int cell = WorldToSpatialCell(chunk.Centre);

        // Create list for this cell if it doesn't exist
        if (!_spatialHash.TryGetValue(cell, out var chunks))
        {
            chunks = new List<Chunk>();
            _spatialHash[cell] = chunks;
        }

        // Add chunk to the appropriate cell
        chunks.Add(chunk);
    }

    /// <summary>
    /// Removes a chunk from the spatial hash system.
    /// </summary>
    /// <param name="chunk">The chunk to remove from the spatial hash.</param>
    private void RemoveChunkFromSpatialHash(Chunk chunk)
    {
        Vector3Int cell = WorldToSpatialCell(chunk.Centre);

        if (_spatialHash.TryGetValue(cell, out var chunks))
        {
            chunks.Remove(chunk);

            // Remove the cell entirely if empty
            if (chunks.Count == 0)
            {
                _spatialHash.Remove(cell);
            }
        }
    }

    /// <summary>
    /// Converts a world position to a spatial hash cell coordinate.
    /// </summary>
    /// <param name="worldPos">The world position to convert.</param>
    /// <returns>The spatial hash cell coordinate.</returns>
    private Vector3Int WorldToSpatialCell(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / _spatialCellSize),
            Mathf.FloorToInt(worldPos.y / _spatialCellSize),
            Mathf.FloorToInt(worldPos.z / _spatialCellSize)
        );
    }

    /// <summary>
    /// Creates a 3D texture with the specified settings.
    /// </summary>
    /// <param name="texture">Reference to the texture to be created or updated.</param>
    /// <param name="size">The size of the 3D texture (same for all dimensions).</param>
    /// <param name="textureName">Name for the texture for debugging.</param>
    private void Create3DTexture(ref RenderTexture texture, int size, string textureName)
    {
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;

        // Check if we need to create a new texture
        bool needsNewTexture = texture == null || !texture.IsCreated() ||
                              texture.width != size || texture.height != size ||
                              texture.volumeDepth != size || texture.graphicsFormat != format;

        if (needsNewTexture)
        {
            // Release existing texture
            if (texture != null && texture.IsCreated())
            {
                texture.Release();
            }

            // Create new texture with proper settings
            texture = new RenderTexture(size, size, 0)
            {
                graphicsFormat = format,
                volumeDepth = size,
                enableRandomWrite = true,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = textureName
            };
            texture.Create();
        }
    }

    #endregion

    #region Terrain Generation Methods

    /// <summary>
    /// Generates the complete terrain by computing density and creating all chunks.
    /// </summary>
    private void GenerateTerrain()
    {
        _timerGeneration = System.Diagnostics.Stopwatch.StartNew();
        _totalVerts = 0;

        // Compute density field
        ComputeDensity();

        // Generate all chunks
        foreach (var chunk in _chunks)
        {
            GenerateChunk(chunk);
        }

        _timerGeneration.Stop();
        Debug.Log($"Generation Time: {_timerGeneration.ElapsedMilliseconds} ms");
        Debug.Log($"Total vertices: {_totalVerts}");
    }

    /// <summary>
    /// Computes the density field using the density and blur compute shaders.
    /// </summary>
    private void ComputeDensity()
    {
        // Set common parameters
        densityCompute.SetInt("textureSize", _textureSize);
        densityCompute.SetFloat("boundsSize", boundsSize);
        densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
        densityCompute.SetFloat("noiseScale", noiseScale);
        densityCompute.SetInt("borderWidth", borderWidth);
        densityCompute.SetFloat("terrainSeed", terrainSeed);
        densityCompute.SetFloat("terrainGradient", terrainGradient);

        // Set terrain zone parameters
        densityCompute.SetFloat("flatnessThreshold", flatnessThreshold);
        densityCompute.SetFloat("detail3DStrength", detail3DStrength);

        // Set border falloff parameters
        densityCompute.SetFloat("borderFalloffStart", borderFalloffStart);
        densityCompute.SetFloat("borderFalloffSteepness", borderFalloffSteepness);
        densityCompute.SetInt("enableBorderFalloff", enableBorderFalloff ? 1 : 0);

        // Dispatch density computation
        ComputeHelper.Dispatch(densityCompute, _textureSize, _textureSize, _textureSize);

        // Apply blur if enabled
        if (useBlur)
        {
            blurCompute.SetInt("textureSize", _textureSize);
            blurCompute.SetInt("blurRadius", blurRadius);
            ComputeHelper.Dispatch(blurCompute, _textureSize, _textureSize, _textureSize);
        }
    }

    /// <summary>
    /// Generates a mesh for a specific chunk using marching cubes.
    /// </summary>
    /// <param name="chunk">The chunk to generate.</param>
    private void GenerateChunk(Chunk chunk)
    {
        if (chunk == null)
            return;

        int numVoxelsPerAxis = numPointsPerAxis - 1;

        // Setup mesh compute shader parameters
        meshCompute.SetInt("textureSize", _textureSize);
        meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
        meshCompute.SetFloat("isoLevel", isoLevel);
        meshCompute.SetFloat("boundsSize", boundsSize);

        // Reset triangle buffer counter and set buffer
        _triangleBuffer.SetCounterValue(0);
        meshCompute.SetBuffer(0, "triangles", _triangleBuffer);

        // Set chunk coordinates for the compute shader
        Vector3 chunkCoord = (Vector3)chunk.Id * numVoxelsPerAxis;
        meshCompute.SetVector("chunkCoord", chunkCoord);

        // Run marching cubes algorithm
        ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis);

        // Get vertex count from indirect buffer
        int[] vertexCountData = new int[1];
        _triCountBuffer.SetData(vertexCountData);
        ComputeBuffer.CopyCount(_triangleBuffer, _triCountBuffer, 0);
        _triCountBuffer.GetData(vertexCountData);

        int numVertices = vertexCountData[0] * 3;

        // Create mesh if vertices were generated
        if (numVertices > 0)
        {
            _triangleBuffer.GetData(_vertexDataArray, 0, 0, numVertices);
            chunk.CreateMesh(_vertexDataArray, numVertices, useFlatShading);
            _totalVerts += numVertices;
        }
        else
        {
            // Handle empty chunks
            chunk.CreateMesh(_vertexDataArray, 0, useFlatShading);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the material for all chunks in the terrain.
    /// </summary>
    /// <param name="newMaterial">The material to apply to all chunks.</param>
    public void SetMaterial(Material newMaterial)
    {
        if (newMaterial == null)
            return;

        // Store the material reference
        material = newMaterial;

        // Apply to existing chunks
        if (_chunks != null)
        {
            foreach (var chunk in _chunks)
            {
                if (chunk != null)
                {
                    chunk.SetMaterial(material);
                }
            }
        }
    }

    /// <summary>
    /// Updates all chunk materials without regenerating terrain.
    /// </summary>
    public void UpdateChunkMaterials()
    {
        SetMaterial(material);
    }

    /// <summary>
    /// Regenerates the terrain during runtime.
    /// </summary>
    public void RegenerateTerrainRuntime()
    {
        Debug.Log("Starting terrain regeneration...");

        // Clean up existing resources
        ReleaseResources();

        // Re-initialize and generate new terrain
        Initialize();
        GenerateTerrain();
    }

    /// <summary>
    /// Sets a new seed and regenerates the terrain.
    /// </summary>
    /// <param name="newSeed">The new seed value to use.</param>
    public void SetSeed(int newSeed)
    {
        terrainSeed = newSeed;
        RegenerateTerrainRuntime();
    }

    /// <summary>
    /// Creates an explosion effect that modifies the terrain density at the specified position.
    /// </summary>
    /// <param name="worldPosition">Position in world space where the explosion should occur.</param>
    /// <param name="radius">Radius of the explosion effect.</param>
    /// <param name="strength">Strength of the explosion (higher values = bigger crater).</param>
    public void CreateExplosion(Vector3 worldPosition, float radius, float strength)
    {
        if (explosionCompute == null)
        {
            Debug.LogError("Explosion compute shader not assigned!");
            return;
        }

        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        // Find chunks affected by the explosion
        List<Chunk> affectedChunks = FindChunksInRadius(worldPosition, radius);
        timer.Stop();

        if (affectedChunks.Count == 0)
        {
            Debug.LogWarning($"Explosion at {worldPosition} with radius {radius} did not affect any chunks.");
            return;
        }

        // Apply explosion to density texture
        Vector3Int minCell = WorldToSpatialCell(worldPosition - new Vector3(radius, radius, radius));
        Vector3Int maxCell = WorldToSpatialCell(worldPosition + new Vector3(radius, radius, radius));

        // Log performance data
        Debug.Log($"Explosion at {worldPosition}, radius {radius}:");
        Debug.Log($"- Found {affectedChunks.Count} affected chunks out of {_chunks.Length} total");
        Debug.Log($"- Last chunks checked: {_lastTotalChunksChecked}");
        Debug.Log($"- Query time: {timer.ElapsedMilliseconds}ms");
        Debug.Log($"- Spatial region: From {minCell} to {maxCell}");
        Debug.Log($"Spatial cell size: {_spatialCellSize}, Chunk size: {boundsSize / numChunks}");

        ApplyExplosionToDensity(worldPosition, radius, strength);

        // Regenerate only affected chunks for better performance
        StartCoroutine(RegenerateChunksProgressively(affectedChunks, worldPosition));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Releases all allocated resources.
    /// </summary>
    private void ReleaseResources()
    {
        // Release compute buffers
        if (_triangleBuffer != null)
        {
            _triangleBuffer.Release();
            _triangleBuffer = null;
        }

        if (_triCountBuffer != null)
        {
            _triCountBuffer.Release();
            _triCountBuffer = null;
        }

        // Release render textures
        if (_densityTexture != null && _densityTexture.IsCreated())
        {
            _densityTexture.Release();
            _densityTexture = null;
        }

        if (_blurredDensityTexture != null && _blurredDensityTexture.IsCreated())
        {
            _blurredDensityTexture.Release();
            _blurredDensityTexture = null;
        }

        // Release all chunks
        if (_chunks != null)
        {
            foreach (Chunk chunk in _chunks)
            {
                if (chunk != null)
                {
                    // Destroy the GameObject if it exists
                    if (chunk.gameObject != null)
                    {
                        DestroyImmediate(chunk.gameObject);
                    }
                    chunk.Release();
                }
            }
            _chunks = null;
        }

        // Clear data structures
        if (_chunkLookup != null)
        {
            _chunkLookup.Clear();
            _chunkLookup = null;
        }

        // Clear spatial hash
        if (_spatialHash != null)
        {
            _spatialHash.Clear();
            _spatialHash = null;
        }

        // Clear large arrays
        _vertexDataArray = null;

        // Reset state
        _initialized = false;
    }

    /// <summary>
    /// Finds all chunks that intersect with a sphere defined by center and radius.
    /// Uses spatial hash for efficient O(k) lookup where k is the number of relevant chunks.
    /// </summary>
    /// <param name="center">The center of the sphere in world space.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <returns>A list of chunks that intersect with the sphere.</returns>
    private List<Chunk> FindChunksInRadius(Vector3 center, float radius)
    {
        // Use spatial hash for efficient lookup
        List<Chunk> affectedChunks = new List<Chunk>();
        float sqrRadius = radius * radius;

        // Track performance metrics
        int chunkCheckCount = 0;
        _lastCheckedCells = new HashSet<Vector3Int>();

        // Calculate the min and max cell coordinates that could be affected
        Vector3Int minCellCoord = WorldToSpatialCell(center - new Vector3(radius, radius, radius));
        Vector3Int maxCellCoord = WorldToSpatialCell(center + new Vector3(radius, radius, radius));

        // Track chunks we've already checked to avoid duplicates (chunks can appear in multiple cells)
        HashSet<Chunk> checkedChunks = new HashSet<Chunk>();

        // Only check cells that might be affected
        for (int x = minCellCoord.x; x <= maxCellCoord.x; x++)
        {
            for (int y = minCellCoord.y; y <= maxCellCoord.y; y++)
            {
                for (int z = minCellCoord.z; z <= maxCellCoord.z; z++)
                {
                    Vector3Int cellCoord = new Vector3Int(x, y, z);
                    _lastCheckedCells.Add(cellCoord); // Track for visualization

                    // Check if this cell contains any chunks
                    if (_spatialHash.TryGetValue(cellCoord, out var chunksInCell))
                    {
                        foreach (var chunk in chunksInCell)
                        {
                            // Skip if we've already checked this chunk
                            if (checkedChunks.Contains(chunk))
                                continue;

                            checkedChunks.Add(chunk);
                            chunkCheckCount++; // Count total chunks examined

                            // Calculate the closest point on the chunk to the explosion center
                            Vector3 closestPoint = GetClosestPointOnChunk(chunk, center);

                            // Check if this closest point is within explosion radius
                            if ((center - closestPoint).sqrMagnitude <= sqrRadius)
                            {
                                affectedChunks.Add(chunk);
                            }
                        }
                    }
                }
            }
        }

        // Store performance metric
        _lastTotalChunksChecked = chunkCheckCount;

        // Log spatial query statistics
        Debug.Log($"Spatial query stats: Checked {chunkCheckCount} chunks across {_lastCheckedCells.Count} cells " +
                 $"out of {_chunks.Length} total chunks");

        return affectedChunks;
    }

    /// <summary>
    /// Gets the closest point on a chunk to a target position.
    /// </summary>
    /// <param name="chunk">The chunk to check.</param>
    /// <param name="targetPos">The target position.</param>
    /// <returns>The closest point on the chunk to the target position.</returns>
    private Vector3 GetClosestPointOnChunk(Chunk chunk, Vector3 targetPos)
    {
        // Calculate chunk bounds
        Vector3 halfSize = Vector3.one * chunk.Size * 0.5f;
        Vector3 min = chunk.Centre - halfSize;
        Vector3 max = chunk.Centre + halfSize;

        // Clamp target position to chunk bounds
        return new Vector3(
            Mathf.Clamp(targetPos.x, min.x, max.x),
            Mathf.Clamp(targetPos.y, min.y, max.y),
            Mathf.Clamp(targetPos.z, min.z, max.z)
        );
    }

    /// <summary>
    /// Applies explosion effect to the density texture.
    /// Only processes the affected region for better performance.
    /// </summary>
    /// <param name="worldPosition">The world space position of the explosion.</param>
    /// <param name="radius">The radius of the explosion.</param>
    /// <param name="strength">The strength of the explosion.</param>
    private void ApplyExplosionToDensity(Vector3 worldPosition, float radius, float strength)
    {
        // Calculate texture space coordinates
        Vector3 normalizedPos = (worldPosition / boundsSize) + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 texturePos = normalizedPos * _textureSize;

        // Convert world-space radius to texture-space radius
        float textureRadius = radius / boundsSize * _textureSize;

        // Calculate affected region bounds (only process what's needed)
        Vector3Int minBound = Vector3Int.FloorToInt(texturePos - new Vector3(textureRadius, textureRadius, textureRadius));
        Vector3Int maxBound = Vector3Int.CeilToInt(texturePos + new Vector3(textureRadius, textureRadius, textureRadius));

        // Clamp to texture boundaries
        minBound = Vector3Int.Max(Vector3Int.zero, minBound);
        maxBound = Vector3Int.Min(new Vector3Int(_textureSize - 1, _textureSize - 1, _textureSize - 1), maxBound);

        // Calculate region dimensions
        int sizeX = maxBound.x - minBound.x + 1;
        int sizeY = maxBound.y - minBound.y + 1;
        int sizeZ = maxBound.z - minBound.z + 1;

        // Select appropriate texture based on blur setting
        RenderTexture targetTexture = useBlur ? _blurredDensityTexture : _densityTexture;

        // Set compute shader parameters
        explosionCompute.SetVector("regionMin", new Vector4(minBound.x, minBound.y, minBound.z, 0));
        explosionCompute.SetVector("regionMax", new Vector4(maxBound.x, maxBound.y, maxBound.z, 0));
        explosionCompute.SetTexture(0, "DensityTexture", targetTexture);
        explosionCompute.SetVector("explosionCenter", texturePos);
        explosionCompute.SetFloat("radius", textureRadius);
        explosionCompute.SetFloat("strength", strength);
        explosionCompute.SetInt("textureSize", _textureSize);
        explosionCompute.SetFloat("boundsSize", boundsSize);
        explosionCompute.SetFloat("isoLevel", isoLevel);

        // Only dispatch compute shader for the affected region
        ComputeHelper.Dispatch(explosionCompute, sizeX, sizeY, sizeZ);
    }

    /// <summary>
    /// Regenerates chunks progressively over multiple frames to maintain performance.
    /// </summary>
    /// <param name="chunks">The list of chunks to regenerate.</param>
    /// <param name="explosionCenter">The center of the explosion for sorting.</param>
    /// <returns>An IEnumerator for coroutine execution.</returns>
    private System.Collections.IEnumerator RegenerateChunksProgressively(List<Chunk> chunks, Vector3 explosionCenter)
    {
        // Sort chunks by distance to explosion center for better visual experience
        chunks.Sort((a, b) =>
            Vector3.Distance(a.Centre, explosionCenter).CompareTo(
            Vector3.Distance(b.Centre, explosionCenter)));

        // Process chunks over multiple frames for better performance
        for (int i = 0; i < chunks.Count; i += chunksPerFrame)
        {
            float startTime = Time.realtimeSinceStartup;

            int count = Mathf.Min(chunksPerFrame, chunks.Count - i);
            for (int j = 0; j < count; j++)
            {
                GenerateChunk(chunks[i + j]);
            }

            float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"Updated {count} chunks in {elapsed:F2}ms");

            // Wait until next frame to continue processing
            yield return null;
        }
    }

    private void OnDrawGizmos()
    {
        // Only draw in play mode when debugging is enabled
        if (!Application.isPlaying || !debugVisualization)
            return;

        // Draw chunk boundaries
        if (_chunks != null)
        {
            Gizmos.color = Color.blue;
            foreach (var chunk in _chunks)
            {
                if (chunk != null)
                    chunk.DrawBoundsGizmo(Color.blue);
            }
        }

        // Draw spatial hash cells that were checked in the last explosion
        if (_lastCheckedCells != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 1f); // Semi-transparent yellow

            foreach (var cell in _lastCheckedCells)
            {
                Vector3 cellCenter = new Vector3(
                    (cell.x + 0.5f) * _spatialCellSize,
                    (cell.y + 0.5f) * _spatialCellSize,
                    (cell.z + 0.5f) * _spatialCellSize
                );

                Gizmos.DrawWireCube(cellCenter, Vector3.one * _spatialCellSize);
            }
        }

    }
}
#endregion