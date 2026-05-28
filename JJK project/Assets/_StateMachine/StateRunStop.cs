using UnityEngine;

/// <summary>
/// Plays the directional run-to-stop animation then returns to Locomotion.
/// Entered from StateLocomotion when the player releases the stick while moving.
///
/// TO REMOVE THIS STATE:
///   1. Delete this file
///   2. Remove StateId.RunStop from the enum in PlayerBlackboard.cs
///   3. Remove WantsToRunStop from PlayerBlackboard.cs
///   4. Remove the WantsToRunStop set from StateLocomotion.OnUpdate()
///   5. Remove the registration and transition row from PlayerController.cs
/// </summary>
public class StateRunStop : PlayerStateBase
{
    // ── Animator hashes ───────────────────────────────────────────────────
    private static readonly int HashRunStop  = Animator.StringToHash("IsRunStopping");
    private static readonly int HashStopX    = Animator.StringToHash("StopX");
    private static readonly int HashStopY    = Animator.StringToHash("StopY");
    private static readonly int HashGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashMoveX    = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY    = Animator.StringToHash("MoveY");

    // ── Blend directions (same set as locomotion) ─────────────────────────
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

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void OnEnter()
    {
        Blackboard.WantsToLocomotion = false;
        Blackboard.VerticalVelocity  = -2f;

        // Tell the animator which directional stop clip to play.
        // RunStopDirection was recorded by StateLocomotion on the frame
        // the stick was released, so it matches the last movement direction.
        Blackboard.Animator.SetFloat(HashStopX,   Blackboard.RunStopDirection.x);
        Blackboard.Animator.SetFloat(HashStopY,   Blackboard.RunStopDirection.y);
        Blackboard.Animator.SetBool(HashRunStop,  true);
        Blackboard.Animator.SetBool(HashGrounded, true);

        Debug.Log($"[StateRunStop] Entered | direction:{Blackboard.RunStopDirection}");
    }

    public override void OnUpdate()
    {
        // Jump can interrupt a stop — feels responsive and natural
        if (Blackboard.JumpQueued)
        {
            Blackboard.WantsToJump = true;
            return;
        }

        // Keep grounded while the stop clip plays
        ApplyGroundingForce();

        // Smoothly drain the blend tree back to zero so locomotion
        // blends in cleanly when we return
        Blackboard.SmoothedBlendInput = Vector2.MoveTowards(
            Blackboard.SmoothedBlendInput,
            Vector2.zero,
            Blackboard.AnimBlendStopSpeed * Time.deltaTime);

        Blackboard.Animator.SetFloat(HashMoveX, Blackboard.SmoothedBlendInput.x);
        Blackboard.Animator.SetFloat(HashMoveY, Blackboard.SmoothedBlendInput.y);

        // Auto-complete: if the blend has fully returned to zero the stop
        // animation has effectively finished — return to locomotion.
        // This fires even without an animation event so it always works.
        if (Blackboard.SmoothedBlendInput.magnitude < 0.02f)
        {
            Debug.Log("[StateRunStop] Blend reached zero — auto-completing.");
            Blackboard.WantsToLocomotion = true;
        }
    }

    public override void OnExit()
    {
        Blackboard.Animator.SetBool(HashRunStop, false);
        Debug.Log("[StateRunStop] Exited.");
    }

    // ── Called by animation event on each stop clip at the settled frame ──
    // Place an OnRunStopComplete event on each directional stop clip
    // at the frame the character fully settles. This gives the crispest
    // transition. If no event is placed, the auto-complete above handles it.
    public void OnRunStopComplete()
    {
        Debug.Log("[StateRunStop] Animation event received — returning to Locomotion.");
        Blackboard.WantsToLocomotion = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyGroundingForce()
    {
        Blackboard.LastCollisionFlags = Blackboard.CharacterController.Move(
            Vector3.up * (-2f * Time.deltaTime));
    }
}
