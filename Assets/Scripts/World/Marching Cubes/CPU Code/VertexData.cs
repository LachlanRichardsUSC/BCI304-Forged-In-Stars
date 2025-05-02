using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Represents data for a single vertex in the procedural planet generation system.
/// </summary>
/// <remarks>
/// This struct holds information about a vertex's position, normal, and an ID used for vertex welding between chunks.
/// Vertex welding ensures that shared vertices between chunks are properly joined, preventing visible seams in the mesh.
/// </remarks>
public struct VertexData
{
    /// <summary>
    /// The position of the vertex in world space.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// The surface normal of the vertex, used for lighting calculations.
    /// </summary>
    public Vector3 normal;

    /// <summary>
    /// An identifier used for vertex welding between adjacent chunks.
    /// </summary>
    public int2 id;
}