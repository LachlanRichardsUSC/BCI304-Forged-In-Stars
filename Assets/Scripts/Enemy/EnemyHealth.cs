using UnityEngine;
using System.Collections;

// Define enemy types
public enum EnemyType
{
    Fast,
    Heavy
}

public class EnemyHealth : MonoBehaviour
{
    public EnemyType enemyType; // Choose Fast or Heavy in the Inspector
    private float currentHealth;

    private bool isDying = false; // Prevent multiple fade calls

    void Start()
    {
        // Set health based on type
        switch (enemyType)
        {
            case EnemyType.Fast:
                currentHealth = 50f;
                break;
            case EnemyType.Heavy:
                currentHealth = 150f;
                break;
            default:
                currentHealth = 50f;
                break;
        }

        Debug.Log(gameObject.name + " initialized with " + currentHealth + " HP.");
    }

    public void TakeDamage(float amount)
    {
        if (isDying) return;

        currentHealth -= amount;
        Debug.Log(gameObject.name + " took " + amount + " damage! HP left: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDying) return;
        isDying = true;

        Debug.Log(gameObject.name + " died!");
        StartCoroutine(SmoothFadeOutAndDestroy());
    }

    IEnumerator SmoothFadeOutAndDestroy()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float duration = 2f; // Longer fade = smoother look
        float timer = 0f;

        // Convert materials to transparent
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                mat.SetFloat("_Mode", 2); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
        }

        // Smooth alpha fade
        while (timer < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            foreach (Renderer rend in renderers)
            {
                foreach (Material mat in rend.materials)
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
