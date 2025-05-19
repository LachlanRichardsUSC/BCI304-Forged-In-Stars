using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float attackRange = 2f;              // How far the attack hits
    public float attackDamage = 10f;            // How much damage dealt
    public LayerMask enemyLayer;                // What counts as an enemy (set this in Inspector)

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            Attack(); // Call the attack function
        }
    }

    void Attack()
    {
        RaycastHit hit;

        // Cast a ray from the camera forward to detect if an enemy is in front
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, attackRange, enemyLayer))
        {
            Debug.Log("Hit " + hit.collider.name);

            // Try to get the EnemyHealth component on the hit object and apply damage
            EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(attackDamage); // This triggers enemy death if health reaches 0
            }
        }
        else
        {
            Debug.Log("Attack missed."); // No enemy in range
        }
    }
}
