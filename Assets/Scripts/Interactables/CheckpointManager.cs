using UnityEngine;

// Manages player checkpoints and respawn logic
public class CheckpointManager : MonoBehaviour
{
    private Vector3 respawnPoint;                  // Stores the last checkpoint position
    private CharacterController controller;        // Reference to the CharacterController component
    private CharacterMovementController movementController; 


    void Start()
    {
        controller = GetComponent<CharacterController>(); // Get the CharacterController attached to this GameObject
        respawnPoint = transform.position;               // Set the initial respawn point to the starting position
        movementController = GetComponent<CharacterMovementController>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to a checkpoint
        if (other.CompareTag("Checkpoint"))
        {
            respawnPoint = other.transform.position;     // Update respawn point to the checkpoint's position
            Debug.Log("Checkpoint set!");                // Log checkpoint activation
        }
    }

    public void Respawn()
    {
        StartCoroutine(RespawnCoroutine());              // Start the respawn coroutine
    }

    private System.Collections.IEnumerator RespawnCoroutine()
    {
        movementController.enabled = false;
        controller.enabled = false;                      // Disable controller to safely move the player
        yield return null;                               // Wait one frame
        transform.position = respawnPoint;               // Move player to the respawn point
        controller.enabled = true;                       // Re-enable the controller
        movementController.enabled = true;

        ResourceManager resourceManager = GetComponent<ResourceManager>();
        if (resourceManager != null)
            resourceManager.ResetHealth();
    }

    void Update()
    {
        // For testing: if the R key is pressed, respawn the player
        if (Input.GetKeyDown(KeyCode.R))
        {
            Respawn();
        }
    }
}
