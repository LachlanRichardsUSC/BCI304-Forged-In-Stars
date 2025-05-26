using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU-based foliage placement system for marching cubes terrain.
/// Uses compute shaders for efficient point generation directly from density field.
/// Based on techniques from GPU Pro 7 Chapter 1: "Bandwidth-Efficient Rendering"
/// </summary>
public class GPUFoliagePlacer : MonoBehaviour
{
    #region Structures

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FoliagePoint
    {
        public Vector3 position;
        public Vector3 normal;
        public float scale;
        public float rotation;

        public static int GetStride() => sizeof(float) * 8;
    }

    #endregion

    #region Serialized Fields

    [Header("References")]
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private ComputeShader foliagePlacementCompute;

    [Header("Placement Grid")]
    [SerializeField, Range(10, 500)]
    [Tooltip("Number of sample points along X and Z axes")]
    private int gridResolution = 100;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Overall density of placement (0 = no foliage, 1 = maximum)")]
    private float placementDensity = 0.5f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Randomness in placement positions")]
    private float jitterAmount = 0.8f;

    [Header("Placement Constraints")]
    [SerializeField, Range(0f, 90f)]
    [Tooltip("Minimum slope angle in degrees")]
    private float minSlopeAngle = 0f;

    [SerializeField, Range(0f, 90f)]
    [Tooltip("Maximum slope angle in degrees")]
    private float maxSlopeAngle = 45f;

    [SerializeField]
    [Tooltip("Minimum world height for placement")]
    private float minHeight = -1000f;

    [SerializeField]
    [Tooltip("Maximum world height for placement")]
    private float maxHeight = 1000f;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugSpheres = true;
    [SerializeField] private Mesh debugSphereMesh;
    [SerializeField] private Material debugMaterial;
    [SerializeField] private float debugSphereScale = 1f;
    [SerializeField] private Color debugColor = Color.green;

    [Header("Performance")]
    [SerializeField, Range(1, 1000000)]
    [Tooltip("Maximum number of foliage points")]
    private int maxFoliagePoints = 100000;

    #endregion

    #region Private Fields

    // Compute buffers
    private ComputeBuffer foliagePointsBuffer;
    private ComputeBuffer argsBuffer;

    // Kernel indices
    private int generateKernel;

    // Current state
    private int currentPointCount = 0;
    private uint seed;
    private Bounds terrainBounds;
    private bool isInitialized = false;
    private bool waitingForTerrain = false;

