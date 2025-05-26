using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the generation of procedural terrain using marching cubes and compute shaders.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    #region Enums and Classes

    public enum TerrainPreset
    {
        Custom,
        RollingHills,
        Mountains,
        Desert,
        Canyons,
        Islands,
        Alien
    }

    #endregion

    #region Serialized Fields

    [Header("Quick Setup")]
    [SerializeField] private TerrainPreset preset = TerrainPreset.Custom;
    [SerializeField][Tooltip("Apply the selected preset")] private bool applyPreset = false;

    [Header("World Structure")]
    [SerializeField]
    [Tooltip("Physical size of the entire terrain in world units")]
    [Range(100, 2000)] private float terrainScale = 600f;

    [SerializeField]
    [Tooltip("Number of chunks in each dimension (5 = 5󬊅 = 125 chunks total)")]
    [Range(1, 10)] private int worldSize = 5;

    [SerializeField]
    [Tooltip("Vertices per chunk edge. Higher = more detail but slower. (32 is usually good)")]
    [Range(16, 64)] private int chunkResolution = 32;

    [Header("Terrain Shape")]
    [SerializeField]
    [Tooltip("Height offset of the terrain base plane")]
    [Range(-50f, 50f)] private float baseHeight = 0f;

    [SerializeField]
    [Tooltip("Size of terrain features. Larger = broader mountains/valleys")]
    [Range(10f, 500f)] private float featureScale = 100f;

    [SerializeField]
    [Tooltip("Height of terrain features")]
    [Range(10f, 300f)] private float featureAmplitude = 50f;

    [SerializeField]
    [Tooltip("Number of noise layers. More = more detail")]
    [Range(3, 9)] private int noiseOctaves = 6;

    [SerializeField]
    [Tooltip("How much each noise layer contributes (0.4-0.6 recommended)")]
    [Range(0.3f, 0.7f)] private float noisePersistence = 0.5f;

    [SerializeField]
    [Tooltip("Frequency multiplier between octaves (1.8-2.2 recommended)")]
    [Range(1.5f, 2.5f)] private float noiseLacunarity = 2.0f;

    [Header("Terrain Features")]
    [SerializeField]
    [Tooltip("Warps terrain for more organic shapes")]
    [Range(0.0f, 6.0f)] private float domainWarpStrength = 0.3f;

    [SerializeField]
    [Tooltip("Creates caves and 3D overhangs")]
    [Range(0.0f, 1.0f)] private float caveStrength = 0.2f;

    [SerializeField]
    [Tooltip("Makes mountains sharp and ridge-like")]
    [Range(0.0f, 1.0f)] private float ridgeStrength = 0.0f;

    [SerializeField]
    [Tooltip("Y level for hard floor (-1000 to disable)")]
    [Range(-1000f, 100f)] private float hardFloorHeight = -50f;

    [SerializeField]
    [Tooltip("Blend distance for hard floor")]
    [Range(5f, 50f)] private float hardFloorBlend = 20f;

    [Header("Terrain Smoothing")]
    [SerializeField]
    [Tooltip("Apply smoothing to reduce jagged edges")]
    private bool useBlur = true;

    [SerializeField]
    [Tooltip("Smoothing radius (1-2 for light smoothing, 3-5 for heavy)")]
    [Range(1, 5)] private int blurRadius = 1;

    [Header("Generation")]
    [SerializeField]
    [Tooltip("Seed for random terrain generation")]
    private int terrainSeed = 0;

    [SerializeField]
    [Tooltip("Generate random seed on start")]
    private bool randomizeSeed = false;

    [SerializeField]
    [Tooltip("Surface level for mesh generation (usually keep at 0)")]
    [Range(-1.0f, 1.0f)] private float isoLevel = 0.0f;

    [Header("Rendering")]
    [SerializeField]
    [Tooltip("Use flat shading for low-poly look")]
    private bool useFlatShading = false;

    [SerializeField]
    [Tooltip("Material for the terrain")]
    private Material material;

    [SerializeField]
    [Tooltip("Layer name for terrain collision")]
    private string groundLayerName = "Ground";

    [Header("Required Shaders")]
    [SerializeField] private ComputeShader meshCompute;
    [SerializeField] private ComputeShader densityCompute;
    [SerializeField] private ComputeShader blurCompute;
    [SerializeField] private ComputeShader explosionCompute;

    [Header("Performance")]
    [SerializeField]
    [Tooltip("Chunks updated per frame during explosions")]
    [Range(1, 10)] private int chunksPerFrame = 3;

    [Header("Debug")]
    [SerializeField] private bool debugVisualization = false;

    #endregion

    #region Preset Values

    private void ApplyPresetValues(TerrainPreset presetType)
    {
        switch (presetType)
        {
            case TerrainPreset.RollingHills:
                baseHeight = 0f;
                featureScale = 150f;
                featureAmplitude = 40f;
                noiseOctaves = 5;
                noisePersistence = 0.5f;
                noiseLacunarity = 2.0f;
                domainWarpStrength = 0.2f;
                caveStrength = 0.1f;
                ridgeStrength = 0.0f;
                hardFloorHeight = -30f;
                hardFloorBlend = 20f;
                useBlur = true;
                blurRadius = 2;
                break;

            case TerrainPreset.Mountains:
                baseHeight = -32f;
                featureScale = 96f;
                featureAmplitude = 72f;
                noiseOctaves = 4;
                noisePersistence = 0.348f;
                noiseLacunarity = 1.755f;
                domainWarpStrength = 0.1f;
                caveStrength = 0.1f;
                ridgeStrength = 0.45f;
                hardFloorHeight = -60f;
                hardFloorBlend = 30f;
                useBlur = true;
                blurRadius = 3;
                break;

            case TerrainPreset.Desert:
                baseHeight = 0f;
                featureScale = 64f;
                featureAmplitude = 20f;
                noiseOctaves = 8;
                noisePersistence = 0.333f;
                noiseLacunarity = 1.5f;
                domainWarpStrength = 0.5f;
                caveStrength = 0.0f;
                ridgeStrength = 0.87f;
                hardFloorHeight = 0f;
                hardFloorBlend = 5f;
                useBlur = true;
                blurRadius = 2;
                break;

            case TerrainPreset.Canyons:
                baseHeight = -50f;
                featureScale = 384f;
                featureAmplitude = 128f;
                noiseOctaves = 7;
                noisePersistence = 0.425f;
                noiseLacunarity = 2.0f;
                domainWarpStrength = 0.5f;
                caveStrength = 0.8f;
                ridgeStrength = 0.8f;
                hardFloorHeight = 0f;
                hardFloorBlend = 5f;
                useBlur = true;
                blurRadius = 3;
                break;

            case TerrainPreset.Islands:
                baseHeight = -20f;
                featureScale = 100f;
                featureAmplitude = 60f;
                noiseOctaves = 5;
                noisePersistence = 0.5f;
                noiseLacunarity = 1.9f;
                domainWarpStrength = 0.4f;
                caveStrength = 0.2f;
                ridgeStrength = 0.2f;
                hardFloorHeight = -80f;
                hardFloorBlend = 20f;
                useBlur = true;
                blurRadius = 1;
                break;

            case TerrainPreset.Alien:
                baseHeight = -27.4f;
                featureScale = 384f;
                featureAmplitude = 104f;
                noiseOctaves = 4;
                noisePersistence = 0.333f;
                noiseLacunarity = 1.982f;
                domainWarpStrength = 1f;
                caveStrength = 0.75f;
                ridgeStrength = 1f;
                hardFloorHeight = 0f; // Disabled
                hardFloorBlend = 5f;
                useBlur = true;
                blurRadius = 2;
                break;
        }

        Debug.Log($"Applied {presetType} preset");
    }

    #endregion

    #region Public Properties

    /// <summary>Gets the number of points per chunk axis.</summary>
    public int NumPointsPerAxis => chunkResolution;

    /// <summary>Gets the total size of the terrain bounds.</summary>
    public float BoundsSize => terrainScale;

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
    private Dictionary<Vector3Int, List<Chunk>> _spatialHash;
    private float _spatialCellSize = 100f;

    // Debug
    private HashSet<Vector3Int> _lastCheckedCells;
    private int _lastTotalChunksChecked;

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        ValidateComponents();
        Initialize();
        GenerateTerrain();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Regenerating terrain...");
            RegenerateTerrainRuntime();
        }
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        if (applyPreset && preset != TerrainPreset.Custom)
        {
            ApplyPresetValues(preset);
            applyPreset = false;
        }
    }

    #endregion

    #region Initialization Methods

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

    private void Initialize()
    {
        if (_initialized)
            ReleaseResources();

        _textureSize = worldSize * (chunkResolution - 1) + 1;

        if (randomizeSeed)
        {
            terrainSeed = Random.Range(0, 10000);
            Debug.Log($"Using random terrain seed: {terrainSeed}");
        }

        _spatialCellSize = terrainScale / (worldSize * 0.5f);
        _spatialHash = new Dictionary<Vector3Int, List<Chunk>>();

        InitTextures();
        CreateBuffers();
        CreateChunks();

        _initialized = true;
    }

    private void InitTextures()
    {
        Create3DTexture(ref _densityTexture, _textureSize, "Density Texture");

        if (useBlur)
        {
            Create3DTexture(ref _blurredDensityTexture, _textureSize, "Blurred Density Texture");
        }

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

    private void CreateBuffers()
    {
        int numVoxelsPerAxis = chunkResolution - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;
        int maxVertexCount = maxTriangleCount * 3;

        _triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
        _vertexDataArray = new VertexData[maxVertexCount];
    }

    private void CreateChunks()
    {
        int chunksPerAxis = worldSize;
        _chunkLookup = new Dictionary<Vector3Int, Chunk>();

        _chunks = new Chunk[chunksPerAxis * chunksPerAxis * chunksPerAxis];
        float chunkSize = terrainScale / chunksPerAxis;
        int index = 0;

        for (int y = 0; y < chunksPerAxis; y++)
        {
            for (int x = 0; x < chunksPerAxis; x++)
            {
                for (int z = 0; z < chunksPerAxis; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);

                    Vector3 centre = new Vector3(
                        (-(chunksPerAxis - 1f) / 2 + x) * chunkSize,
                        (-(chunksPerAxis - 1f) / 2 + y) * chunkSize,
                        (-(chunksPerAxis - 1f) / 2 + z) * chunkSize
                    );

                    GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})")
                    {
                        transform = { parent = transform },
                        layer = _groundLayer
                    };

                    Chunk chunk = new Chunk(coord, centre, chunkSize, meshHolder);
                    chunk.SetMaterial(material);
                    chunk.SetLayer(_groundLayer);

                    _chunks[index] = chunk;
                    _chunkLookup[coord] = chunk;
                    AddChunkToSpatialHash(chunk);

                    index++;
                }
            }
        }
    }

    private void AddChunkToSpatialHash(Chunk chunk)
    {
        Vector3Int cell = WorldToSpatialCell(chunk.Centre);

        if (!_spatialHash.TryGetValue(cell, out var chunks))
        {
            chunks = new List<Chunk>();
            _spatialHash[cell] = chunks;
        }

        chunks.Add(chunk);
    }

    private void RemoveChunkFromSpatialHash(Chunk chunk)
    {
        Vector3Int cell = WorldToSpatialCell(chunk.Centre);

        if (_spatialHash.TryGetValue(cell, out var chunks))
        {
            chunks.Remove(chunk);

            if (chunks.Count == 0)
            {
                _spatialHash.Remove(cell);
            }
        }
    }

    private Vector3Int WorldToSpatialCell(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / _spatialCellSize),
            Mathf.FloorToInt(worldPos.y / _spatialCellSize),
            Mathf.FloorToInt(worldPos.z / _spatialCellSize)
        );
    }

    private void Create3DTexture(ref RenderTexture texture, int size, string textureName)
    {
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;

        bool needsNewTexture = texture == null || !texture.IsCreated() ||
                              texture.width != size || texture.height != size ||
                              texture.volumeDepth != size || texture.graphicsFormat != format;

        if (needsNewTexture)
        {
            if (texture != null && texture.IsCreated())
            {
                texture.Release();
            }

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

    private void GenerateTerrain()
    {
        _timerGeneration = System.Diagnostics.Stopwatch.StartNew();
        _totalVerts = 0;

        ComputeDensity();

        foreach (var chunk in _chunks)
        {
            GenerateChunk(chunk);
        }

        _timerGeneration.Stop();
        Debug.Log($"Generation Time: {_timerGeneration.ElapsedMilliseconds} ms");
        Debug.Log($"Total vertices: {_totalVerts}");
    }

    private void ComputeDensity()
    {
        // Pass parameters to compute shader
        densityCompute.SetInt("textureSize", _textureSize);
        densityCompute.SetFloat("boundsSize", terrainScale);
        densityCompute.SetFloat("terrainSeed", terrainSeed);

        // New terrain shape parameters
        densityCompute.SetFloat("baseHeight", baseHeight);
        densityCompute.SetFloat("terrainScale", featureScale);
        densityCompute.SetFloat("terrainAmplitude", featureAmplitude);
        densityCompute.SetInt("octaves", noiseOctaves);
        densityCompute.SetFloat("persistence", noisePersistence);
        densityCompute.SetFloat("lacunarity", noiseLacunarity);

        // Feature parameters
        densityCompute.SetFloat("warpStrength", domainWarpStrength);
        densityCompute.SetFloat("hardFloorHeight", hardFloorHeight);
        densityCompute.SetFloat("hardFloorBlend", hardFloorBlend);
        densityCompute.SetFloat("cavesStrength", caveStrength);
        densityCompute.SetFloat("ridgeStrength", ridgeStrength);

        ComputeHelper.Dispatch(densityCompute, _textureSize, _textureSize, _textureSize);

        if (useBlur)
        {
            blurCompute.SetInt("textureSize", _textureSize);
            blurCompute.SetInt("blurRadius", blurRadius);
            ComputeHelper.Dispatch(blurCompute, _textureSize, _textureSize, _textureSize);
        }
    }

    private void GenerateChunk(Chunk chunk)
    {
        if (chunk == null)
            return;

        int numVoxelsPerAxis = chunkResolution - 1;

        meshCompute.SetInt("textureSize", _textureSize);
        meshCompute.SetInt("numPointsPerAxis", chunkResolution);
        meshCompute.SetFloat("isoLevel", isoLevel);
        meshCompute.SetFloat("boundsSize", terrainScale);

        _triangleBuffer.SetCounterValue(0);
        meshCompute.SetBuffer(0, "triangles", _triangleBuffer);

        Vector3 chunkCoord = (Vector3)chunk.Id * numVoxelsPerAxis;
        meshCompute.SetVector("chunkCoord", chunkCoord);

        ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis);

        int[] vertexCountData = new int[1];
        _triCountBuffer.SetData(vertexCountData);
        ComputeBuffer.CopyCount(_triangleBuffer, _triCountBuffer, 0);
        _triCountBuffer.GetData(vertexCountData);

        int numVertices = vertexCountData[0] * 3;

        if (numVertices > 0)
        {
            _triangleBuffer.GetData(_vertexDataArray, 0, 0, numVertices);
            chunk.CreateMesh(_vertexDataArray, numVertices, useFlatShading);
            _totalVerts += numVertices;
        }
        else
        {
            chunk.CreateMesh(_vertexDataArray, 0, useFlatShading);
        }
    }

    #endregion

    #region Public Methods

    public void SetMaterial(Material newMaterial)
    {
        if (newMaterial == null)
            return;

        material = newMaterial;

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

    public void UpdateChunkMaterials()
    {
        SetMaterial(material);
    }

    public void RegenerateTerrainRuntime()
    {
        Debug.Log("Starting terrain regeneration...");
        ReleaseResources();
        Initialize();
        GenerateTerrain();

        // Notify foliage placer if it exists
        GPUFoliagePlacer foliagePlacer = Object.FindFirstObjectByType<GPUFoliagePlacer>();
        if (foliagePlacer != null)
        {
            foliagePlacer.OnTerrainRegenerated();
        }

    }

    public void SetSeed(int newSeed)
    {
        terrainSeed = newSeed;
        RegenerateTerrainRuntime();
    }

    public void CreateExplosion(Vector3 worldPosition, float radius, float strength)
    {
        if (explosionCompute == null)
        {
            Debug.LogError("Explosion compute shader not assigned!");
            return;
        }

        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        List<Chunk> affectedChunks = FindChunksInRadius(worldPosition, radius);
        timer.Stop();

        if (affectedChunks.Count == 0)
        {
            Debug.LogWarning($"Explosion at {worldPosition} with radius {radius} did not affect any chunks.");
            return;
        }

        Vector3 normalizedPos = (worldPosition / terrainScale) + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 texturePos = normalizedPos * _textureSize;
        float textureRadius = radius / terrainScale * _textureSize;
        Vector3Int minBound = Vector3Int.FloorToInt(texturePos - new Vector3(textureRadius, textureRadius, textureRadius));
        Vector3Int maxBound = Vector3Int.CeilToInt(texturePos + new Vector3(textureRadius, textureRadius, textureRadius));
        minBound = Vector3Int.Max(Vector3Int.zero, minBound);
        maxBound = Vector3Int.Min(new Vector3Int(_textureSize - 1, _textureSize - 1, _textureSize - 1), maxBound);
        int sizeX = maxBound.x - minBound.x + 1;
        int sizeY = maxBound.y - minBound.y + 1;
        int sizeZ = maxBound.z - minBound.z + 1;

        Debug.Log($"Explosion at {worldPosition} with radius {radius} affecting {affectedChunks.Count} chunks. Region size: {sizeX}x{sizeY}x{sizeZ}");

        Vector3Int minCell = WorldToSpatialCell(worldPosition - new Vector3(radius, radius, radius));
        Vector3Int maxCell = WorldToSpatialCell(worldPosition + new Vector3(radius, radius, radius));

        Debug.Log($"Explosion at {worldPosition}, radius {radius}:");
        Debug.Log($"- Found {affectedChunks.Count} affected chunks out of {_chunks.Length} total");
        Debug.Log($"- Last chunks checked: {_lastTotalChunksChecked}");
        Debug.Log($"- Query time: {timer.ElapsedMilliseconds}ms");
        Debug.Log($"- Spatial region: From {minCell} to {maxCell}");
        Debug.Log($"Spatial cell size: {_spatialCellSize}, Chunk size: {terrainScale / worldSize}");

        ApplyExplosionToDensity(worldPosition, radius, strength);
        StartCoroutine(RegenerateChunksProgressively(affectedChunks, worldPosition));
    }

    #endregion

    #region Helper Methods

    private void ReleaseResources()
    {
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

        if (_chunks != null)
        {
            foreach (Chunk chunk in _chunks)
            {
                if (chunk != null)
                {
                    if (chunk.gameObject != null)
                    {
                        DestroyImmediate(chunk.gameObject);
                    }
                    chunk.Release();
                }
            }
            _chunks = null;
        }

        if (_chunkLookup != null)
        {
            _chunkLookup.Clear();
            _chunkLookup = null;
        }

        if (_spatialHash != null)
        {
            _spatialHash.Clear();
            _spatialHash = null;
        }

        _vertexDataArray = null;
        _initialized = false;
    }

    private List<Chunk> FindChunksInRadius(Vector3 center, float radius)
    {
        List<Chunk> affectedChunks = new List<Chunk>();
        float sqrRadius = radius * radius;

        int chunkCheckCount = 0;
        _lastCheckedCells = new HashSet<Vector3Int>();

        Vector3Int minCellCoord = WorldToSpatialCell(center - new Vector3(radius, radius, radius));
        Vector3Int maxCellCoord = WorldToSpatialCell(center + new Vector3(radius, radius, radius));

        HashSet<Chunk> checkedChunks = new HashSet<Chunk>();

        for (int x = minCellCoord.x; x <= maxCellCoord.x; x++)
        {
            for (int y = minCellCoord.y; y <= maxCellCoord.y; y++)
            {
                for (int z = minCellCoord.z; z <= maxCellCoord.z; z++)
                {
                    Vector3Int cellCoord = new Vector3Int(x, y, z);
                    _lastCheckedCells.Add(cellCoord);

                    if (_spatialHash.TryGetValue(cellCoord, out var chunksInCell))
                    {
                        foreach (var chunk in chunksInCell)
                        {
                            if (checkedChunks.Contains(chunk))
                                continue;

                            checkedChunks.Add(chunk);
                            chunkCheckCount++;

                            Vector3 closestPoint = GetClosestPointOnChunk(chunk, center);

                            if ((center - closestPoint).sqrMagnitude <= sqrRadius)
                            {
                                affectedChunks.Add(chunk);
                            }
                        }
                    }
                }
            }
        }

        _lastTotalChunksChecked = chunkCheckCount;

        Debug.Log($"Spatial query stats: Checked {chunkCheckCount} chunks across {_lastCheckedCells.Count} cells " +
                 $"out of {_chunks.Length} total chunks");

        return affectedChunks;
    }

    private Vector3 GetClosestPointOnChunk(Chunk chunk, Vector3 targetPos)
    {
        Vector3 halfSize = Vector3.one * chunk.Size * 0.5f;
        Vector3 min = chunk.Centre - halfSize;
        Vector3 max = chunk.Centre + halfSize;

        return new Vector3(
            Mathf.Clamp(targetPos.x, min.x, max.x),
            Mathf.Clamp(targetPos.y, min.y, max.y),
            Mathf.Clamp(targetPos.z, min.z, max.z)
        );
    }

    private void ApplyExplosionToDensity(Vector3 worldPosition, float radius, float strength)
    {
        Vector3 normalizedPos = (worldPosition / terrainScale) + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 texturePos = normalizedPos * _textureSize;

        float textureRadius = radius / terrainScale * _textureSize;

        Vector3Int minBound = Vector3Int.FloorToInt(texturePos - new Vector3(textureRadius, textureRadius, textureRadius));
        Vector3Int maxBound = Vector3Int.CeilToInt(texturePos + new Vector3(textureRadius, textureRadius, textureRadius));

        minBound = Vector3Int.Max(Vector3Int.zero, minBound);
        maxBound = Vector3Int.Min(new Vector3Int(_textureSize - 1, _textureSize - 1, _textureSize - 1), maxBound);

        int sizeX = maxBound.x - minBound.x + 1;
        int sizeY = maxBound.y - minBound.y + 1;
        int sizeZ = maxBound.z - minBound.z + 1;

        RenderTexture targetTexture = useBlur ? _blurredDensityTexture : _densityTexture;

        explosionCompute.SetInts("regionMin", minBound.x, minBound.y, minBound.z);
        explosionCompute.SetInts("regionMax", maxBound.x, maxBound.y, maxBound.z);
        explosionCompute.SetTexture(0, "DensityTexture", targetTexture);
        explosionCompute.SetVector("explosionCenter", texturePos);
        explosionCompute.SetFloat("radius", textureRadius);
        explosionCompute.SetFloat("strength", strength);
        explosionCompute.SetInt("textureSize", _textureSize);
        explosionCompute.SetFloat("boundsSize", terrainScale);
        explosionCompute.SetFloat("isoLevel", isoLevel);

        ComputeHelper.Dispatch(explosionCompute, sizeX, sizeY, sizeZ);
    }

    private System.Collections.IEnumerator RegenerateChunksProgressively(List<Chunk> chunks, Vector3 explosionCenter)
    {
        chunks.Sort((a, b) =>
            Vector3.Distance(a.Centre, explosionCenter).CompareTo(
            Vector3.Distance(b.Centre, explosionCenter)));

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

            yield return null;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !debugVisualization)
            return;

        if (_chunks != null)
        {
            Gizmos.color = Color.blue;
            foreach (var chunk in _chunks)
            {
                if (chunk != null)
                    chunk.DrawBoundsGizmo(Color.blue);
            }
        }

        if (_lastCheckedCells != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 1f);

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

    #endregion
}