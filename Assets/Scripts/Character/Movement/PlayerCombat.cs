using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float attackRange = 2f;              // How far the attack hits
    public float attackDamage = 10f;            // How much damage dealt
    public LayerMask enemyLayer;                // What counts as an enemy

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            Attack();
        }
    }

    void Attack()
    {
        RaycastHit hit;

        // Cast a ray from the camera forward
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, attackRange, enemyLayer))
        {
            Debug.Log("Hit " + hit.collider.name);

            // Apply damage if enemy has health
            hit.collider.GetComponent<EnemyHealth>()?.TakeDamage(attackDamage);
        }
        else
        {
            Debug.Log("Attack missed.");
        }
    }
}
