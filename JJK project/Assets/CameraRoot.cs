using UnityEngine;

public class CameraRoot : MonoBehaviour
{
    [Tooltip("Assign the combat target the player orbits around.")]
    public Transform combatTarget;

    private void LateUpdate()
    {
        if (combatTarget == null) return;

        Vector3 toTarget = combatTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(toTarget);
    }
}