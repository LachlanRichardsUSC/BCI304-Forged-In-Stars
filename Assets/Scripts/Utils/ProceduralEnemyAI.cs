using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy AI that wanders around and chases the player using A* pathfinding
/// replicates the navmesh based enemy AI.
/// Also runs like trash but OH WELL LOL
/// </summary>
public class ProceduralEnemyAI : MonoBehaviour
{
    public enum EnemyType { Fast, Heavy }

    [Header("Enemy Type")]
    [SerializeField] private EnemyType enemyType = EnemyType.Fast;

    [Header("References")]
    [SerializeField] private EnemyPathfinder pathfinder;
    [SerializeField] private Transform player;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private bool conformToTerrain = true;
    [SerializeField] private LayerMask terrainMask = 1;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float obstacleAvoidanceDistance = 2f;

    [Header("Combat")]
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime = 0f;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float losePlayerDistance = 25f; // Larger than detection for hysteresis
    [SerializeField] private LayerMask playerMask = 1;

    [Header("Wandering")]
    [SerializeField] private float wanderRadius = 20f;
    [SerializeField] private float wanderTimer = 5f;
    [SerializeField] private float idleTime = 2f;

    [Header("Pathfinding")]
    [SerializeField] private float pathUpdateInterval = 1f;
    [SerializeField] private float pathRecalculateDistance = 5f; // Recalculate if player moves this far
    [SerializeField] private bool enablePathfindingOptimization = true;

    [Header("Debug")]
    [SerializeField] private bool showDetectionRadius = true;
    [SerializeField] private bool showWanderArea = true;
    [SerializeField] private bool showCurrentPath = true;

    public enum EnemyState
    {
        Idle,
        Wandering,
        Chasing
    }

    [Header("State")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    private List<Vector3> currentPath;
    private int currentWaypointIndex;
    private float lastPathUpdate;
    private float wanderCooldown;
    private float idleCooldown;
    private Vector3 spawnPosition;
    private Vector3 lastPlayerPosition;
    private Vector3 targetPosition;

    void Start()
    {
        // Get references if not assigned
        if (pathfinder == null)
            pathfinder = GetComponent<EnemyPathfinder>();

        if (player == null)
            player = Object.FindFirstObjectByType<CharacterController>()?.transform; // Adjust based on your player setup

        spawnPosition = transform.position;
        SetState(EnemyState.Idle);
    }

    void Update()
    {
        UpdateStateMachine();
        FollowCurrentPath();
    }

    private void UpdateStateMachine()
    {
        bool playerInRange = IsPlayerInDetectionRange();

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState(playerInRange);
                break;

            case EnemyState.Wandering:
                HandleWanderingState(playerInRange);
                break;

            case EnemyState.Chasing:
                HandleChasingState(playerInRange);
                break;
        }
    }

    private void HandleIdleState(bool playerInRange)
    {
        idleCooldown -= Time.deltaTime;

        if (playerInRange)
        {
            SetState(EnemyState.Chasing);
        }
        else if (idleCooldown <= 0f)
        {
            SetState(EnemyState.Wandering);
        }
    }

    private void HandleWanderingState(bool playerInRange)
    {
        if (playerInRange)
        {
            SetState(EnemyState.Chasing);
            return;
        }

        wanderCooldown -= Time.deltaTime;

        // If we reached our wander target or timer expired, find new wander point
        if (HasReachedDestination() || wanderCooldown <= 0f)
        {
            Vector3 wanderTarget = pathfinder.GetRandomWalkablePosition(spawnPosition, wanderRadius);
            SetNewPath(wanderTarget);
            wanderCooldown = wanderTimer + Random.Range(-1f, 2f); // Add some randomness
        }
    }

    private void HandleChasingState(bool playerInRange)
    {
        if (!playerInRange && Vector3.Distance(transform.position, player.position) > losePlayerDistance)
        {
            Debug.Log("Lost player, returning to wander");
            SetState(EnemyState.Wandering);
            return;
        }

        // Update path to player periodically or if player moved significantly
        bool shouldUpdatePath = Time.time - lastPathUpdate > pathUpdateInterval;
        bool playerMovedFar = Vector3.Distance(player.position, lastPlayerPosition) > pathRecalculateDistance;

        if (enablePathfindingOptimization)
        {
            // Only update path if we really need to
            if (shouldUpdatePath || playerMovedFar || currentPath == null || currentPath.Count == 0)
            {
                SetNewPath(player.position);
                lastPlayerPosition = player.position;
            }
        }
        else
        {
            if (shouldUpdatePath || playerMovedFar)
            {
                SetNewPath(player.position);
                lastPlayerPosition = player.position;
            }
        }
    }

