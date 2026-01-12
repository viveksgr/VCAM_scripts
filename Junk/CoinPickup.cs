using UnityEngine;
using TMPro;

public class CoinPickup : MonoBehaviour
{
    public TextMeshProUGUI pickupText; // Assign in Inspector
    public float textDisplayDuration = 2f;

    private void Start()
    {
        if (pickupText != null)
            pickupText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (pickupText != null)
            {
                pickupText.gameObject.SetActive(true);
                Invoke("HideText", textDisplayDuration);
            }

            gameObject.SetActive(false); // Make coin disappear
        }
    }

    void HideText()
    {
        if (pickupText != null)
            pickupText.gameObject.SetActive(false);
    }
}
