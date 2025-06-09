using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
/// <summary>
/// Simple portal that loads a level when player collides with it.
/// Attach to portal objects and set the scene name in inspector.
/// </summary>
public class LevelSelector : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField]
    [Tooltip("Name of the scene to load (must match exactly)")]
    private string sceneName = "";
    [SerializeField]
    [Tooltip("Display name for UI feedback")]
    private string levelDisplayName = "";
    [Header("Player Detection")]
    [SerializeField]
    [Tooltip("Tag to identify the player (usually 'Player')")]
    private string playerTag = "Player";
    [Header("Loading Settings")]
    [SerializeField]
    [Tooltip("Delay before loading scene (for transition effects)")]
    [Range(0f, 3f)] private float loadDelay = 0.5f;
    [SerializeField]
    [Tooltip("Show loading feedback in console")]
    private bool showDebugMessages = true;
    private bool isLoading = false;
    void Start()
    {
        // Validate scene name
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"Portal '{gameObject.name}' has no scene name assigned!", this);
        }
        // Ensure we have a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"Portal '{gameObject.name}' needs a Collider component set as trigger!", this);
        }
    }
    void OnTriggerEnter(Collider other)
    {
        // Check if it's the player and we're not already loading
        if (other.CompareTag(playerTag) && !isLoading)
        {
            StartCoroutine(LoadLevel());
        }
    }
    private IEnumerator LoadLevel()
    {
        isLoading = true;
        if (showDebugMessages)
        {
            string displayName = string.IsNullOrEmpty(levelDisplayName) ? sceneName : levelDisplayName;
            Debug.Log($"Loading level: {displayName}");
        }
        // Wait for delay (allows for transition effects)
        if (loadDelay > 0)
        {
            yield return new WaitForSeconds(loadDelay);
        }
        // Load the scene
        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load scene '{sceneName}': {e.Message}", this);
            isLoading = false; // Reset in case of error
        }
    }
    // Public method to load level (can be called by UI or other scripts)
    public void LoadLevelManually()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadLevel());
        }
    }
    // Validation in editor
    void OnValidate()
    {
        if (string.IsNullOrEmpty(levelDisplayName) && !string.IsNullOrEmpty(sceneName))
        {
            levelDisplayName = sceneName;
        }
    }
    // Debug visualization in scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 2f);
        // Draw portal direction if we have a forward vector
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}