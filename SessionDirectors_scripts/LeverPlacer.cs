using System.Collections.Generic;
using UnityEngine;
using SwitchesLeversAndButtons; // ToggleInteractable

public class LeverPlacer : MonoBehaviour
{
    [System.Serializable]
    public class LeverUnit
    {
        [Tooltip("The ToggleInteractable on the lever handle")]
        public ToggleInteractable handle;

        [Tooltip("The Transform to move (usually the parent panel+handle). If null, uses handle.transform.root.")]
        public Transform moveRoot;

        [HideInInspector] public int lastSpawnIndex = -1; // for debug
    }

    [Header("Lever assemblies (exactly 3)")]
    public List<LeverUnit> levers = new List<LeverUnit>(3);

    [Header("Spawn points (15 empties)")]
    public List<Transform> spawnPoints = new List<Transform>(15);

    [Header("Placement options")]
    public bool alignRotation = true;                  // use spawn rotation
    public Vector3 positionOffset = Vector3.zero;      // optional local offset at spawn
    public Vector3 rotationOffsetEuler = Vector3.zero; // optional extra rotation at spawn

    [Header("Randomness")]
    [Tooltip("0 = time-based seed")]
    public int randomSeed = 0;

    private System.Random rng;

    void Awake()
    {
        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
    }

    /// <summary>
    /// Move the 3 lever assemblies to 3 unique spawn points and force them OFF (invoking events).
    /// Returns the chosen spawn indices in order of leverUnits.
    /// </summary>
    public int[] PlaceRandomAndReset()
    {
        if (levers.Count != 3 || spawnPoints.Count < 3)
        {
            Debug.LogWarning($"[LeverPlacer] Need 3 lever units and >=3 spawn points. Have levers={levers.Count}, points={spawnPoints.Count}");
            return new[] { -1, -1, -1 };
        }

        // Pick 3 unique spawn indices
        var chosen = new HashSet<int>();
        while (chosen.Count < 3) chosen.Add(rng.Next(0, spawnPoints.Count));

        // Assign in the order of levers list
        int i = 0;
        var indices = new int[3];

        foreach (var idx in chosen)
        {
            var unit = levers[i];
            var root = unit.moveRoot ? unit.moveRoot :
                       (unit.handle ? unit.handle.transform.root : null);

            if (root == null)
            {
                Debug.LogWarning($"[LeverPlacer] LeverUnit {i} has no moveRoot and no handle. Skipping.");
                indices[i] = -1;
            }
            else
            {
                var p = spawnPoints[idx];

                // Apply spawn transform + optional offsets
                Vector3 pos = p.position + (alignRotation ? (p.rotation * positionOffset) : positionOffset);
                Quaternion rot = alignRotation ? p.rotation * Quaternion.Euler(rotationOffsetEuler)
                                               : Quaternion.Euler(rotationOffsetEuler);

                root.SetPositionAndRotation(pos, rot);

                // Force lever state OFF and invoke event so DoorLockManager recomputes
                if (unit.handle != null)
                    unit.handle.ForceSet(false, invokeEvent: true);

                unit.lastSpawnIndex = idx;
                indices[i] = idx;
            }

            i++;
        }

        DataLogger.Instance?.LogEvent("LEVER_SPAWN_SET", "idx0,idx1,idx2", $"{indices[0]},{indices[1]},{indices[2]}");
        return indices;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Visualize spawn points
        Gizmos.color = Color.yellow;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            var t = spawnPoints[i];
            if (!t) continue;
            Gizmos.DrawWireSphere(t.position, 0.15f);
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.1f, $"S{i}");
        }
    }
#endif
}
