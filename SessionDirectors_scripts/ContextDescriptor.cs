// ContextDescriptor.cs
using System.Collections.Generic;
using UnityEngine;

public class ContextDescriptor : MonoBehaviour
{
    [Header("Identity")]
    public ContextId id;

    [Header("Player spawns (pick 1 at random)")]
    public List<Transform> startPoints = new List<Transform>(5);   // fill with 5 empties

    [Header("Door (optional)")]
    public DoorScript.Door door;                                    // your existing door (can be null)

    [Header("Lever placement (choose ONE)")]
    public LeverPlacer placer;       // moves 3 existing levers to 3 of 15 points
    public LeverSpawner spawner;     // OR: instantiates levers at 3 of 15 points

    public Transform GetRandomStart()
    {
        if (startPoints == null || startPoints.Count == 0) return null;
        return startPoints[Random.Range(0, startPoints.Count)];
    }
}
