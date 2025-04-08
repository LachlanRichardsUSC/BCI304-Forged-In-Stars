using UnityEngine;
using UnityEngine.AI; // Needed for NavMeshAgent

public class EnemyAI : MonoBehaviour
{
    public float detectionRange = 10f; // How far the enemy can "see" the player
    public float damageAmount = 10f;   // How much damage it does on contact
    public Transform player;           // Reference to the player
    private NavMeshAgent agent;        // Unity's pathfinding component

    void Start()
    {
        // Get the NavMeshAgent on this object
        agent = GetComponent<NavMeshAgent>();

        // Find the player by tag
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        // Check distance between enemy and player
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < detectionRange)
        {
            // If player is in range, move toward them
            agent.SetDestination(player.position);
        }
    }

    // Called when the enemy collides with a trigger (like the player)
    private void OnTriggerEnter(Collider other)
    {
        // If it collides with player, deal damage
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player hit!");

            // You need to have a method in your player to take damage, like this:
            other.GetComponent<PlayerHealth>().TakeDamage(damageAmount);
        }
    }
}
