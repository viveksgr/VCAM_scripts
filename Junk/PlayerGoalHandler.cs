using UnityEngine;
using UnityEngine.UI;

public class PlayerGoalHandler : MonoBehaviour
{
    public Transform player;              // Assign player object
    public Transform respawnPoint;        // Where to send player after reaching goal
    public Image goalImage;               // Canvas image to display
    public float displayDuration = 3f;    // Time to show image before respawn

    private bool goalReached = false;

    void OnTriggerEnter(Collider other)
    {
        if (!goalReached && other.transform == player)
        {
            goalReached = true;
            StartGoalSequence();
        }
    }

    void StartGoalSequence()
    {
        if (goalImage != null)
            goalImage.gameObject.SetActive(true);

        Invoke("RespawnPlayer", displayDuration);
    }

    void RespawnPlayer()
    {
        if (goalImage != null)
            goalImage.gameObject.SetActive(false);

        if (player != null && respawnPoint != null)
            player.position = respawnPoint.position;

        // Optional: reset velocity if using Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
            rb.velocity = Vector3.zero;

        // Optional: trigger reset logic in other systems here
        goalReached = false; // reset for future triggers if needed
    }
}
