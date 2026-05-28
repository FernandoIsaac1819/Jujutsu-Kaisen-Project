using UnityEngine;

/// <summary>
/// Shared data container for all player states.
/// No logic lives here — states read and write, PlayerController owns and initialises.
///
/// TRANSITION FLAGS: states set these to signal intent.
/// PlayerController reads them each frame and drives the actual transition.
/// This means states never need to know about each other.
/// To add a new transition: add a flag here, set it in a state, read it in PlayerController.
/// </summary>
public class PlayerBlackboard
{
    // ── Component references ──────────────────────────────────────────────
    public CharacterController CharacterController;
    public Animator            Animator;
    public Transform           Target;
    public Transform           GroundCheckOrigin;

    // ── Locomotion config ─────────────────────────────────────────────────
    public float OrbitSpeed;
    public float ApproachSpeed;
    public float MinDistance;
    public float MaxDistance;
    public float InputThreshold;
    public float AnimBlendStartSpeed;
    public float AnimBlendStopSpeed;

    // ── Jump config ───────────────────────────────────────────────────────
    public float JumpHeight;
    public float CoyoteTime;
    public float JumpCooldown;
    public float JumpEventTimeout;
    public float ForwardBackJumpDistance;
    public float SideJumpOrbitDegrees;
    public float ForwardJumpHeightMult;
    public float BackwardJumpHeightMult;
    public float SideJumpHeightMult;

    // ── Gravity config ────────────────────────────────────────────────────
    public float RiseGravity;
    public float FallGravity;

    // ── Landing config ────────────────────────────────────────────────────
    public float LandingAnticipationTime;

    // ── Ground check config ───────────────────────────────────────────────
    public float     GroundCheckRadius;
    public LayerMask GroundLayers;

    // ── Runtime locomotion state ──────────────────────────────────────────
    public float          CurrentOrbitAngle;
    public float          CurrentDistance;
    public float          VerticalVelocity;
    public bool           IsGrounded;
    public bool           WasGrounded;
    public Vector2        CurrentInput;
    public Vector2        SmoothedBlendInput;
    public bool           IsMoving;
    public CollisionFlags LastCollisionFlags;

    // ── Runtime jump state ────────────────────────────────────────────────
    public bool     IsJumping;
    public bool     IsLanding;
    public bool     JumpQueued;
    public float    JumpQueuedTime;
    public JumpType CurrentJumpType;
    public Vector2  JumpDirection;
    public float    JumpApproachVelocity;
    public float    JumpApproachRemaining;
    public float    JumpOrbitVelocity;
    public float    JumpOrbitRemaining;
    public float    LandingTriggerTimer;
    public bool     LandingTimerActive;

    // ── Runtime land state ────────────────────────────────────────────────
    public bool    IsRunStopping;
    public Vector2 RunStopDirection;

    // ── Timers ────────────────────────────────────────────────────────────
    public float CoyoteTimer;
    public float JumpCooldownTimer;

    // ── Transition flags ──────────────────────────────────────────────────
    // States set these to signal a desired transition.
    // PlayerController reads and clears them every frame.
    // Never set more than one at a time — PlayerController
    // processes them in priority order defined in its transition table.
    public bool WantsToJump;        // set by StateLocomotion / StateJump when jump is queued
    public bool WantsToLand;        // set by StateJump when CC reports CollisionFlags.Below
    public bool WantsToLocomotion;  // set by StateLand / StateRunStop when clip finishes
    public bool WantsToRunStop;     // set by StateLocomotion when player releases stick while moving
}

public enum JumpType { Neutral, Forward, Backward, Left, Right }
public enum StateId  { None, Locomotion, Jump, Land, RunStop }
