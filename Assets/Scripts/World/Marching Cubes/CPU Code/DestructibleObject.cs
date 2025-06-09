using UnityEngine;

/// <summary>
/// Attach this to any object that should be destroyed by explosions.
/// Automatically handles registration/unregistration with the explosion system.
/// </summary>
public class DestructibleObject : MonoBehaviour
{
    private void Start()
    {
        // Auto-register with explosion system
        ExplodingObject.RegisterDestructibleObject(gameObject);
    }

    private void OnDestroy()
    {
        // Auto-unregister when destroyed
        ExplodingObject.UnregisterDestructibleObject(gameObject);
    }
}