    /// <summary>
    /// Called when the enemy collides with a trigger (like the player)
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"Enemy touching player - Time since last attack: {Time.time - lastAttackTime}");

            // Damage player if cooldown has passed (regardless of enemy state)
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
                    Debug.LogWarning("ResourceManager component not found on Player GameObject!");
                }
            }
        }
    }

    // Add these for additional debugging
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered enemy trigger!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player exited enemy trigger!");
        }
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Exit current state
        switch (currentState)
        {
            case EnemyState.Idle:
                break;
            case EnemyState.Wandering:
                break;
            case EnemyState.Chasing:
                break;
        }

        currentState = newState;

        // Enter new state
        switch (currentState)
        {
            case EnemyState.Idle:
                idleCooldown = idleTime;
                currentPath = null;
                break;

            case EnemyState.Wandering:
                wanderCooldown = 0f; // Find wander target immediately
                break;

            case EnemyState.Chasing:
                if (player != null)
                {
                    SetNewPath(player.position);
                }
                break;
        }

        Debug.Log($"Enemy {gameObject.name} changed state to: {currentState}");
    }

    private void SetNewPath(Vector3 destination)
    {
        // Throttle pathfinding requests
        if (Time.time - lastPathUpdate < 0.2f) // Minimum 200ms between path requests
        {
            return;
        }

        targetPosition = destination;

        // Only calculate new path if pathfinder exists
        if (pathfinder != null)
        {
            currentPath = pathfinder.FindPath(transform.position, destination);
            currentWaypointIndex = 0;
            lastPathUpdate = Time.time;

            if (currentPath == null || currentPath.Count == 0)
            {
                Debug.LogWarning($"Failed to find path from {transform.position} to {destination}");
            }
            else
            {
                Debug.Log($"Found path with {currentPath.Count} waypoints");
            }
        }
        else
        {
            Debug.LogError("Pathfinder component missing!");
        }
    }

    private void FollowCurrentPath()
    {
        if (currentPath == null || currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
            return;

        Vector3 targetWaypoint = currentPath[currentWaypointIndex];
        Vector3 direction = (targetWaypoint - transform.position);
        direction.y = 0; // Keep movement horizontal

        float distanceToWaypoint = direction.magnitude;

        float effectiveStoppingDistance = (currentState == EnemyState.Chasing) ? 0.3f : stoppingDistance;
        if (distanceToWaypoint < effectiveStoppingDistance)
        {
            // Move to next waypoint
            currentWaypointIndex++;

            if (currentWaypointIndex >= currentPath.Count)
            {
                // Reached destination
                currentPath = null;
                Debug.Log("Reached path destination");
            }
        }
        else
        {
            // Move towards waypoint with obstacle avoidance
            direction = direction.normalized;

            // Check for obstacles in movement direction
            Vector3 moveDirection = GetObstacleAvoidedDirection(direction);

            // Apply movement
            Vector3 newPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;

            // Conform to terrain
            if (conformToTerrain)
            {
                newPosition = ConformPositionToTerrain(newPosition);
            }

            transform.position = newPosition;

            // Rotate towards movement direction
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    rotationSpeed * Time.deltaTime);
            }
        }
    }

    private Vector3 GetObstacleAvoidedDirection(Vector3 desiredDirection)
    {
        // Check if path is clear
        Vector3 checkPosition = transform.position + Vector3.up * 0.5f;

        if (!Physics.Raycast(checkPosition, desiredDirection, obstacleAvoidanceDistance, obstacleMask))
        {
            return desiredDirection; // Path is clear
        }

        // Try left and right alternatives
        Vector3 leftDirection = Quaternion.Euler(0, -45f, 0) * desiredDirection;
        Vector3 rightDirection = Quaternion.Euler(0, 45f, 0) * desiredDirection;

        bool leftClear = !Physics.Raycast(checkPosition, leftDirection, obstacleAvoidanceDistance, obstacleMask);
        bool rightClear = !Physics.Raycast(checkPosition, rightDirection, obstacleAvoidanceDistance, obstacleMask);

        if (leftClear && rightClear)
        {
            // Both sides clear, choose randomly or based on some logic
            return Random.value > 0.5f ? leftDirection : rightDirection;
        }
        else if (leftClear)
        {
            return leftDirection;
        }
        else if (rightClear)
        {
            return rightDirection;
        }

        // Try sharper angles
        leftDirection = Quaternion.Euler(0, -90f, 0) * desiredDirection;
        rightDirection = Quaternion.Euler(0, 90f, 0) * desiredDirection;

        leftClear = !Physics.Raycast(checkPosition, leftDirection, obstacleAvoidanceDistance, obstacleMask);
        rightClear = !Physics.Raycast(checkPosition, rightDirection, obstacleAvoidanceDistance, obstacleMask);

        if (leftClear)
        {
            return leftDirection;
        }
        else if (rightClear)
        {
            return rightDirection;
        }

        // No clear path, stop moving
        return Vector3.zero;
    }

    private Vector3 ConformPositionToTerrain(Vector3 position)
    {
        Vector3 rayStart = new Vector3(position.x, position.y + 50f, position.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainMask))
        {
            return new Vector3(position.x, hit.point.y + 0.1f, position.z);
        }

        return position;
    }

    private bool IsPlayerInDetectionRange()
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRadius)
        {
            // Add line of sight check
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            if (Physics.Raycast(transform.position + Vector3.up, directionToPlayer, out RaycastHit hit,
                detectionRadius, ~playerMask))
            {
                // Something is blocking view to player
                return hit.transform == player;
            }

            return true;
        }

        return false;
    }

    private bool HasReachedDestination()
    {
        return currentPath == null || currentWaypointIndex >= currentPath.Count;
    }

    void OnDrawGizmos()
    {
        // Draw detection radius
        if (showDetectionRadius)
        {
            Gizmos.color = Color.red * 0.3f;
            Gizmos.DrawSphere(transform.position, detectionRadius);

            Gizmos.color = Color.yellow * 0.2f;
            Gizmos.DrawSphere(transform.position, losePlayerDistance);
        }

        // Draw wander area
        if (showWanderArea)
        {
            Gizmos.color = Color.green * 0.2f;
            Gizmos.DrawSphere(spawnPosition, wanderRadius);
        }

        // Draw current path
        if (showCurrentPath && currentPath != null && currentPath.Count > 1)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }

            // Highlight current target waypoint
            if (currentWaypointIndex < currentPath.Count)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(currentPath[currentWaypointIndex], 0.5f);
            }
        }

        // Draw state info
        if (Application.isPlaying)
        {
            Vector3 labelPos = transform.position + Vector3.up * 3f;

#if UNITY_EDITOR
            string label = $"State: {currentState}\nType: {enemyType}\nDamage: {damageAmount}";
            UnityEditor.Handles.Label(labelPos, label);
#endif
        }
    }
}