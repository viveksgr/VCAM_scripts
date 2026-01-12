using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    public Transform player;       // assign to player
    public Transform cameraPivot;  // an empty GameObject at player head
    public Transform cameraTransform; // actual camera
    public float maxDistance = 0.3f; // how far camera can be from head
    public LayerMask collisionMask;

    void LateUpdate()
    {
        Vector3 direction = cameraTransform.position - cameraPivot.position;
        float distance = direction.magnitude;

        if (Physics.Raycast(cameraPivot.position, direction.normalized, out RaycastHit hit, maxDistance, collisionMask))
        {
            cameraTransform.position = cameraPivot.position + direction.normalized * (hit.distance - 0.05f);
        }
        else
        {
            cameraTransform.position = cameraPivot.position + direction.normalized * maxDistance;
        }
    }
}
