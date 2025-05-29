using UnityEngine;

/// <summary>
/// Simple visualization of terrain chunks with ID labels and center point.
/// </summary>
public class TerrainDebugVisualizer : MonoBehaviour
{
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private Color chunkColor = Color.green;
    [SerializeField] private Color centerPointColor = Color.red;
    [SerializeField] private float centerPointSize = 5f;

    private void Awake()
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = Object.FindFirstObjectByType<TerrainGenerator>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (terrainGenerator == null || terrainGenerator.Chunks == null)
            return;

        // Draw each chunk
        Vector3 meshCenterSum = Vector3.zero;
        int chunkCount = 0;

        foreach (Chunk chunk in terrainGenerator.Chunks)
        {
            if (chunk == null || chunk.gameObject == null)
                continue;

            // Draw chunk bounds
            Gizmos.color = chunkColor;
            Gizmos.DrawWireCube(chunk.Centre, Vector3.one * chunk.Size);

            // Draw chunk ID
            DrawLabel(chunk.Centre, $"({chunk.Id.x}, {chunk.Id.y}, {chunk.Id.z})");

            meshCenterSum += chunk.Centre;
            chunkCount++;
        }

        // Draw mesh center point
        if (chunkCount > 0)
        {
            Vector3 meshCenter = meshCenterSum / chunkCount;
            Gizmos.color = centerPointColor;
            Gizmos.DrawSphere(meshCenter, centerPointSize);
            DrawLabel(meshCenter + Vector3.up * centerPointSize * 1.5f, "MESH CENTER");
        }
    }

    private void DrawLabel(Vector3 position, string text)
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        UnityEditor.Handles.Label(position, text, style);
#endif
    }
}