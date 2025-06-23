using System.Collections.Generic;
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

    [Header("Object Destruction")]
    [SerializeField] private float destructionRadius = 8f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float effectDuration = 1f;
    [SerializeField] private float effectScale = 1f;

    [Header("Cooldown")]
    [SerializeField] private float explosionCooldown = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Static list for tracking all destructible objects
    private static HashSet<GameObject> destructibleObjects = new HashSet<GameObject>();

    private float lastExplosionTime;

    private void Start()
    {
        // Find terrain generator if not assigned
        if (terrainGenerator == null)
        {
            terrainGenerator = Object.FindAnyObjectByType<TerrainGenerator>();
            if (terrainGenerator == null)
            {
                Debug.LogError("Terrain Generator not found! Please assign it in the inspector.");
            }
        }

        // Populate list with existing tagged objects in scene
        PopulateDestructibleList();

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

        // Debug info
        if (showDebugInfo && Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log($"Tracking {destructibleObjects.Count} destructible objects");
        }
    }

    /// <summary>
    /// Populates the destructible objects list with existing scene objects
    /// </summary>
    private void PopulateDestructibleList()
    {
        DestructibleObject[] destructibleComponents = Object.FindObjectsByType<DestructibleObject>(FindObjectsSortMode.None);

        foreach (DestructibleObject component in destructibleComponents)
        {
            RegisterDestructibleObject(component.gameObject);
        }

        if (showDebugInfo)
        {
            Debug.Log($"Found and registered {destructibleComponents.Length} destructible objects in scene");
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

        // Destroy objects in range before deforming terrain
        DestroyObjectsInRange(explosionPosition);

        // Create explosion in terrain
        terrainGenerator.CreateExplosion(explosionPosition, explosionRadius, explosionStrength);

        // Spawn visual effect if prefab is assigned
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, explosionPosition, Quaternion.identity);
            effect.transform.localScale = Vector3.one * effectScale;
            Destroy(effect, effectDuration);
        }

        Debug.Log($"SDF explosion created at ground position ({explosionPosition})");
    }

    /// <summary>
    /// Destroys all tracked destructible objects within the destruction radius
    /// </summary>
    private void DestroyObjectsInRange(Vector3 explosionCenter)
    {
        List<GameObject> objectsToDestroy = new List<GameObject>();

        // Collect objects to destroy (can't modify collection while iterating)
        foreach (GameObject obj in destructibleObjects)
        {
            if (obj != null) // Object might have been destroyed already
            {
                float distance = Vector3.Distance(obj.transform.position, explosionCenter);
                if (distance <= destructionRadius)
                {
                    objectsToDestroy.Add(obj);
                }
            }
        }

        // Destroy collected objects
        foreach (GameObject obj in objectsToDestroy)
        {
            UnregisterDestructibleObject(obj);
            Destroy(obj);
        }

        if (objectsToDestroy.Count > 0 && showDebugInfo)
        {
            Debug.Log($"Destroyed {objectsToDestroy.Count} objects in explosion range");
        }
    }

    /// <summary>
    /// Registers an object as destructible. Call this when spawning new destructible objects.
    /// </summary>
    public static void RegisterDestructibleObject(GameObject obj)
    {
        if (obj != null && obj.GetComponent<DestructibleObject>() != null)
        {
            destructibleObjects.Add(obj);
        }
    }

    /// <summary>
    /// Unregisters an object from the destructible list. Call this before destroying objects manually.
    /// </summary>
    public static void UnregisterDestructibleObject(GameObject obj)
    {
        destructibleObjects.Remove(obj);
    }

    /// <summary>
    /// Gets the current count of tracked destructible objects
    /// </summary>
    public static int GetDestructibleObjectCount()
    {
        return destructibleObjects.Count;
    }

    /// <summary>
    /// Clears the static lists (useful for scene changes)
    /// </summary>
    public static void ClearDestructibleLists()
    {
        destructibleObjects.Clear();
    }
}