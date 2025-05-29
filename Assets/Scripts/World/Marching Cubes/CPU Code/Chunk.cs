using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Linq;

/// <summary>
/// Represents a single chunk of terrain in the procedural generation system.
/// </summary>
/// <remarks>
/// The Chunk class manages the mesh, rendering, and collider data for a terrain chunk.
/// It supports vertex welding, flat shading, and material assignment.
/// </remarks>
public class Chunk
{
    // Public properties
    /// <summary>
    /// Gets the center position of the chunk in world space.
    /// </summary>
    public Vector3 Centre => _centre;

    /// <summary>
    /// Gets the size of the chunk.
    /// </summary>
    public float Size => _size;

    /// <summary>
    /// Gets the identifier of the chunk, representing its coordinate within the terrain grid.
    /// </summary>
    public Vector3Int Id => _id;

    // Private fields
    private Vector3 _centre;
    private float _size;
    private Vector3Int _id;
    private Mesh _mesh;
    public MeshRenderer _renderer;
    private MeshCollider _collider;
    private Dictionary<int2, int> _vertexIndexMap;
    private List<Vector3> _processedVertices;
    private List<Vector3> _processedNormals;
    private List<int> _processedTriangles;
    public GameObject gameObject { get; private set; }

    /// <summary>
    /// Initializes a new instance of the Chunk class with the specified coordinates, center, size, and mesh holder.
    /// </summary>
    /// <param name="coord">The coordinate of the chunk within the terrain grid.</param>
    /// <param name="centre">The center position of the chunk in world space.</param>
    /// <param name="size">The size of the chunk.</param>
    /// <param name="meshHolder">The GameObject that holds the chunk's mesh and rendering components.</param>
    public Chunk(Vector3Int coord, Vector3 centre, float size, GameObject meshHolder)
    {
        _id = coord;
        _centre = centre;
        _size = size;
        gameObject = meshHolder; // Store reference

        _mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

        var filter = meshHolder.AddComponent<MeshFilter>();
        _renderer = meshHolder.AddComponent<MeshRenderer>();
        filter.mesh = _mesh;
        _collider = meshHolder.AddComponent<MeshCollider>();

        _vertexIndexMap = new Dictionary<int2, int>();
        _processedVertices = new List<Vector3>();
        _processedNormals = new List<Vector3>();
        _processedTriangles = new List<int>();
    }

    /// <summary>
    /// Creates and updates the chunk's mesh using the provided vertex data.
    /// </summary>
    /// <param name="vertexData">An array of VertexData objects representing the chunk's vertices.</param>
    /// <param name="numVertices">The number of vertices to process.</param>
    /// <param name="useFlatShading">Indicates whether flat shading should be used.</param>
    public void CreateMesh(VertexData[] vertexData, int numVertices, bool useFlatShading)
    {
        // Clear previous mesh data
        _vertexIndexMap.Clear();
        _processedVertices.Clear();
        _processedNormals.Clear();
        _processedTriangles.Clear();

        int triangleIndex = 0;

        // Process each vertex
        for (int i = 0; i < numVertices; i++)
        {
            VertexData data = vertexData[i];

            // Check if we can reuse an existing vertex (unless using flat shading)
            if (!useFlatShading && _vertexIndexMap.TryGetValue(data.id, out int sharedVertexIndex))
            {
                _processedTriangles.Add(sharedVertexIndex);
            }
            else
            {
                // Add new vertex
                if (!useFlatShading)
                {
                    _vertexIndexMap.Add(data.id, triangleIndex);
                }
                _processedVertices.Add(data.position);
                _processedNormals.Add(data.normal);
                _processedTriangles.Add(triangleIndex);
                triangleIndex++;
            }
        }

        // Update mesh
        _mesh.Clear();
        _mesh.SetVertices(_processedVertices);
        _mesh.SetTriangles(_processedTriangles, 0, true);

        if (useFlatShading)
        {
            _mesh.RecalculateNormals();
        }
        else
        {
            _mesh.SetNormals(_processedNormals);
        }

        // Only apply the collider AFTER updating the mesh
        // AND only if there are vertices
        if (_processedVertices.Count > 0)
        {
            _collider.sharedMesh = _mesh;
            _renderer.enabled = true; // Explicitly enable
        }
        else
        {
            _collider.sharedMesh = null;

            // Keep renderer enabled unless we're confident there's no geometry
            // When higher resolution is used, disabling can be problematic
            if (numVertices == 0 && _renderer != null)
            {
                _renderer.enabled = false;
            }
        }
    }

    /// <summary>
    /// Sets the material for the chunk's renderer.
    /// </summary>
    /// <param name="material">The material to apply to the chunk. If null, the method does nothing.</param>
    public void SetMaterial(Material material)
    {
        if (material == null) return;
        _renderer.material = material;
    }

    /// <summary>
    /// Sets the mesh's layer to Ground for interaction with the player character.
    /// </summary>
    /// <param name="layerIndex"></param>
    public void SetLayer(int layerIndex)
    {
        if (gameObject != null)
        {
            gameObject.layer = layerIndex;
        }
    }

    /// <summary>
    /// Releases the chunk's mesh to free up memory.
    /// </summary>
    public void Release()
    {
        // Clear collections to help GC
        if (_vertexIndexMap != null)
        {
            _vertexIndexMap.Clear();
            _vertexIndexMap = null;
        }

        if (_processedVertices != null)
        {
            _processedVertices.Clear();
            _processedVertices = null;
        }

        if (_processedNormals != null)
        {
            _processedNormals.Clear();
            _processedNormals = null;
        }

        if (_processedTriangles != null)
        {
            _processedTriangles.Clear();
            _processedTriangles = null;
        }

        // Clean up mesh
        if (_mesh != null)
        {
            _mesh.Clear();
            Object.Destroy(_mesh);
            _mesh = null;
        }

        // Clear renderer references
        _renderer = null;
        _collider = null;
    }

    /// <summary>
    /// Draws the wireframe bounds of the chunk using Gizmos.
    /// </summary>
    /// <param name="col">The color to use for drawing the bounds.</param>
    public void DrawBoundsGizmo(Color col)
    {
        Gizmos.color = col;
        Gizmos.DrawWireCube(_centre, Vector3.one * _size);
    }


}