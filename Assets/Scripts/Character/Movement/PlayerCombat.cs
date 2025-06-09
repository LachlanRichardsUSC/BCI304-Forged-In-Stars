using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    public float attackRange = 6f;              // Attack radius around player
    public float attackHeight = 10f;             // Height of cylindrical attack area
    public float attackDamage = 25f;            // Damage dealt
    public LayerMask enemyLayer;                // What counts as an enemy

    [Header("Visual Indicator")]
    public bool showRangeIndicator = true;      // Toggle the visual circle
    public float indicatorHeight = 0.0f;        // How high above ground to draw circle

    [Header("Visual Effects")]
    [SerializeField] private GameObject attackEffectPrefab; // Choose an attack prefab vfx
    [SerializeField] private float effectDuration = 0.5f; // Duration of effect

    // Visual indicator components
    private LineRenderer rangeIndicator;

    // Colors for different states
    private readonly Color noEnemiesColor = Color.yellow;
    private readonly Color enemiesInRangeColor = Color.green;
    private readonly Color attackHitColor = Color.red;

    // State tracking
    private float lastAttackTime;
    private float attackHitDisplayDuration = 0.3f;

    void Start()
    {
        if (showRangeIndicator)
        {
            SetupRangeIndicator();
        }
    }

    void SetupRangeIndicator()
    {
        // Create LineRenderer for the circle
        GameObject indicatorObj = new GameObject("RangeIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = Vector3.zero;

        rangeIndicator = indicatorObj.AddComponent<LineRenderer>();
        Material indicatorMaterial = new Material(Shader.Find("Sprites/Default"));
        rangeIndicator.material = indicatorMaterial;
        rangeIndicator.startColor = noEnemiesColor;
        rangeIndicator.endColor = noEnemiesColor;
        rangeIndicator.startWidth = 0.08f;
        rangeIndicator.endWidth = 0.08f;
        rangeIndicator.useWorldSpace = false;
        rangeIndicator.loop = true;

        // Create circle points
        int segments = 32;
        rangeIndicator.positionCount = segments;
        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            points[i] = new Vector3(
                Mathf.Cos(angle) * attackRange,
                indicatorHeight,
                Mathf.Sin(angle) * attackRange
            );
        }

        rangeIndicator.SetPositions(points);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            Attack();
        }

        UpdateRangeIndicator();
    }

    void UpdateRangeIndicator()
    {
        if (!showRangeIndicator || rangeIndicator == null) return;

        Color currentColor;

        // Check if we're still showing attack hit color
        if (Time.time - lastAttackTime < attackHitDisplayDuration)
        {
            currentColor = attackHitColor;
        }
        else
        {
            // Check if enemies are in range using cylindrical detection
            Collider[] enemiesInRange = GetEnemiesInCylindricalRange();
            currentColor = enemiesInRange.Length > 0 ? enemiesInRangeColor : noEnemiesColor;
        }

        rangeIndicator.startColor = currentColor;
        rangeIndicator.endColor = currentColor;
    }

    void Attack()
    {
        if (attackEffectPrefab != null)
        {
            // Match player's Y rotation plus 90-degree correction (no Y variation for precise facing)
            float playerYRotation = transform.eulerAngles.y;
            float correctionOffset = 90f; // 90-degree offset to match character orientation

            // Random rotation on X and Z, Y follows player exactly
            Quaternion effectRotation = Quaternion.Euler(
                Random.Range(0f, 45f), // Random X rotation
                playerYRotation + correctionOffset, // Y follows player + correction (no random variation)
                Random.Range(0f, 45f)  // Random Z rotation
            );

            GameObject effect = Instantiate(attackEffectPrefab, transform.position, effectRotation);
            effect.transform.SetParent(transform);
            Destroy(effect, effectDuration);
        }

        // Find all enemies within cylindrical attack range
        Collider[] enemiesInRange = GetEnemiesInCylindricalRange();

        if (enemiesInRange.Length == 0)
        {
            Debug.Log("No enemies in range!");
            return;
        }

        // Record attack time for visual feedback
        lastAttackTime = Time.time;

        // Attack all enemies in range
        foreach (Collider enemy in enemiesInRange)
        {
            DamageEnemy(enemy);
        }
    }

    Collider[] GetEnemiesInCylindricalRange()
    {
        // Create cylindrical detection using OverlapCapsule
        Vector3 point1 = transform.position - Vector3.up * (attackHeight * 0.5f);
        Vector3 point2 = transform.position + Vector3.up * (attackHeight * 0.5f);

        return Physics.OverlapCapsule(point1, point2, attackRange, enemyLayer);
    }

    void DamageEnemy(Collider enemyCollider)
    {
        Debug.Log($"Hit {enemyCollider.name}!");

        EnemyHealth enemyHealth = enemyCollider.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(attackDamage);
        }
    }

    // Gizmos for debugging in Scene view
    void OnDrawGizmosSelected()
    {
        // Draw cylindrical attack range
        Gizmos.color = Color.cyan;

        // Draw the cylinder as a wireframe capsule
        Vector3 point1 = transform.position - Vector3.up * (attackHeight * 0.5f);
        Vector3 point2 = transform.position + Vector3.up * (attackHeight * 0.5f);

        // Draw cylinder body
        Gizmos.DrawWireSphere(point1, attackRange);
        Gizmos.DrawWireSphere(point2, attackRange);

        // Draw connecting lines for cylinder
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * attackRange;
            Gizmos.DrawLine(point1 + offset, point2 + offset);
        }

        // Show enemies in range
        Collider[] enemies = GetEnemiesInCylindricalRange();
        if (enemies.Length > 0)
        {
            Gizmos.color = Color.red;
            foreach (Collider enemy in enemies)
            {
                Gizmos.DrawLine(transform.position, enemy.transform.position);
            }
        }
    }
}