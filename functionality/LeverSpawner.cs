using System.Collections.Generic;
using UnityEngine;

public class LeverSpawner : MonoBehaviour
{
    [Tooltip("Predefined spawn points (e.g., 15 empties under this root).")]
    public List<Transform> spawnPoints = new List<Transform>(15);

    [Tooltip("Lever prefab to spawn (ToggleInteractable).")]
    public GameObject leverPrefab;

    [Tooltip("Optional fixed seed (0 = time-based).")]
    public int randomSeed = 0;

    private readonly List<GameObject> liveLevers = new List<GameObject>(3);
    private System.Random rng;

    void Awake()
    {
        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
    }

    public void ResetState()
    {
        foreach (var go in liveLevers) if (go) Destroy(go);
        liveLevers.Clear();
    }

    /// <summary>Spawn 3 unique levers and return the chosen indices.</summary>
    public int[] SpawnThree()
    {
        if (leverPrefab == null || spawnPoints.Count < 3)
        {
            Debug.LogWarning("[LeverSpawner] Need a prefab and at least 3 spawn points.");
            return new[] { -1, -1, -1 };
        }

        ResetState();

        var chosen = new HashSet<int>();
        while (chosen.Count < 3)
            chosen.Add(rng.Next(0, spawnPoints.Count));

        int[] arr = new int[3];
        int i = 0;
        foreach (var idx in chosen)
        {
            var t = spawnPoints[idx];
            var go = Instantiate(leverPrefab, t.position, t.rotation, transform);
            liveLevers.Add(go);
            arr[i++] = idx;
        }

        DataLogger.Instance?.LogEvent("LEVER_SPAWN_SET", "idx0,idx1,idx2", $"{arr[0]},{arr[1]},{arr[2]}");
        return arr;
    }
}
