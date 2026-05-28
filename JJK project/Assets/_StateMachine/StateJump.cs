using UnityEngine;

/// <summary>
/// Handles all airborne logic: liftoff, directional arc, gravity, landing anticipation.
/// Sets Blackboard.WantsToLand when CC reports ground contact.
/// PlayerController reads that flag and drives the transition.
/// </summary>
public class StateJump : PlayerStateBase
{
    // ── Animator hashes ───────────────────────────────────────────────────
    private static readonly int HashJumpX     = Animator.StringToHash("JumpX");
    private static readonly int HashJumpY     = Animator.StringToHash("JumpY");
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int HashIsFalling = Animator.StringToHash("IsFalling");
    private static readonly int HashIsLanding = Animator.StringToHash("IsLanding");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");

    private static readonly Vector2[] JumpBlendDirs =
    {
        new Vector2( 0f,  1f),
        new Vector2( 0f, -1f),
        new Vector2(-1f,  0f),
        new Vector2( 1f,  0f),
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void OnEnter()
    {
        Blackboard.WantsToLand = false;
        Debug.Log("[StateJump] Entered.");
    }

    public override void OnUpdate()
    {
        HandleCoyoteTime();
        HandleJumpTimeout();
        HandleLandingTimer();
        ApplyGravity();
        ApplyHorizontalArc();
        ApplyPosition();
        FaceTarget();

        // Signal landing intent — PlayerController transitions to Land
        bool hitGround = (Blackboard.LastCollisionFlags & CollisionFlags.Below) != 0;
        if (hitGround && (Blackboard.IsJumping || !Blackboard.JumpQueued))
            Blackboard.WantsToLand = true;
    }

    public override void OnExit()
    {
        Blackboard.WantsToLand = false;
        Debug.Log("[StateJump] Exited.");
    }

    // ── Public — called by PlayerController ───────────────────────────────

    public void OnJumpPressed(System.EventArgs e)
    {
        if (e is not InputManager.DashJumpEventArgs args) return;
        if (!args.IsDoubleTap) return;

        bool canJump = (Blackboard.IsGrounded || Blackboard.CoyoteTimer > 0f)
                    && Blackboard.JumpCooldownTimer <= 0f
                    && !Blackboard.IsJumping
                    && !Blackboard.JumpQueued;

        if (!canJump)
        {
            Debug.Log($"[StateJump] Jump blocked | grounded:{Blackboard.IsGrounded} " +
                      $"coyote:{Blackboard.CoyoteTimer:F3} " +
                      $"cooldown:{Blackboard.JumpCooldownTimer:F3}");
            return;
        }

        Blackboard.JumpDirection = args.Direction.magnitude > Blackboard.InputThreshold
            ? SnapToNearest(args.Direction.normalized, JumpBlendDirs)
            : Vector2.zero;

        Blackboard.CurrentJumpType = ClassifyJump(args.Direction);

        float activeHeight   = GetActiveHeight(Blackboard.CurrentJumpType);
        float launchVelocity = Mathf.Sqrt(2f * Blackboard.RiseGravity * activeHeight);
        float totalAirTime   = (launchVelocity / Blackboard.RiseGravity)
                             + Mathf.Sqrt(2f * activeHeight / Blackboard.FallGravity);

        ComputeHorizontalVelocities(Blackboard.CurrentJumpType, totalAirTime);

        Blackboard.LandingTriggerTimer = totalAirTime - Blackboard.LandingAnticipationTime;
        Blackboard.LandingTimerActive  = false;
        Blackboard.JumpQueued          = true;
        Blackboard.JumpQueuedTime      = Time.time;
        Blackboard.IsLanding           = false;

        Blackboard.Animator.SetFloat(HashJumpX,    Blackboard.JumpDirection.x);
        Blackboard.Animator.SetFloat(HashJumpY,    Blackboard.JumpDirection.y);
        Blackboard.Animator.SetBool(HashIsJumping, true);
        Blackboard.Animator.SetBool(HashIsFalling, false);
        Blackboard.Animator.SetBool(HashIsLanding, false);
        Blackboard.Animator.SetBool(HashGrounded,  false);

        Debug.Log($"[StateJump] Jump queued | type:{Blackboard.CurrentJumpType} " +
                  $"height:{activeHeight:F2} airTime:{totalAirTime:F3}s");
    }

    public void OnJumpLiftoff()
    {
        if (!Blackboard.JumpQueued)
        {
            Debug.Log("[StateJump] Liftoff fired but nothing queued — ignoring.");
            return;
        }

        float activeHeight          = GetActiveHeight(Blackboard.CurrentJumpType);
        Blackboard.VerticalVelocity = Mathf.Sqrt(2f * Blackboard.RiseGravity * activeHeight);
        Blackboard.IsJumping        = true;
        Blackboard.JumpQueued       = false;
        Blackboard.CoyoteTimer      = 0f;
        Blackboard.LandingTimerActive = true;

        Debug.Log($"[StateJump] Liftoff | type:{Blackboard.CurrentJumpType} " +
                  $"vel:{Blackboard.VerticalVelocity:F2}");
    }

    // ── Timers ────────────────────────────────────────────────────────────

    private void HandleCoyoteTime()
    {
        if (Blackboard.IsGrounded)
            Blackboard.CoyoteTimer = Blackboard.CoyoteTime;
        else
            Blackboard.CoyoteTimer -= Time.deltaTime;
    }

    private void HandleJumpTimeout()
    {
        if (!Blackboard.JumpQueued) return;
        if (Time.time - Blackboard.JumpQueuedTime >= Blackboard.JumpEventTimeout)
        {
            Debug.LogWarning("[StateJump] Jump event timed out — launching without event.");
            OnJumpLiftoff();
        }
    }

    private void HandleLandingTimer()
    {
        if (!Blackboard.LandingTimerActive || Blackboard.IsLanding) return;

        Blackboard.LandingTriggerTimer -= Time.deltaTime;
        if (Blackboard.LandingTriggerTimer > 0f) return;

        Blackboard.IsLanding          = true;
        Blackboard.LandingTimerActive = false;

        Blackboard.Animator.SetBool(HashIsFalling, false);
        Blackboard.Animator.SetBool(HashIsLanding, true);

        Debug.Log($"[StateJump] Landing anticipation triggered | " +
                  $"~{Blackboard.LandingAnticipationTime:F3}s to contact.");
    }

    // ── Physics ───────────────────────────────────────────────────────────

    private void ApplyGravity()
    {
        if (!Blackboard.IsJumping && Blackboard.IsGrounded) return;

        float g = Blackboard.VerticalVelocity < 0f
            ? Blackboard.FallGravity
            : Blackboard.RiseGravity;

        Blackboard.VerticalVelocity -= g * Time.deltaTime;

        if (Blackboard.VerticalVelocity < 0f
            && !Blackboard.IsLanding
            && !Blackboard.Animator.GetBool(HashIsFalling))
        {
            Blackboard.Animator.SetBool(HashIsFalling, true);
            Debug.Log($"[StateJump] Apex — falling | vel:{Blackboard.VerticalVelocity:F2}");
        }
    }

    private void ApplyHorizontalArc()
    {
        if (!Blackboard.IsJumping) return;

        if (Blackboard.JumpApproachRemaining > 0f)
        {
            float step = Blackboard.JumpApproachVelocity * Time.deltaTime;
            if (Mathf.Abs(step) > Blackboard.JumpApproachRemaining)
                step = Blackboard.JumpApproachRemaining * Mathf.Sign(Blackboard.JumpApproachVelocity);

            Blackboard.CurrentDistance -= step;
            Blackboard.CurrentDistance  = Mathf.Clamp(
                Blackboard.CurrentDistance,
                Blackboard.MinDistance,
                Blackboard.MaxDistance);
            Blackboard.JumpApproachRemaining -= Mathf.Abs(step);
        }

        if (Blackboard.JumpOrbitRemaining > 0f)
        {
            float step = Blackboard.JumpOrbitVelocity * Time.deltaTime;
            if (Mathf.Abs(step) > Blackboard.JumpOrbitRemaining)
                step = Blackboard.JumpOrbitRemaining * Mathf.Sign(Blackboard.JumpOrbitVelocity);

            Blackboard.CurrentOrbitAngle  += step;
            Blackboard.JumpOrbitRemaining -= Mathf.Abs(step);
        }
    }

    private void ApplyPosition()
    {
        float rad      = Blackboard.CurrentOrbitAngle * Mathf.Deg2Rad;
        Vector3 orbitPos = Blackboard.Target.position
                         + new Vector3(
                               Mathf.Sin(rad) * Blackboard.CurrentDistance,
                               0f,
                               Mathf.Cos(rad) * Blackboard.CurrentDistance);

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

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ComputeHorizontalVelocities(JumpType type, float totalAirTime)
    {
        switch (type)
        {
            case JumpType.Forward:
                Blackboard.JumpApproachVelocity  =  Blackboard.ForwardBackJumpDistance / totalAirTime;
                Blackboard.JumpApproachRemaining =  Blackboard.ForwardBackJumpDistance;
                Blackboard.JumpOrbitVelocity     =  0f;
                Blackboard.JumpOrbitRemaining    =  0f;
                break;
            case JumpType.Backward:
                Blackboard.JumpApproachVelocity  = -(Blackboard.ForwardBackJumpDistance / totalAirTime);
                Blackboard.JumpApproachRemaining =   Blackboard.ForwardBackJumpDistance;
                Blackboard.JumpOrbitVelocity     =   0f;
                Blackboard.JumpOrbitRemaining    =   0f;
                break;
            case JumpType.Left:
                Blackboard.JumpOrbitVelocity     =  Blackboard.SideJumpOrbitDegrees / totalAirTime;
                Blackboard.JumpOrbitRemaining    =  Blackboard.SideJumpOrbitDegrees;
                Blackboard.JumpApproachVelocity  =  0f;
                Blackboard.JumpApproachRemaining =  0f;
                break;
            case JumpType.Right:
                Blackboard.JumpOrbitVelocity     = -(Blackboard.SideJumpOrbitDegrees / totalAirTime);
                Blackboard.JumpOrbitRemaining    =   Blackboard.SideJumpOrbitDegrees;
                Blackboard.JumpApproachVelocity  =  0f;
                Blackboard.JumpApproachRemaining =  0f;
                break;
            default:
                Blackboard.JumpApproachVelocity  = 0f;
                Blackboard.JumpApproachRemaining = 0f;
                Blackboard.JumpOrbitVelocity     = 0f;
                Blackboard.JumpOrbitRemaining    = 0f;
                break;
        }
    }

    private JumpType ClassifyJump(Vector2 direction)
    {
        if (direction.magnitude <= Blackboard.InputThreshold) return JumpType.Neutral;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y > 0f ? JumpType.Forward : JumpType.Backward;
        return direction.x > 0f ? JumpType.Right : JumpType.Left;
    }

    private float GetActiveHeight(JumpType type)
    {
        switch (type)
        {
            case JumpType.Forward:  return Blackboard.JumpHeight * Blackboard.ForwardJumpHeightMult;
            case JumpType.Backward: return Blackboard.JumpHeight * Blackboard.BackwardJumpHeightMult;
            case JumpType.Left:
            case JumpType.Right:    return Blackboard.JumpHeight * Blackboard.SideJumpHeightMult;
            default:                return Blackboard.JumpHeight;
        }
    }

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
}
