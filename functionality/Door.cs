using UnityEngine;

namespace DoorScript
{
    public class Door : MonoBehaviour
    {
        [Header("State")]
        public bool open;
        [SerializeField] private bool locked = true;  // stays locked until Unlock()

        [Header("Motion")]
        public float smooth = 1f;
        [SerializeField] private float doorOpenAngle = -90f;
        [SerializeField] private float doorCloseAngle = 0f;

        [Header("Player interaction")]
        [SerializeField] private Transform player;           // optional; can be auto-found
        [SerializeField] private bool autoFindPlayer = true; // auto-find on Awake if null
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private int mouseButton = 1;        // 0=LMB, 1=RMB, etc.
        [SerializeField] private KeyCode altKey = KeyCode.None; // optional alternate key

        public bool IsLocked => locked;

        void Awake()
        {
            if (!player && autoFindPlayer)
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged) player = tagged.transform;
                else if (Camera.main) player = Camera.main.transform; // fallback
            }
        }

        void Update()
        {
            // Interact: only if unlocked & we have a player & within range
            if (!locked && (Input.GetMouseButtonDown(mouseButton) ||
                            (altKey != KeyCode.None && Input.GetKeyDown(altKey))))
            {
                if (player && Vector3.Distance(player.position, transform.position) <= interactionRange)
                    ToggleDoor();
            }

            // Smooth rotate
            var targetRot = Quaternion.Euler(0, open ? doorOpenAngle : doorCloseAngle, 0);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, targetRot, Time.deltaTime * 5f * smooth);
        }

        private void ToggleDoor() => open = !open;

        // --- Public API used by your puzzle/session code ---

        // Called by DoorLockManager when puzzle solved
        public void Unlock() => locked = false;

        // Force a locked, closed state (use when respawning/resetting)
        public void LockDoor()
        {
            locked = true;
            open = false;
        }

        // Let the SessionDirector set the MainRoom player explicitly
        public void SetPlayer(Transform t) => player = t;

        // Optional one-shot open call (maintains toggle semantics)
        public void OpenDoor() => ToggleDoor();
    }
}
