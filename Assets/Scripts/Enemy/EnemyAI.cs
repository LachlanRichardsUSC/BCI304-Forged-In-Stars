using UnityEngine;
using UnityEngine.AI; // Needed for NavMeshAgent
public class EnemyAI : MonoBehaviour
{
    public enum EnemyType { Fast, Heavy }
    public EnemyType enemyType; // Type of enemy (Fast or Heavy)
    public float detectionRange = 10f; // How far the enemy can "see" the player
    public float damageAmount = 10f;   // How much damage it does on contact
    public float attackCooldown = 2f; // Time between attacks
    private float lastAttackTime = 0f; // Time of the last attack
    public Transform player;           // Reference to the player
    private NavMeshAgent agent;        // Unity's pathfinding component
    private float speedmultiplier = 1f; // Speed multiplier based on enemy type
    void Start()
    {
        // Get the NavMeshAgent on this object
        agent = GetComponent<NavMeshAgent>();
        // Find the player by tag
        player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player == null)
        {
            Debug.LogError("Player not found! Make sure the player has the 'Player' tag.");
        }
        // Set different behaviors based on enemy type
        if (enemyType == EnemyType.Fast)
        {
            agent.speed = 5f * speedmultiplier;  // Fast enemies move quickly
            attackCooldown = 1f; // Quick attack speed
            damageAmount = 5f;   // Light damage
        }
        else if (enemyType == EnemyType.Heavy)
        {
            agent.speed = 2f * speedmultiplier;  // Slow enemies move slowly
            attackCooldown = 3f; // Slow attack speed
            damageAmount = 20f;   // Heavy damage
        }
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
    private void OnTriggerStay(Collider other) // called every frame as long as another collider stays inside the trigger.
    {
        if (other.CompareTag("Player"))
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                 ResourceManager resourceManager = other.GetComponent<ResourceManager>();
                if (resourceManager != null)
                {
                    Debug.Log($"{enemyType} enemy hit player for {damageAmount} damage!");
                    resourceManager.TakeDamage(damageAmount);
                    lastAttackTime = Time.time;
                }
                else
                {
                    Debug.LogWarning("PlayerHealth component not found on Player GameObject!");
                }
            }
        }
    }
}