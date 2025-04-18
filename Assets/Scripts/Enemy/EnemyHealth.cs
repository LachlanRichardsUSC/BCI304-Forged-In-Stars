using UnityEngine;

// Define enemy types
public enum EnemyType
{
    Fast,
    Heavy
}

public class EnemyHealth : MonoBehaviour
{
    public EnemyType enemyType; // Assign this in the inspector

    private float currentHealth;

    void Start()
    {
        // Assign health based on enemy type
        switch (enemyType)
        {
            case EnemyType.Fast:
                currentHealth = 50f; // Light, quick enemy
                break;
            case EnemyType.Heavy:
                currentHealth = 150f; // Slow but tanky enemy
                break;
            default:
                currentHealth = 50f;
                break;
        }

        Debug.Log(gameObject.name + " initialized with " + currentHealth + " HP.");
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log(gameObject.name + " took " + amount + " damage! HP left: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " died!");
        Destroy(gameObject); // Optional: Play death animation before destroying
    }
}
