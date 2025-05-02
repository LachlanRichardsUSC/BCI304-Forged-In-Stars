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
    [SerializeField] private int terrainSeed = 0; // Add this line
    [SerializeField] private bool randomizeSeed = false; // Add this line

    [Header("Blur Settings")]
    [SerializeField] private bool useBlur = true;
    [SerializeField] [Range(1, 5)] private int blurRadius = 1;

    [Header("References")]
    [SerializeField] private ComputeShader meshCompute;
    [SerializeField] private ComputeShader densityCompute;
    [SerializeField] private ComputeShader blurCompute;
    [SerializeField] private Material material;

    [Header("Terrain Zones")]
    [SerializeField][Range(0.0f, 1.0f)] private float flatnessThreshold = 0.4f;
    [SerializeField][Range(0.0f, 1.0f)] private float detail3DStrength = 0.2f;

    // Public properties for external access
    public int NumPointsPerAxis => numPointsPerAxis;
    public float BoundsSize => boundsSize;
    public RenderTexture DensityTexture => _densityTexture;
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
        densityCompute.SetFloat("planetSize", boundsSize);
        densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
        densityCompute.SetFloat("noiseScale", noiseScale);
        densityCompute.SetInt("borderWidth", borderWidth);
        densityCompute.SetFloat("terrainSeed", terrainSeed);

        densityCompute.SetFloat("flatnessThreshold", flatnessThreshold);
        densityCompute.SetFloat("detail3DStrength", detail3DStrength);

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
        meshCompute.SetFloat("planetSize", boundsSize);

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
        // For flat terrain, we want more chunks horizontally and fewer vertically
        int chunksY = Mathf.Max(1, numChunks / 2); // Fewer vertical chunks
        int chunksXZ = numChunks;                  // Same horizontal chunks

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

                    // Adjusted center calculation - keep terrain in view
                    Vector3 centre = new Vector3(
                        (-(chunksXZ - 1f) / 2 + x) * chunkSize,
                        // Place chunks with y=0 at or slightly below center
                        -chunkSize + y * chunkSize,
                        (-(chunksXZ - 1f) / 2 + z) * chunkSize
                    );

                    GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})")
                    {
                        transform = { parent = transform },
                        layer = gameObject.layer
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
    /// Releases allocated buffers and textures when the object is destroyed.
    /// </summary>
    /// 

    void OnDestroy()
    {
        ComputeHelper.Release(_triangleBuffer, _triCountBuffer);
        _blurredDensityTexture?.Release();

        foreach (Chunk chunk in _chunks)
        {
            chunk.Release();
        }
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

}