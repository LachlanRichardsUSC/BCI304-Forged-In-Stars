using UnityEngine;

/// <summary>
/// Visualizes procedural terrain chunks and density points using Unity's Gizmos and GUI features.
/// </summary>
/// <remarks>
/// This class is responsible for drawing chunk boundaries, visualizing density points, and displaying chunk information
/// when interacted with via the main camera.
/// </remarks>
public class TerrainDebugVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator terrainGenerator;

    [Header("Visualization Settings")]
    [SerializeField] private bool showChunkBounds = true;
    [SerializeField] private bool showDensityPoints = true;
    [SerializeField] private float densityPointSize = 1f;

    [Header("Colors")]
    [SerializeField] private Color chunkBoundsColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color densityPositiveColor = Color.blue;
    [SerializeField] private Color densityNegativeColor = Color.red;

    /// <summary>
    /// Draws the chunk boundaries and density points using Gizmos.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (terrainGenerator == null) return;

        if (showChunkBounds && terrainGenerator.Chunks != null)
        {
            DrawChunkBounds();
        }

        if (showDensityPoints && terrainGenerator.DensityTexture != null)
        {
            DrawDensityPoints();
        }
    }

    /// <summary>
    /// Draws the wireframe boundaries for each chunk and connects adjacent chunks.
    /// </summary>
    private void DrawChunkBounds()
    {
        foreach (var chunk in terrainGenerator.Chunks)
        {
            if (chunk == null) continue;

            Gizmos.color = chunkBoundsColor;
            Gizmos.DrawWireCube(chunk.Centre, Vector3.one * chunk.Size);
            DrawChunkConnections(chunk);
        }
    }

    /// <summary>
    /// Draws lines connecting adjacent chunks for visual clarity.
    /// </summary>
    private void DrawChunkConnections(Chunk chunk)
    {
        Gizmos.color = new Color(chunkBoundsColor.r, chunkBoundsColor.g, chunkBoundsColor.b, 0.2f);
        foreach (var otherChunk in terrainGenerator.Chunks)
        {
            if (otherChunk == null || otherChunk == chunk) continue;

            Vector3Int distance = new Vector3Int(
                Mathf.Abs(chunk.Id.x - otherChunk.Id.x),
                Mathf.Abs(chunk.Id.y - otherChunk.Id.y),
                Mathf.Abs(chunk.Id.z - otherChunk.Id.z)
            );

            if (distance.x + distance.y + distance.z == 1)
            {
                Gizmos.DrawLine(chunk.Centre, otherChunk.Centre);
            }
        }
    }

    /// <summary>
    /// Draws density points within the procedural terrain for visualization purposes.
    /// </summary>
    private void DrawDensityPoints()
    {
        int step = terrainGenerator.NumPointsPerAxis / 4;
        int size = terrainGenerator.DensityTexture.width;

        for (int x = 0; x < size; x += step)
        {
            for (int y = 0; y < size; y += step)
            {
                for (int z = 0; z < size; z += step)
                {
                    Vector3 worldPos = new Vector3(
                        (x / (float)(size - 1) - 0.5f) * terrainGenerator.BoundsSize,
                        (y / (float)(size - 1) - 0.5f) * terrainGenerator.BoundsSize,
                        (z / (float)(size - 1) - 0.5f) * terrainGenerator.BoundsSize
                    );

                    if (Vector3.Distance(worldPos, Vector3.zero) > terrainGenerator.BoundsSize * 0.5f)
                        continue;

                    Gizmos.color = Vector3.Distance(worldPos, Vector3.zero) < terrainGenerator.BoundsSize * 0.4f
                        ? densityPositiveColor
                        : densityNegativeColor;
                    Gizmos.DrawSphere(worldPos, densityPointSize);
                }
            }
        }
    }

    /// <summary>
    /// Displays chunk information when the main camera is pointing at a chunk.
    /// </summary>
    private void OnGUI()
    {
        if (!showChunkBounds) return;

        if (Camera.main == null)
        {
            Debug.LogWarning("No Main Camera found. Ensure a camera is tagged as 'MainCamera'.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            foreach (var chunk in terrainGenerator.Chunks)
            {
                if (chunk == null) continue;

                Vector3 localPoint = hit.point - chunk.Centre;
                if (Mathf.Abs(localPoint.x) <= chunk.Size / 2 &&
                    Mathf.Abs(localPoint.y) <= chunk.Size / 2 &&
                    Mathf.Abs(localPoint.z) <= chunk.Size / 2)
                {
                    Vector2 screenPoint = Event.current.mousePosition;
                    Rect rect = new Rect(screenPoint.x, screenPoint.y, 200, 100);
                    GUI.Box(rect, $"Chunk ID: {chunk.Id}\n" +
                                  $"Position: {chunk.Centre}\n" +
                                  $"Size: {chunk.Size}");
                    break;
                }
            }
        }
    }
}
