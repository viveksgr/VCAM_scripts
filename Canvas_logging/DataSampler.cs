using UnityEngine;

public class DataSampler : MonoBehaviour
{
    [Header("Sources")]
    public Transform subject;   // your player/body
    public Transform head;      // your VR camera or head object

    void Update()
    {
        if (DataLogger.Instance != null)
            DataLogger.Instance.LogFrame(subject, head);
    }
}