    // For debug visualization
    private MaterialPropertyBlock propertyBlock;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait for terrain generator to be ready
        if (terrainGenerator == null)
        {
            terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
        }

        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            enabled = false;
            yield break;
        }

        // Wait for terrain to generate
        waitingForTerrain = true;
        while (terrainGenerator.DensityTexture == null)
        {
            yield return null;
        }
        waitingForTerrain = false;

        // Now initialize and generate foliage
        if (Initialize())
        {
            GenerateFoliagePoints();
        }
        else
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (isInitialized && showDebugSpheres && currentPointCount > 0)
        {
            DrawDebugVisualization();
        }
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void OnValidate()
    {
        // Ensure constraints are valid
        maxSlopeAngle = Mathf.Max(minSlopeAngle, maxSlopeAngle);
        maxHeight = Mathf.Max(minHeight, maxHeight);

        // Only regenerate if we're properly initialized and playing
        if (Application.isPlaying && isInitialized && !waitingForTerrain)
        {
            GenerateFoliagePoints();
        }
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        // Validate references
        if (foliagePlacementCompute == null)
        {
            Debug.LogError("Foliage placement compute shader not assigned!");
            return false;
        }

        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found!");
            return false;
        }

        // Setup debug visualization
        if (debugSphereMesh == null)
        {
            debugSphereMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(GameObject.Find("Sphere"));
        }

        if (debugMaterial == null)
        {
            // Try to find the shader
            Shader shader = Shader.Find("Debug/SimpleFoliagePoints");
            if (shader != null)
            {
                debugMaterial = new Material(shader);
            }
            else
            {
                // Create a simple unlit material as fallback
                shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    debugMaterial = new Material(shader);
                    debugMaterial.color = debugColor;
                }
                else
                {
                    Debug.LogWarning("No suitable debug shader found. Debug visualization will not work.");
                }
            }
        }

        propertyBlock = new MaterialPropertyBlock();

        // Get kernel
        generateKernel = foliagePlacementCompute.FindKernel("GenerateFoliagePoints");

        // Initialize seed
        seed = (uint)Random.Range(0, int.MaxValue);

        // Get terrain bounds
        float size = terrainGenerator.BoundsSize;
        terrainBounds = new Bounds(Vector3.zero, Vector3.one * size);

        // Create buffers
        CreateBuffers();

        isInitialized = true;
        return true;
    }

    private void CreateBuffers()
    {
        ReleaseBuffers();

        // Create foliage points buffer
        foliagePointsBuffer = new ComputeBuffer(maxFoliagePoints, FoliagePoint.GetStride(), ComputeBufferType.Append);

        // Create indirect args buffer for instanced rendering
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        if (debugSphereMesh != null)
        {
            args[0] = debugSphereMesh.GetIndexCount(0);
            args[1] = 0; // Instance count - will be set by CopyCount
            args[2] = debugSphereMesh.GetIndexStart(0);
            args[3] = debugSphereMesh.GetBaseVertex(0);
            args[4] = 0;
        }

        argsBuffer.SetData(args);
    }

    private void ReleaseBuffers()
    {
        foliagePointsBuffer?.Release();
        foliagePointsBuffer = null;

        argsBuffer?.Release();
        argsBuffer = null;
    }

    #endregion

    #region Foliage Generation

    public void GenerateFoliagePoints()
    {
        if (!Application.isPlaying || !isInitialized)
            return;

        if (foliagePlacementCompute == null || terrainGenerator == null)
        {
            Debug.LogError("Missing required references for foliage generation!");
            return;
        }

        RenderTexture densityTexture = terrainGenerator.BlurredDensityTexture ?? terrainGenerator.DensityTexture;
        if (densityTexture == null)
        {
            Debug.LogError("No density texture available from terrain generator!");
            return;
        }

        // Clear existing points
        foliagePointsBuffer.SetCounterValue(0);

        // Calculate adaptive grid resolution based on terrain size and desired density
        float terrainArea = terrainBounds.size.x * terrainBounds.size.z;
        float targetPointDensity = 0.1f; // points per square unit
        int estimatedPoints = Mathf.RoundToInt(terrainArea * targetPointDensity * placementDensity);

        // Clamp grid resolution to prevent artifacts
        int effectiveGridRes = Mathf.Clamp(gridResolution, 10, 200);

        // Set compute shader parameters
        foliagePlacementCompute.SetTexture(generateKernel, "DensityTexture", densityTexture);
        foliagePlacementCompute.SetBuffer(generateKernel, "FoliagePoints", foliagePointsBuffer);

        // Set grid dimensions
        foliagePlacementCompute.SetInts("gridDimensions", effectiveGridRes, 1, effectiveGridRes);

        // Set bounds
        Vector3 boundsMin = terrainBounds.min;
        foliagePlacementCompute.SetVector("boundsMin", boundsMin);
        foliagePlacementCompute.SetVector("boundsSize", terrainBounds.size);

        // Set placement parameters
        foliagePlacementCompute.SetFloat("placementDensity", placementDensity);
        foliagePlacementCompute.SetFloat("minSlopeAngle", minSlopeAngle);
        foliagePlacementCompute.SetFloat("maxSlopeAngle", maxSlopeAngle);
        foliagePlacementCompute.SetFloat("minHeight", minHeight);
        foliagePlacementCompute.SetFloat("maxHeight", maxHeight);
        foliagePlacementCompute.SetInt("seed", (int)seed);
        foliagePlacementCompute.SetFloat("isoLevel", 0f);
        foliagePlacementCompute.SetInt("textureSize", densityTexture.width);
        foliagePlacementCompute.SetFloat("jitterAmount", jitterAmount);
        foliagePlacementCompute.SetInt("maxPoints", maxFoliagePoints); // Add this

        // Dispatch with appropriate thread groups
        int threadGroupsX = Mathf.CeilToInt(effectiveGridRes / 8f);
        int threadGroupsZ = Mathf.CeilToInt(effectiveGridRes / 8f);
        foliagePlacementCompute.Dispatch(generateKernel, threadGroupsX, 1, threadGroupsZ);

        // Update instance count in args buffer
        ComputeBuffer.CopyCount(foliagePointsBuffer, argsBuffer, sizeof(uint));

        // Get point count for debug
        UpdatePointCount();
    }

    private void UpdatePointCount()
    {
        if (argsBuffer == null)
            return;

        uint[] args = new uint[5];
        argsBuffer.GetData(args);
        currentPointCount = (int)args[1];

        Debug.Log($"Generated {currentPointCount} foliage points");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the compute buffer containing foliage points for instancing
    /// </summary>
    public ComputeBuffer GetFoliagePointsBuffer() => foliagePointsBuffer;

    /// <summary>
    /// Gets the current number of foliage points
    /// </summary>
    public int GetPointCount() => currentPointCount;

    /// <summary>
    /// Regenerates foliage with a new random seed
    /// </summary>
    public void RegenerateWithNewSeed()
    {
        if (!isInitialized)
            return;

        seed = (uint)Random.Range(0, int.MaxValue);
        GenerateFoliagePoints();
    }

    /// <summary>
    /// Called by TerrainGenerator when terrain regeneration is complete
    /// </summary>
    public void OnTerrainRegenerated()
    {
        if (isInitialized)
        {
            GenerateFoliagePoints();
        }
    }

    #endregion

    #region Debug Visualization

    private void DrawDebugVisualization()
    {
        if (debugSphereMesh == null || debugMaterial == null || foliagePointsBuffer == null || currentPointCount == 0)
            return;

        // Setup material properties
        propertyBlock.SetBuffer("_FoliagePoints", foliagePointsBuffer);
        propertyBlock.SetFloat("_Scale", debugSphereScale);
        propertyBlock.SetColor("_Color", debugColor);

        // Draw instanced meshes
        Graphics.DrawMeshInstancedIndirect(
            debugSphereMesh,
            0,
            debugMaterial,
            terrainBounds,
            argsBuffer,
            0,
            propertyBlock,
            ShadowCastingMode.Off,
            false
        );
    }

    #endregion
}