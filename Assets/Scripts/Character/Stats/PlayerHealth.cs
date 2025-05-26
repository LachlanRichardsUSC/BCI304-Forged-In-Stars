using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float health = 100f;

    //  to reduce health
    public void TakeDamage(float amount)
    {
        health -= amount;
        Debug.Log("Player Health: " + health);

        if (health <= 0)
        {
            Debug.Log("Player is dead!");
        
        }
    }
}
