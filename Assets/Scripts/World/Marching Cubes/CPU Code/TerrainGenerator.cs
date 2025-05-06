using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the generation of procedural terrain using marching cubes and compute shaders.
/// </summary>
/// <remarks>
/// This class initializes textures, dispatches compute shaders for density and blur generation,
/// and creates terrain chunks based on generated vertex data.
/// </remarks>
public class TerrainGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private int numChunks = 5;
    [SerializeField] private int numPointsPerAxis = 32;
    [SerializeField] private float boundsSize = 600;
    [SerializeField] private int borderWidth = 1;
    [SerializeField] private float isoLevel = 0.0f;
    [SerializeField] private bool useFlatShading = false;

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

    // Public properties for external access
    public int NumPointsPerAxis => numPointsPerAxis;
    public float BoundsSize => boundsSize;
    public RenderTexture DensityTexture => _densityTexture;
    public RenderTexture BlurredDensityTexture => _blurredDensityTexture;
    public Chunk[] Chunks => _chunks;

    private ComputeBuffer _triangleBuffer;
    private ComputeBuffer _triCountBuffer;
    private RenderTexture _densityTexture;
    private RenderTexture _blurredDensityTexture;
    private Chunk[] _chunks;
    private VertexData[] _vertexDataArray;
    private int _totalVerts;
    private System.Diagnostics.Stopwatch _timerGeneration;

    /// <summary>
    /// Initializes textures, buffers, and chunks, then generates all chunks.
    /// </summary>
    void Start()
    {
        InitTextures();
        CreateBuffers();
        CreateChunks();

        _timerGeneration = System.Diagnostics.Stopwatch.StartNew();
        GenerateAllChunks();
        Debug.Log($"Generation Time: {_timerGeneration.ElapsedMilliseconds} ms");
        Debug.Log($"Total vertices: {_totalVerts}");
    }

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
    /// Initializes the 3D textures for density and optional blur.
    /// </summary>
    void InitTextures()
    {
        int size = numChunks * (numPointsPerAxis - 1) + 1;
        if (numChunks <= 0 || numPointsPerAxis <= 0)
            throw new System.ArgumentException("Invalid generation parameters");

        Create3DTexture(ref _densityTexture, size, "Density Texture");

        if (useBlur)
        {
            Create3DTexture(ref _blurredDensityTexture, size, "Blurred Density Texture");
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

    /// <summary>
    /// Generates all terrain chunks by computing density and creating meshes.
    /// </summary>
    void GenerateAllChunks()
    {
        _totalVerts = 0;
        ComputeDensity();

        foreach (var chunk in _chunks)
        {
            GenerateChunk(chunk);
        }
    }

    /// <summary>
    /// Computes the density values using the compute shader.
    /// </summary>
    void ComputeDensity()
    {
        int textureSize = _densityTexture.width;

        if (randomizeSeed)
        {
            terrainSeed = Random.Range(0, 10000);
            Debug.Log($"Using random terrain seed: {terrainSeed}");
        }

        // Set parameters
        densityCompute.SetInt("textureSize", textureSize);
        densityCompute.SetFloat("boundsSize", boundsSize);
        densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
        densityCompute.SetFloat("noiseScale", noiseScale);
        densityCompute.SetInt("borderWidth", borderWidth);
        densityCompute.SetFloat("terrainSeed", terrainSeed);

        densityCompute.SetFloat("flatnessThreshold", flatnessThreshold);
        densityCompute.SetFloat("detail3DStrength", detail3DStrength);

        // Add border falloff parameters
        densityCompute.SetFloat("borderFalloffStart", borderFalloffStart);
        densityCompute.SetFloat("borderFalloffSteepness", borderFalloffSteepness);
        densityCompute.SetInt("enableBorderFalloff", enableBorderFalloff ? 1 : 0);

        // Set texture using index 0 directly
        densityCompute.SetTexture(0, "DensityTexture", _densityTexture);

        // Dispatch with default kernel index (0)
        ComputeHelper.Dispatch(densityCompute, textureSize, textureSize, textureSize);

        if (useBlur)
        {
            // No need to find the kernel index here either
            blurCompute.SetInt("textureSize", textureSize);
            blurCompute.SetInt("blurRadius", blurRadius);
            blurCompute.SetTexture(0, "Source", _densityTexture);
            blurCompute.SetTexture(0, "Result", _blurredDensityTexture);
            ComputeHelper.Dispatch(blurCompute, textureSize, textureSize, textureSize);
        }
    }

    /// <summary>
    /// Generates a mesh for a specific chunk based on computed density data.
    /// </summary>
    /// <param name="chunk">The chunk for which the mesh is generated.</param>
    void GenerateChunk(Chunk chunk)
    {
        int numVoxelsPerAxis = numPointsPerAxis - 1;

        meshCompute.SetInt("textureSize", _densityTexture.width);
        meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
        meshCompute.SetFloat("isoLevel", isoLevel);
        meshCompute.SetFloat("boundsSize", boundsSize);

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

        _triangleBuffer.GetData(_vertexDataArray, 0, 0, numVertices);
        chunk.CreateMesh(_vertexDataArray, numVertices, useFlatShading);

        _totalVerts += numVertices;
    }

    /// <summary>
    /// Creates the necessary compute buffers for vertex and triangle data.
    /// </summary>
    void CreateBuffers()
    {
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;
        int maxVertexCount = maxTriangleCount * 3;

        _triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
        _vertexDataArray = new VertexData[maxVertexCount];
    }

    /// <summary>
    /// Creates the terrain chunks with appropriate sizes and positions.
    /// </summary>
    void CreateChunks()
    {

        int chunksY = 10;
        // int chunksY = numChunks;
        int chunksXZ = numChunks;
        int groundLayer = LayerMask.NameToLayer(groundLayerName);

        _chunks = new Chunk[chunksXZ * chunksY * chunksXZ];
        float chunkSize = boundsSize / numChunks;
        int i = 0;


        for (int y = 0; y < chunksY; y++)
        {
            for (int x = 0; x < chunksXZ; x++)
            {
                for (int z = 0; z < chunksXZ; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    Vector3 centre = new Vector3(
                        (-(chunksXZ - 1f) / 2 + x) * chunkSize,
                        (-(chunksY - 1f) / 2 + y) * chunkSize,  // Center chunks around Y=0
                        (-(chunksXZ - 1f) / 2 + z) * chunkSize
                    );

                    GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})")
                    {
                        transform = { parent = transform },
                        layer = groundLayer
                    };

                    Chunk chunk = new Chunk(coord, centre, chunkSize, meshHolder);
                    chunk.SetMaterial(material);

                    _chunks[i] = chunk;
                    i++;
                }
            }
        }
    }

    /// <summary>
    /// Creates a 3D texture with the specified settings.
    /// </summary>
    /// <param name="texture">The reference to the RenderTexture to be created.</param>
    /// <param name="size">The size of each dimension of the 3D texture.</param>
    /// <param name="densityTexture">The name of the texture for identification.</param>
    void Create3DTexture(ref RenderTexture texture, int size, string densityTexture)
    {
        var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        if (texture == null || !texture.IsCreated() || texture.width != size ||
            texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
        {
            texture?.Release();
            texture = new RenderTexture(size, size, 0)
            {
                graphicsFormat = format,
                volumeDepth = size,
                enableRandomWrite = true,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = densityTexture
            };
            texture.Create();
        }
    }

    /// <summary>
    /// Sets the material for all chunks in the terrain.
    /// </summary>
    /// <param name="newMaterial">The material to apply to all chunks.</param>
    public void SetMaterial(Material newMaterial)
    {
        if (newMaterial == null) return;

        // Store the material reference
        material = newMaterial;

        // Apply to existing chunks if they exist
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
    /// <remarks>
    /// Useful for updating material properties during runtime.
    /// </remarks>
    public void UpdateChunkMaterials()
    {
        if (material != null && _chunks != null)
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
    /// Releases allocated buffers and textures when the object is destroyed.
    /// </summary>
    /// 

    void OnDestroy()
    {
        // Release compute buffers
        ComputeHelper.Release(_triangleBuffer, _triCountBuffer);
        _triangleBuffer = null;
        _triCountBuffer = null;

        // Release render textures
        if (_densityTexture != null)
        {
            _densityTexture.Release();
            _densityTexture = null;
        }

        if (_blurredDensityTexture != null)
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
                    chunk.Release();
                }
            }
            _chunks = null;
        }

        // Clear large arrays
        _vertexDataArray = null;
    }

    public void RegenerateTerrainRuntime()
    {
        Debug.Log("Starting terrain regeneration...");

        // Clean up existing terrain
        foreach (var chunk in _chunks)
        {
            if (chunk != null)
            {
                // Get the GameObject from renderer
                var meshHolder = chunk._renderer?.gameObject;
                if (meshHolder != null)
                {
                    Destroy(meshHolder);
                }
                chunk.Release();
            }
        }

        // Release compute buffers
        ComputeHelper.Release(_triangleBuffer, _triCountBuffer);

        // NEW: Explicitly release textures
        if (_densityTexture != null)
        {
            _densityTexture.Release();
            _densityTexture = null;
        }

        if (_blurredDensityTexture != null)
        {
            _blurredDensityTexture.Release();
            _blurredDensityTexture = null;
        }

        // Re-initialize
        InitTextures();
        CreateBuffers();
        CreateChunks();

        _timerGeneration = System.Diagnostics.Stopwatch.StartNew();
        GenerateAllChunks();
        Debug.Log($"Regeneration complete in {_timerGeneration.ElapsedMilliseconds} ms");
        Debug.Log($"Total vertices: {_totalVerts}");
    }

    public int GetCurrentSeed()
    {
        return terrainSeed;
    }

    public void SetSeed(int newSeed)
    {
        terrainSeed = newSeed;
        RegenerateTerrainRuntime();
    }

    /// <summary>
    /// Creates an explosion effect that modifies the terrain density at the specified position
    /// </summary>
    /// <param name="worldPosition">Position in world space where the explosion should occur</param>
    /// <param name="radius">Radius of the explosion effect</param>
    /// <param name="strength">Strength of the explosion (higher values = bigger crater)</param>
    public void CreateExplosion(Vector3 worldPosition, float radius, float strength)
    {
        // Validate compute shader
        if (explosionCompute == null)
        {
            Debug.LogError("Explosion compute shader not assigned!");
            return;
        }

        // Step 1: Find affected chunks more precisely
        List<Chunk> affectedChunks = new List<Chunk>();
        float sqrRadius = radius * radius; // No additional margin

        foreach (var chunk in _chunks)
        {
            // Calculate the closest point on the chunk to the explosion center
            Vector3 closestPoint = GetClosestPointOnChunk(chunk, worldPosition);

            // Check if this closest point is within explosion radius
            float sqrDst = (worldPosition - closestPoint).sqrMagnitude;

            if (sqrDst <= sqrRadius)
            {
                affectedChunks.Add(chunk);

                // Only include direct face-sharing neighbors if they're at the boundary
                // of the explosion radius (to handle edge cases at chunk boundaries)
                if (sqrDst > sqrRadius * 0.75f) // Only for explosions near chunk boundaries
                {
                    foreach (var otherChunk in _chunks)
                    {
                        if (IsNeighbour(chunk, otherChunk) && !affectedChunks.Contains(otherChunk))
                        {
                            // Double-check if this neighbor might be affected
                            Vector3 neighborClosest = GetClosestPointOnChunk(otherChunk, worldPosition);
                            if ((worldPosition - neighborClosest).sqrMagnitude <= sqrRadius * 1.05f)
                            {
                                affectedChunks.Add(otherChunk);
                            }
                        }
                    }
                }
            }
        }

        if (affectedChunks.Count == 0)
        {
            Debug.LogWarning($"Explosion at {worldPosition} with radius {radius} did not affect any chunks.");
            return;
        }

        // Step 2: Calculate texture space coordinates
        int textureSize = _densityTexture.width;
        Vector3 normalizedPos = (worldPosition / boundsSize) + new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 texturePos = normalizedPos * textureSize;

        // Convert world-space radius to texture-space radius
        float textureRadius = radius / boundsSize * textureSize;

        // Calculate affected region bounds
        Vector3Int minBound = Vector3Int.FloorToInt(texturePos - new Vector3(textureRadius, textureRadius, textureRadius));
        Vector3Int maxBound = Vector3Int.CeilToInt(texturePos + new Vector3(textureRadius, textureRadius, textureRadius));

        // Clamp to texture boundaries
        minBound = Vector3Int.Max(Vector3Int.zero, minBound);
        maxBound = Vector3Int.Min(new Vector3Int(textureSize - 1, textureSize - 1, textureSize - 1), maxBound);

        // Calculate region dimensions
        int sizeX = maxBound.x - minBound.x + 1;
        int sizeY = maxBound.y - minBound.y + 1;
        int sizeZ = maxBound.z - minBound.z + 1;

        // Step 3: Set computation parameters
        RenderTexture targetTexture = useBlur ? _blurredDensityTexture : _densityTexture;

        // Pass region bounds to compute shader
        explosionCompute.SetVector("regionMin", new Vector4(minBound.x, minBound.y, minBound.z, 0));
        explosionCompute.SetVector("regionMax", new Vector4(maxBound.x, maxBound.y, maxBound.z, 0));

        // Pass other essential parameters
        explosionCompute.SetTexture(0, "DensityTexture", targetTexture);
        explosionCompute.SetVector("explosionCenter", texturePos);
        explosionCompute.SetFloat("radius", radius);
        explosionCompute.SetFloat("strength", strength);
        explosionCompute.SetInt("textureSize", textureSize);
        explosionCompute.SetFloat("boundsSize", boundsSize);
        explosionCompute.SetFloat("isoLevel", isoLevel);

        // Step 4: Process only the affected region
        ComputeHelper.Dispatch(explosionCompute, sizeX, sizeY, sizeZ);

        // Step 5: Regenerate only affected chunks
        StartCoroutine(RegenerateOnlyAffectedChunks(affectedChunks, worldPosition));

        // Log success information
        Debug.Log($"Explosion at {worldPosition} with radius {radius} affecting {affectedChunks.Count} " +
                  $"chunks. Region size: {sizeX}x{sizeY}x{sizeZ}");
    }

    /// <summary>
    /// Gets the closest point on a chunk to a target position
    /// </summary>
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

    private bool IsNeighbour(Chunk a, Chunk b)
    {
        // Skip comparing with self
        if (a == b) return false;

        // Calculate Manhattan distance (sum of differences in each axis)
        int manhattanDist =
            Mathf.Abs(a.Id.x - b.Id.x) +
            Mathf.Abs(a.Id.y - b.Id.y) +
            Mathf.Abs(a.Id.z - b.Id.z);

        // Only count chunks that share a face (Manhattan distance = 1)
        // This ignores diagonal neighbors
        return manhattanDist == 1;
    }

    private System.Collections.IEnumerator RegenerateOnlyAffectedChunks(List<Chunk> chunks, Vector3 explosionCenter)
    {
        // Sort chunks by distance to explosion center
        chunks.Sort((a, b) =>
            Vector3.Distance(a.Centre, explosionCenter).CompareTo(
            Vector3.Distance(b.Centre, explosionCenter)));

        // Process 2 chunks per frame, starting with the closest ones
        const int chunksPerFrame = 2;

        for (int i = 0; i < chunks.Count; i += chunksPerFrame)
        {
            int count = Mathf.Min(chunksPerFrame, chunks.Count - i);

            for (int j = 0; j < count; j++)
            {
                GenerateChunk(chunks[i + j]);
            }

            yield return null;
        }
    }

}