using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject fastenemyPrefab;
    [SerializeField] private GameObject heavyenemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float activationRadius = 10f;
    [SerializeField] private int enemySpawnCount = 3;
    [SerializeField] private float spawnCooldown = 10f;

    [Header("Visual Indicator")]
    [SerializeField] private bool showRangeIndicator = true;
    [SerializeField] private float indicatorHeight = 0.1f;

    // Player Reference
    private Transform player;

    // Visual indicator components
    private LineRenderer rangeIndicator;

    private float lastSpawnTime = -10f;

    void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError("EnemySpawner: No GameObject with 'Player' tag found! Make sure your player has the 'Player' tag assigned.");
        }

        if (showRangeIndicator)
        {
            SetupRangeIndicator();
        }
    }

    void SetupRangeIndicator()
    {
        // Create LineRenderer for the circle
        GameObject indicatorObj = new GameObject("SpawnerRangeIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = Vector3.zero;

        rangeIndicator = indicatorObj.AddComponent<LineRenderer>();

        // Create glowing emissive material
        Material glowMaterial = CreateGlowMaterial();
        rangeIndicator.material = glowMaterial;

        rangeIndicator.startWidth = 0.15f;
        rangeIndicator.endWidth = 0.15f;
        rangeIndicator.useWorldSpace = false;
        rangeIndicator.loop = true;

        // Create circle points (same as before)
        int segments = 48;
        rangeIndicator.positionCount = segments;
        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            points[i] = new Vector3(
                Mathf.Cos(angle) * activationRadius,
                indicatorHeight,
                Mathf.Sin(angle) * activationRadius
            );
        }

        rangeIndicator.SetPositions(points);
    }

    Material CreateGlowMaterial()
    {
        Material glowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // URP transparency setup
        glowMat.EnableKeyword("_EMISSION");
        glowMat.SetFloat("_Surface", 0);
        glowMat.renderQueue = 2000;

        // Base material properties
        glowMat.SetColor("_BaseColor", Color.white);
        glowMat.SetFloat("_Metallic", 0f);
        glowMat.SetFloat("_Smoothness", 0.2f);

        return glowMat;
    }

    void Update()
    {
        // Check distance between player and this spawner
        float distance = Vector3.Distance(transform.position, player.position);
        bool playerInRange = distance <= activationRadius;

        if (playerInRange)
        {
            // Check if enough time has passed since last spawn
            if (Time.time >= lastSpawnTime + spawnCooldown)
            {
                SpawnEnemies();
                lastSpawnTime = Time.time;
            }
        }

        UpdateRangeIndicator(playerInRange);
    }

    void UpdateRangeIndicator(bool playerInRange)
    {
        if (!showRangeIndicator || rangeIndicator == null) return;

        bool onCooldown = Time.time < lastSpawnTime + spawnCooldown;
        Color baseColor;
        float pulseSpeed;
        float pulseIntensity;
        float hdrIntensity;

        // Set colors and pulse parameters based on state
        if (onCooldown)
        {
            if (playerInRange)
            {
                baseColor = Color.blue;
                pulseSpeed = 3f;
                pulseIntensity = 0.5f;
                hdrIntensity = 2.5f;
            }
            else
            {
                // Red - pulse between red and near-black
                baseColor = Color.red;
                pulseSpeed = 1f;
                pulseIntensity = 0.3f;
                hdrIntensity = 2f;

                // Custom pulse calculation for red - lerp to dark red/black
                float pulseValue = (1f + Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI)) * 0.5f;
                Color darkRed = new Color(0.1f, 0f, 0f);  // Very dark red, almost black

                Color redPulsedColor = Color.Lerp(darkRed, baseColor, pulseValue);
                Color redHdrEmissiveColor = redPulsedColor * hdrIntensity;

                // Set colors and return early to skip normal calculation
                rangeIndicator.material.SetColor("_BaseColor", redPulsedColor);
                rangeIndicator.material.SetColor("_EmissionColor", redHdrEmissiveColor);
                return;
            }
        }
        else
        {
            baseColor = Color.green;
            pulseSpeed = 1.8f;
            pulseIntensity = 0.4f;
            hdrIntensity = 3f;
        }

        // Calculate effects for blue and green states
        float minBrightness = 0.6f;
        float pulseMultiplier = minBrightness + pulseIntensity * (1f + Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI)) * 0.5f;
        float hdrPulseRange = 1.2f;
        float currentHDRIntensity = hdrIntensity * (1f + hdrPulseRange * (1f + Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI)) * 0.5f);

        Color pulsedColor = baseColor * pulseMultiplier;
        Color hdrEmissiveColor = baseColor * currentHDRIntensity;

        // URP material properties
        rangeIndicator.material.SetColor("_BaseColor", pulsedColor);
        rangeIndicator.material.SetColor("_EmissionColor", hdrEmissiveColor);
    }

        void SpawnEnemies()
    {
        for (int i = 0; i < enemySpawnCount; i++)
        {
            // Pick random spawn point
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            // Randomly decide enemy type
            GameObject enemyToSpawn = (Random.value > 0.5f) ? fastenemyPrefab : heavyenemyPrefab;
            Instantiate(enemyToSpawn, point.position, point.rotation);
        }
        Debug.Log("Spawner activated and spawned " + enemySpawnCount + " enemies.");
    }

}