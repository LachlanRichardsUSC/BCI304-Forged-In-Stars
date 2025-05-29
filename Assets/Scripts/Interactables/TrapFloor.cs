using UnityEngine;

public class TrapFloor : MonoBehaviour
{
    public float disableDelay = 0.2f;      // How fast it disappears after stepping on
    public float respawnDelay = 5f;        // Optional: bring trap back after X seconds
    private Renderer trapRenderer;
    private Collider trapCollider;

    void Start()
    {
        trapRenderer = GetComponent<Renderer>();
        trapCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(TriggerTrap());
        }
    }

    System.Collections.IEnumerator TriggerTrap()
    {
        yield return new WaitForSeconds(disableDelay);

        // Hide and disable collider so player falls
        trapRenderer.enabled = false;
        trapCollider.enabled = false;

        // Optional: bring the trap back after a delay
        yield return new WaitForSeconds(respawnDelay);
        trapRenderer.enabled = true;
        trapCollider.enabled = true;
    }
}
