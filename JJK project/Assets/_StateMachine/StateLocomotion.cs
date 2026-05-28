using UnityEngine;

/// <summary>
/// Handles grounded movement: orbit, approach, blend tree, face target.
/// Sets Blackboard.WantsToJump when a jump is queued.
/// Sets Blackboard.WantsToRunStop when the player releases the stick while moving.
/// PlayerController reads those flags and drives all transitions.
/// </summary>
public class StateLocomotion : PlayerStateBase
{
    // ── Animator hashes ───────────────────────────────────────────────────
    private static readonly int HashMoveX    = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY    = Animator.StringToHash("MoveY");
    private static readonly int HashGrounded = Animator.StringToHash("IsGrounded");

    // ── Blend directions ──────────────────────────────────────────────────
    private static readonly Vector2[] LocoBlendDirs =
    {
        new Vector2( 0f,  1f),
        new Vector2( 1f,  1f).normalized,
        new Vector2( 1f,  0f),
        new Vector2( 1f, -1f).normalized,
        new Vector2( 0f, -1f),
        new Vector2(-1f, -1f).normalized,
        new Vector2(-1f,  0f),
        new Vector2(-1f,  1f).normalized,
    };

    private bool    _wasMovingLastFrame;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void OnEnter()
    {
        Blackboard.IsJumping        = false;
        Blackboard.IsLanding        = false;
        Blackboard.VerticalVelocity = -2f;
        Blackboard.WantsToJump      = false;
        Blackboard.WantsToRunStop   = false;

        Blackboard.Animator.SetBool(HashGrounded, true);

        Debug.Log("[StateLocomotion] Entered.");
    }

    public override void OnUpdate()
    {
        // Jump takes highest priority
        if (Blackboard.JumpQueued)
        {
            Blackboard.WantsToJump = true;
            return;
        }

        // Walked off an edge
        if (!Blackboard.IsGrounded)
        {
            Blackboard.WantsToJump = true;
            return;
        }

        // Detect the frame the player releases the stick while moving
        bool isMovingNow = Blackboard.CurrentInput.magnitude > Blackboard.InputThreshold;
        if (_wasMovingLastFrame && !isMovingNow)
        {
            // Record which direction we were moving so StateRunStop
            // knows which stop clip to play
            Blackboard.RunStopDirection = SnapToNearest(
                Blackboard.SmoothedBlendInput.normalized, LocoBlendDirs);

            Blackboard.WantsToRunStop = true;
            _wasMovingLastFrame = false;
            return;
        }
        _wasMovingLastFrame = isMovingNow;

        ApplyOrbit();
        ApplyApproach();
        ApplyGroundingForce();
        ApplyPosition();
        DriveAnimator();
        FaceTarget();
    }

    public override void OnExit()
    {
        Debug.Log("[StateLocomotion] Exited.");
    }

    // ── Movement ──────────────────────────────────────────────────────────

    private void ApplyOrbit()
    {
        if (Mathf.Abs(Blackboard.CurrentInput.x) < 0.01f) return;
        Blackboard.CurrentOrbitAngle -= Blackboard.CurrentInput.x
                                        * Blackboard.OrbitSpeed
                                        * Time.deltaTime;
    }

    private void ApplyApproach()
    {
        if (Mathf.Abs(Blackboard.CurrentInput.y) < 0.01f) return;
        Blackboard.CurrentDistance -= Blackboard.CurrentInput.y
                                      * Blackboard.ApproachSpeed
                                      * Time.deltaTime;
        Blackboard.CurrentDistance = Mathf.Clamp(
            Blackboard.CurrentDistance,
            Blackboard.MinDistance,
            Blackboard.MaxDistance);
    }

    private void ApplyGroundingForce()
    {
        Blackboard.VerticalVelocity = -2f;
    }

    private void ApplyPosition()
    {
        Vector3 orbitPos = OrbitPosition(
            Blackboard.CurrentOrbitAngle,
            Blackboard.CurrentDistance,
            Blackboard.Target);

        Vector3 horizontalDelta = new Vector3(
            orbitPos.x - Blackboard.CharacterController.transform.position.x,
            0f,
            orbitPos.z - Blackboard.CharacterController.transform.position.z);

        Vector3 motion = horizontalDelta
                       + Vector3.up * (Blackboard.VerticalVelocity * Time.deltaTime);

        Blackboard.LastCollisionFlags = Blackboard.CharacterController.Move(motion);
    }

    private void FaceTarget()
    {
        if (Blackboard.Target == null) return;
        Vector3 toTarget = Blackboard.Target.position
                         - Blackboard.CharacterController.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;
        Blackboard.CharacterController.transform.rotation =
            Quaternion.LookRotation(toTarget);
    }

    // ── Animator ─────────────────────────────────────────────────────────

    private void DriveAnimator()
    {
        Vector2 snapTarget = Blackboard.CurrentInput.magnitude > Blackboard.InputThreshold
            ? SnapToNearest(Blackboard.CurrentInput.normalized, LocoBlendDirs)
            : Vector2.zero;

        bool  isStopping = snapTarget == Vector2.zero;
        float speed      = isStopping
            ? Blackboard.AnimBlendStopSpeed
            : Blackboard.AnimBlendStartSpeed;

        Blackboard.SmoothedBlendInput = Vector2.MoveTowards(
            Blackboard.SmoothedBlendInput,
            snapTarget,
            speed * Time.deltaTime);

        Blackboard.Animator.SetFloat(HashMoveX,   Blackboard.SmoothedBlendInput.x);
        Blackboard.Animator.SetFloat(HashMoveY,   Blackboard.SmoothedBlendInput.y);
        Blackboard.Animator.SetBool(HashGrounded, Blackboard.IsGrounded);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Vector2 SnapToNearest(Vector2 normalizedInput, Vector2[] dirs)
    {
        Vector2 best    = dirs[0];
        float   bestDot = float.MinValue;
        foreach (Vector2 dir in dirs)
        {
            float dot = Vector2.Dot(normalizedInput, dir);
            if (dot > bestDot) { bestDot = dot; best = dir; }
        }
        return best;
    }

    private static Vector3 OrbitPosition(float angleDeg, float distance, Transform target)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return target.position
             + new Vector3(Mathf.Sin(rad) * distance, 0f, Mathf.Cos(rad) * distance);
    }
}
