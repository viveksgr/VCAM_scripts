using UnityEngine;
using SwitchesLeversAndButtons;   // ToggleInteractable

public class DoorLockManager : MonoBehaviour
{
    [Header("Door & Lever Sequence (must be 1 → 2 → 3 ON)")]
    [SerializeField] private DoorScript.Door door;
    [SerializeField] private ToggleInteractable lever1;
    [SerializeField] private ToggleInteractable lever2;
    [SerializeField] private ToggleInteractable lever3;

    [SerializeField] private DoorFeedbackUI doorFeedback;  // assign in Inspector

    private bool l1On, l2On, l3On;
    private int sequenceStep = 0; // 0→1→2→3 (3 = unlocked)

    void Awake()
    {
        lever1.OnInteract += OnLever1Changed;
        lever2.OnInteract += OnLever2Changed;
        lever3.OnInteract += OnLever3Changed;
    }

    void OnDestroy()
    {
        lever1.OnInteract -= OnLever1Changed;
        lever2.OnInteract -= OnLever2Changed;
        lever3.OnInteract -= OnLever3Changed;
    }

    void OnLever1Changed(bool isOn)
    {
        l1On = isOn;
        DataLogger.Instance?.LogLever(1, isOn ? +1 : -1);
        HandleChange(1, isOn);
    }

    void OnLever2Changed(bool isOn)
    {
        l2On = isOn;
        DataLogger.Instance?.LogLever(2, isOn ? +1 : -1);
        HandleChange(2, isOn);
    }

    void OnLever3Changed(bool isOn)
    {
        l3On = isOn;
        DataLogger.Instance?.LogLever(3, isOn ? +1 : -1);
        HandleChange(3, isOn);
    }

    void HandleChange(int leverIndex, bool turnedOn)
    {
        if (turnedOn)
        {
            // advance only on the expected ON in order
            if (sequenceStep == 0 && leverIndex == 1) sequenceStep = 1;
            else if (sequenceStep == 1 && leverIndex == 2) sequenceStep = 2;
            else if (sequenceStep == 2 && leverIndex == 3) sequenceStep = 3;
            // else: out-of-order ON → ignore (no reset)
        }
        else
        {
            // OFF cancels progress: recompute longest prefix ON
            sequenceStep = ComputePrefixOn();
        }

        if (sequenceStep == 3) UnlockDoor();
    }

    int ComputePrefixOn()
    {
        if (!l1On) return 0;
        if (!l2On) return 1;
        return l3On ? 3 : 2;
    }

    void UnlockDoor()
    {
        door.Unlock();
        Debug.Log("Door unlocked!");
        DataLogger.Instance?.LogEvent("DOOR_UNLOCKED");

        doorFeedback?.ShowUnlocked(3f, "Door unlocked");   // <-- show only on success
    }
}
