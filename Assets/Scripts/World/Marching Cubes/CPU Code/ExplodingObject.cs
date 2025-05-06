using UnityEngine;

/// <summary>
/// Creates explosion effects in the terrain at the position of the object it's attached to.
/// Designed to be attached to a ground detector object that's always at ground level.
/// </summary>
public class ExplodingObject : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator terrainGenerator;

    [Header("Explosion Settings")]
    [SerializeField] private float explosionRadius = 20f;
    [SerializeField] private float explosionStrength = 1.5f;
    [SerializeField] private KeyCode explosionKey = KeyCode.E;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float effectDuration = 1f;

    [Header("Cooldown")]
    [SerializeField] private float explosionCooldown = 1.0f;
    private float lastExplosionTime;

    private void Start()
    {
        // Find terrain generator if not assigned
        if (terrainGenerator == null)
        {
            // terrainGenerator = FindObjectOfType<TerrainGenerator>();
            terrainGenerator = Object.FindAnyObjectByType<TerrainGenerator>();
            if (terrainGenerator == null)
            {
                Debug.LogError("Terrain Generator not found! Please assign it in the inspector.");
            }
        }

        lastExplosionTime = -explosionCooldown; // Allow immediate explosion on start
    }

    private void Update()
    {
        // Check for explosion key press with cooldown
        if (Input.GetKeyDown(explosionKey) && Time.time >= lastExplosionTime + explosionCooldown)
        {
            TriggerExplosion();
            lastExplosionTime = Time.time;
        }
    }

    /// <summary>
    /// Creates an explosion at this object's position (ground detector position)
    /// </summary>
    private void TriggerExplosion()
    {
        if (terrainGenerator == null)
        {
            Debug.LogError("Missing Terrain Generator reference for explosion!");
            return;
        }

        // Use this object's position directly since it's already at ground level
        Vector3 explosionPosition = transform.position;

        // Create explosion in terrain - radius and strength parameters are defined here only
        terrainGenerator.CreateExplosion(explosionPosition, explosionRadius, explosionStrength);

        // Spawn visual effect if prefab is assigned
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, explosionPosition, Quaternion.identity);
            Destroy(effect, effectDuration);
        }

        Debug.Log($"Explosion created at ground position ({explosionPosition})");
    }
}