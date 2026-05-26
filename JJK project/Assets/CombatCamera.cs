using UnityEngine;

public class CombatCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Positioning")]
    [Tooltip("How far behind the player the camera sits")]
    public float followDistance = 5f;

    [Tooltip("Height offset above the player's root")]
    public float heightOffset = 1.8f;

    [Tooltip("How quickly the camera follows position changes")]
    public float positionSmoothTime = 0.1f;

    [Tooltip("How quickly the camera rotates to look at the player")]
    public float rotationSmoothSpeed = 10f;

    // ── Internal ─────────────────────────────────────────────────────────
    private Vector3 _velocity; // used by SmoothDamp

    // ── Unity lifecycle ───────────────────────────────────────────────────


    void FixedUpdate()
    {
        if (player == null) return;

        UpdatePosition();
        UpdateRotation();
    }

    // ── Camera logic ──────────────────────────────────────────────────────

    /// <summary>
    /// Positions the camera directly behind the player using the player's
    /// forward direction (which always faces the target).
    /// </summary>
    private void UpdatePosition()
    {
        // "Behind" the player = opposite of player.forward
        Vector3 desiredPos = player.position
                             - player.forward * followDistance
                             + Vector3.up * heightOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref _velocity,
            positionSmoothTime
        );
    }

    /// <summary>
    /// Rotates the camera so it always looks at the player.
    /// </summary>
    private void UpdateRotation()
    {
        Vector3 lookDir = (player.position + Vector3.up * (heightOffset * 0.5f))
                          - transform.position;

        if (lookDir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSmoothSpeed * Time.deltaTime
        );
    }
}
