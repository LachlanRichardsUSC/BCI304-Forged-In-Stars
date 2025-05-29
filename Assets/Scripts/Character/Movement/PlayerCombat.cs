using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    public float attackRange = 3f;              // Attack radius around player
    public float attackDamage = 10f;            // Damage dealt
    public LayerMask enemyLayer;                // What counts as an enemy
    public bool attackClosestOnly = true;       // Attack only closest enemy or all in range

    [Header("Visual Indicator")]
    public bool showRangeIndicator = true;      // Toggle the visual circle
    public float indicatorHeight = 0.1f;        // How high above ground to draw circle

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
            // Check if enemies are in range
            Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
            currentColor = enemiesInRange.Length > 0 ? enemiesInRangeColor : noEnemiesColor;
        }

        rangeIndicator.startColor = currentColor;
        rangeIndicator.endColor = currentColor;
    }

    void Attack()
    {
        // Find all enemies within attack range
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        if (enemiesInRange.Length == 0)
        {
            Debug.Log("No enemies in range!");
            return;
        }

        // Record attack time for visual feedback
        lastAttackTime = Time.time;

        if (attackClosestOnly)
        {
            // Attack only the closest enemy
            Collider closestEnemy = null;
            float closestDistance = float.MaxValue;

            foreach (Collider enemy in enemiesInRange)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                }
            }

            if (closestEnemy != null)
            {
                DamageEnemy(closestEnemy);
            }
        }
        else
        {
            // Attack all enemies in range
            foreach (Collider enemy in enemiesInRange)
            {
                DamageEnemy(enemy);
            }
        }
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
        // Draw attack range sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Show enemies in range
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
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