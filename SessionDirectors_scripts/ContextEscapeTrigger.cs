// ContextEscapeTrigger.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ContextEscapeTrigger : MonoBehaviour
{
    public SessionDirectorAdditive director;
    public string playerTag = "Player";
    public bool autoAddKinematicRb = true;

    void Reset() { var c = GetComponent<Collider>(); c.isTrigger = true; }

    void Awake()
    {
        if (!director) director = FindObjectOfType<SessionDirectorAdditive>();
        if (autoAddKinematicRb && !GetComponent<Rigidbody>())
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform.root;
        Debug.Log($"[EscapeTrigger] Hit by '{other.name}' (root '{root.name}', tag '{root.tag}')");
        if (!root.CompareTag(playerTag)) return;
        if (!director) { Debug.LogError("[EscapeTrigger] No SessionDirectorAdditive found."); return; }
        director.NotifyEscape();
    }
}
