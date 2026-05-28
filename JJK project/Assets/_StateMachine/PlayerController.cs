using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the CharacterController, blackboard, and state machine.
///
/// HOW TO ADD A NEW STATE:
///   1. Create a new class that inherits PlayerStateBase (e.g. StateDash.cs)
///   2. Add a new value to the StateId enum in PlayerBlackboard.cs (e.g. StateId.Dash)
///   3. Add a new transition flag to PlayerBlackboard (e.g. WantsToDash)
///   4. Set that flag from whichever state should trigger the transition
///   5. Register the state in Awake() below
///   6. Add a row to EvaluateTransitions() below
///   That's it — no other state files need to change.
///
/// HOW TO REMOVE A STATE:
///   1. Delete the state file
///   2. Remove its StateId value
///   3. Remove its flag from PlayerBlackboard
///   4. Remove its row from EvaluateTransitions()
///   5. Remove its registration from Awake()
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Target")]
    [SerializeField] private Transform _target;

    [Header("Orbit")]
    [SerializeField] private float _orbitSpeed    = 120f;
    [SerializeField] private float _approachSpeed = 6f;
    [SerializeField] private float _minDistance   = 2f;
    [SerializeField] private float _maxDistance   = 12f;

    [Header("Input")]
    [SerializeField] private float _inputThreshold = 0.2f;

    [Header("Animator")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float    _animBlendStartSpeed = 5f;
    [SerializeField] private float    _animBlendStopSpeed  = 14f;

    [Header("Jump - General")]
    [SerializeField] private float _jumpHeight       = 2.5f;
    [SerializeField] private float _coyoteTime       = 0.15f;
    [SerializeField] private float _jumpCooldown     = 0.4f;
    [SerializeField] private float _jumpEventTimeout = 0.4f;

    [Header("Jump - Directional")]
    [SerializeField] private float _forwardBackJumpDistance = 5f;
    [SerializeField] private float _sideJumpOrbitDegrees    = 40f;
    [SerializeField] [Range(0.3f, 1.5f)] private float _forwardJumpHeightMult  = 0.75f;
    [SerializeField] [Range(0.3f, 1.5f)] private float _backwardJumpHeightMult = 1.2f;
    [SerializeField] [Range(0.3f, 1.5f)] private float _sideJumpHeightMult     = 0.9f;

    [Header("Gravity")]
    [SerializeField] private float _riseGravity = 25f;
    [SerializeField] private float _fallGravity = 45f;

    [Header("Landing")]
    [SerializeField] private float _landingAnticipationTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheckOrigin;
    [SerializeField] private float     _groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask _groundLayers;

    // ── Private ───────────────────────────────────────────────────────────

    private PlayerBlackboard                     _blackboard;
    private Dictionary<StateId, PlayerStateBase> _states;
    private PlayerStateBase                      _activeState;
    private StateId                              _activeStateId;

    // Typed references kept for animation event forwarding
    private StateLocomotion _stateLocomotion;
    private StateJump       _stateJump;
    private StateLand       _stateLand;
    private StateRunStop    _stateRunStop;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _blackboard = BuildBlackboard();

        // ── Register states here ──────────────────────────────────────────
        // To add a new state: instantiate it, add it to the dictionary.
        // The state's Init() is called automatically in the loop below.
        _stateLocomotion = new StateLocomotion();
        _stateJump       = new StateJump();
        _stateLand       = new StateLand();
        _stateRunStop    = new StateRunStop();

        _states = new Dictionary<StateId, PlayerStateBase>
        {
            { StateId.Locomotion, _stateLocomotion },
            { StateId.Jump,       _stateJump       },
            { StateId.Land,       _stateLand       },
            { StateId.RunStop,    _stateRunStop    },
        };

        foreach (PlayerStateBase state in _states.Values)
            state.Init(_blackboard);

        TransitionTo(StateId.Locomotion);
    }

    private void Start()
    {
        // Subscribe in Start — all Awake() calls are guaranteed complete by now
        if (InputManager.Instance != null)
        {
            InputManager.Instance.onJumpDashPressed += OnJumpDashPressed;
            Debug.Log("[PlayerController] Input subscribed.");
        }
        else
        {
            Debug.LogError("[PlayerController] InputManager.Instance is null in Start.");
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.onJumpDashPressed -= OnJumpDashPressed;
    }

    private void Update()
    {
        if (_target == null || InputManager.Instance == null) return;

        UpdateBlackboard();
        _activeState.OnUpdate();
        EvaluateTransitions();
        _blackboard.WasGrounded = _blackboard.IsGrounded;
    }

    // ── Blackboard update ─────────────────────────────────────────────────

    private void UpdateBlackboard()
    {
        Vector2 rawInput              = InputManager.Instance.GetMoveInput();
        _blackboard.IsMoving          = rawInput.magnitude > _blackboard.InputThreshold;
        _blackboard.CurrentInput      = _blackboard.IsMoving ? rawInput : Vector2.zero;
        _blackboard.IsGrounded        = CheckGrounded();
        _blackboard.JumpCooldownTimer = Mathf.Max(0f, _blackboard.JumpCooldownTimer - Time.deltaTime);
    }

    // ── Transition table ──────────────────────────────────────────────────
    // This is the only place transitions are decided.
    // Read flags from the blackboard, transition if conditions are met,
    // then clear the flag so it doesn't fire again next frame.
    //
    // Priority is top-to-bottom — checks are ordered most urgent first.
    // To add a new transition: add an else-if block here and a flag to the blackboard.

    private void EvaluateTransitions()
    {
        if (_blackboard.WantsToJump)
        {
            _blackboard.WantsToJump = false;
            TransitionTo(StateId.Jump);
        }
        else if (_blackboard.WantsToLand)
        {
            _blackboard.WantsToLand = false;
            TransitionTo(StateId.Land);
        }
        else if (_blackboard.WantsToRunStop)
        {
            _blackboard.WantsToRunStop = false;
            TransitionTo(StateId.RunStop);
        }
        else if (_blackboard.WantsToLocomotion)
        {
            _blackboard.WantsToLocomotion = false;
            TransitionTo(StateId.Locomotion);
        }
        // ── Add new transitions here ──────────────────────────────────────
        // Example: dash state
        // else if (_blackboard.WantsToDash)
        // {
        //     _blackboard.WantsToDash = false;
        //     TransitionTo(StateId.Dash);
        // }
    }

    // ── State machine ─────────────────────────────────────────────────────

    private void TransitionTo(StateId id)
    {
        if (!_states.ContainsKey(id))
        {
            Debug.LogError($"[PlayerController] No state registered for {id}.");
            return;
        }

        _activeState?.OnExit();
        _activeStateId = id;
        _activeState   = _states[id];
        _activeState.OnEnter();
        Debug.Log($"[PlayerController] -> {id}");
    }

    // ── Input forwarding ──────────────────────────────────────────────────

    private void OnJumpDashPressed(object sender, System.EventArgs e)
    {
        // StateJump validates CanJump() internally — safe to always forward
        _stateJump.OnJumpPressed(e);
    }

    // ── Animation events ──────────────────────────────────────────────────

    /// <summary>Place on JumpRise clip at the liftoff frame.</summary>
    public void OnJumpLiftoff() => _stateJump.OnJumpLiftoff();

    /// <summary>Place on JumpLand clip at the impact frame. VFX/SFX only.</summary>
    public void OnJumpImpact() => _stateLand.OnJumpImpact();

    /// <summary>Place on each run-stop clip at the full-stop frame.</summary>
    public void OnRunStopComplete() => _stateRunStop.OnRunStopComplete();

    // ── Ground check ──────────────────────────────────────────────────────

    private bool CheckGrounded()
    {
        if (_groundCheckOrigin == null) return false;
        return Physics.CheckSphere(
            _groundCheckOrigin.position,
            _groundCheckRadius,
            _groundLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckOrigin == null) return;
        bool grounded = _blackboard != null && _blackboard.IsGrounded;
        Gizmos.color  = grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_groundCheckOrigin.position, _groundCheckRadius);
    }

    // ── Blackboard factory ────────────────────────────────────────────────

    private PlayerBlackboard BuildBlackboard()
    {
        var cc     = GetComponent<CharacterController>();
        var offset = transform.position - _target.position;

        return new PlayerBlackboard
        {
            CharacterController     = cc,
            Animator                = _animator,
            Target                  = _target,
            GroundCheckOrigin       = _groundCheckOrigin,

            OrbitSpeed              = _orbitSpeed,
            ApproachSpeed           = _approachSpeed,
            MinDistance             = _minDistance,
            MaxDistance             = _maxDistance,
            InputThreshold          = _inputThreshold,
            AnimBlendStartSpeed     = _animBlendStartSpeed,
            AnimBlendStopSpeed      = _animBlendStopSpeed,

            JumpHeight              = _jumpHeight,
            CoyoteTime              = _coyoteTime,
            JumpCooldown            = _jumpCooldown,
            JumpEventTimeout        = _jumpEventTimeout,
            ForwardBackJumpDistance = _forwardBackJumpDistance,
            SideJumpOrbitDegrees    = _sideJumpOrbitDegrees,
            ForwardJumpHeightMult   = _forwardJumpHeightMult,
            BackwardJumpHeightMult  = _backwardJumpHeightMult,
            SideJumpHeightMult      = _sideJumpHeightMult,

            RiseGravity             = _riseGravity,
            FallGravity             = _fallGravity,
            LandingAnticipationTime = _landingAnticipationTime,
            GroundCheckRadius       = _groundCheckRadius,
            GroundLayers            = _groundLayers,

            CurrentDistance   = Mathf.Clamp(
                new Vector2(offset.x, offset.z).magnitude,
                _minDistance, _maxDistance),
            CurrentOrbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg,
            VerticalVelocity  = -2f,
        };
    }
}
