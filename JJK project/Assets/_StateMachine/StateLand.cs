using UnityEngine;

/// <summary>
/// Handles the landing sequence: physics cleanup, landing clip hold, recovery.
/// Sets Blackboard.WantsToLocomotion when the landing clip finishes.
/// PlayerController reads that flag and drives the transition.
/// </summary>
public class StateLand : PlayerStateBase
{
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int HashIsFalling = Animator.StringToHash("IsFalling");
    private static readonly int HashIsLanding = Animator.StringToHash("IsLanding");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void OnEnter()
    {
        Blackboard.IsJumping             = false;
        Blackboard.IsLanding             = false;
        Blackboard.LandingTimerActive    = false;
        Blackboard.JumpApproachVelocity  = 0f;
        Blackboard.JumpApproachRemaining = 0f;
        Blackboard.JumpOrbitVelocity     = 0f;
        Blackboard.JumpOrbitRemaining    = 0f;
        Blackboard.JumpCooldownTimer     = Blackboard.JumpCooldown;
        Blackboard.VerticalVelocity      = -2f;
        Blackboard.CurrentJumpType       = JumpType.Neutral;
        Blackboard.WantsToLocomotion     = false;

        Blackboard.Animator.SetBool(HashIsJumping, false);
        Blackboard.Animator.SetBool(HashIsFalling, false);
        Blackboard.Animator.SetBool(HashIsLanding, true);
        Blackboard.Animator.SetBool(HashGrounded,  true);

        Debug.Log("[StateLand] Entered.");
    }

    public override void OnUpdate()
    {
        // Pin to ground during landing clip
        Blackboard.LastCollisionFlags = Blackboard.CharacterController.Move(
            Vector3.up * (-2f * Time.deltaTime));

        if (Blackboard.JumpCooldownTimer > 0f)
            Blackboard.JumpCooldownTimer -= Time.deltaTime;

        // JumpLandSMB.OnStateExit clears IsLanding when the clip finishes.
        // Once it's clear, signal that we want to return to locomotion.
        if (!Blackboard.Animator.GetBool(HashIsLanding))
            Blackboard.WantsToLocomotion = true;
    }

    public override void OnExit()
    {
        Blackboard.WantsToLocomotion = false;
        Debug.Log("[StateLand] Exited.");
    }

    // ── Animation event — VFX/SFX only ───────────────────────────────────
    public void OnJumpImpact()
    {
        Debug.Log("[StateLand] Impact event — trigger VFX/SFX here.");
    }
}
