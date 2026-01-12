using UnityEngine;
using UnityEngine.UI;

public class TriggerImageOnArrival : MonoBehaviour
{
    public Image arrivalImage; // Assign the Canvas Image to show
    public float displayDuration = 0f; // Set > 0 to auto-hide after time

    private void Start()
    {
        if (arrivalImage != null)
            arrivalImage.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && arrivalImage != null)
        {
            arrivalImage.gameObject.SetActive(true);

            if (displayDuration > 0f)
                Invoke("HideImage", displayDuration);
        }
    }

    private void HideImage()
    {
        if (arrivalImage != null)
            arrivalImage.gameObject.SetActive(false);
    }
}
