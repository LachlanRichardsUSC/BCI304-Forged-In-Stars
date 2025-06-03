using System.Drawing;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    //Assigns enemies in inspector
    public GameObject fastenemyPrefab;
    public GameObject heavyenemyPrefab;
   
    //Postions where enemies will spawn
    public Transform[] spawnPoints;
    public Transform player;                // Assign your player GameObject here
    public float activationRadius = 10f;    // Spawner activates when player gets this close
    public int maxEnemiesToSpawn = 3;       // Spawn only 3 enemies
    private bool hasSpawned = false;        // Prevent multiple spawns


    // Update is called once per frame
    void Update()
    {
        if (hasSpawned) return; // Exit if already spawned

        // Check distance between player and this spawner
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= activationRadius)
        {
            SpawnEnemies();
            hasSpawned = true; // Mark as done
        }
    }

    void SpawnEnemies()
    {
        for (int i = 0; i < maxEnemiesToSpawn; i++)
        {
            // Pick random spawn point
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // Randomly decide enemy type
            GameObject enemyToSpawn = (Random.value > 0.5f) ? fastenemyPrefab : heavyenemyPrefab;

            Instantiate(enemyToSpawn, point.position, point.rotation);
        }

        Debug.Log("Spawner activated and spawned 3 enemies.");
    }
}



