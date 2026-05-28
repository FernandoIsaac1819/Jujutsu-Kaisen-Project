using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerLocomotion : MonoBehaviour
{
    // Target
    [Header("Target")]
    public Transform target;

    // Orbit
    [Header("Orbit Settings")]
    public float orbitSpeed    = 120f;
    public float approachSpeed = 6f;

    [Header("Distance Constraints")]
    public float minDistance = 2f;
    public float maxDistance = 12f;

    // Input
    [Header("Input")]
    public float inputThreshold = 0.2f;

    // Jump General
    [Header("Jump - General")]
    public float jumpHeight    = 2.5f;
    public float coyoteTime    = 0.15f;
    public float jumpCooldown  = 0.4f;

    [Tooltip("If OnJumpLiftoff animation event does not fire within this time, jump launches automatically.")]
    public float jumpEventTimeout = 0.4f;

    // Jump Directional
    [Header("Jump - Directional Travel")]
    public float forwardBackJumpDistance = 5f;
    public float sideJumpOrbitDegrees    = 40f;

    [Range(0.3f, 1.5f)] public float forwardJumpHeightMult  = 0.75f;
    [Range(0.3f, 1.5f)] public float backwardJumpHeightMult = 1.2f;
    [Range(0.3f, 1.5f)] public float sideJumpHeightMult     = 0.9f;

    // Landing
    [Header("Landing")]
    [Tooltip("How many seconds before predicted ground contact to start the landing animation. " +
             "Tune this so the landing pose hits exactly when the feet touch the ground. " +
             "Roughly equal to the time from the first frame of your JumpLand clip to the impact frame.")]
    public float landingAnticipationTime = 0.12f;

    // Gravity
    [Header("Custom Gravity")]
    public float riseGravity = 25f;
    public float fallGravity = 45f;

    // Ground Check
    [Header("Ground Check")]
    public Transform groundCheckOrigin;
    public float     groundCheckRadius = 0.25f;
    public LayerMask groundLayers;

    // Animator
    [Header("Animator")]
    public Animator animator;

    [Tooltip("How fast the blend value moves toward a new direction or idle target. Lower = weightier feel.")]
    public float animBlendStartSpeed = 5f;

    [Tooltip("How fast the blend value returns to zero when stopping. Higher = snappier stop.")]
    public float animBlendStopSpeed = 14f;

    // Animator hashes
    private static readonly int HashMoveX     = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY     = Animator.StringToHash("MoveY");
    private static readonly int HashJumpX     = Animator.StringToHash("JumpX");
    private static readonly int HashJumpY     = Animator.StringToHash("JumpY");
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int HashIsFalling = Animator.StringToHash("IsFalling");
    private static readonly int HashIsLanding = Animator.StringToHash("IsLanding");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");

    // Blend directions
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

    private static readonly Vector2[] JumpBlendDirs =
    {
        new Vector2( 0f,  1f),
        new Vector2( 0f, -1f),
        new Vector2(-1f,  0f),
        new Vector2( 1f,  0f),
    };

    private enum JumpType { Neutral, Forward, Backward, Left, Right }

    // Internal state
    private CharacterController _cc;
    private CollisionFlags      _lastCollisionFlags;
    private float               _currentOrbitAngle;
    private float               _currentDistance;
    private float               _verticalVelocity;
    private bool                _isJumping;
    private bool                _isLanding;
    private float               _coyoteTimer;
    private float               _jumpCooldownTimer;
    private bool                _isGrounded;
    private bool                _wasGrounded;
    private Vector2             _jumpDirection;
    private Vector2             _currentInput;
    private Vector2             _smoothedBlendInput;  // smoothed value fed to the blend tree

    // Jump intent
    private bool     _jumpQueued;
    private float    _jumpQueuedTime;
    private JumpType _currentJumpType;
    private float    _jumpApproachVelocity;
    private float    _jumpApproachRemaining;
    private float    _jumpOrbitVelocity;
    private float    _jumpOrbitRemaining;

    // Landing timer — set at liftoff, counts down to anticipation trigger
    // This is the key: we know the full arc duration at liftoff, so we
    // schedule the landing animation trigger precisely rather than polling.
    private float _landingTriggerTimer;   // counts down from (airTime - anticipationTime) to 0
    private bool  _landingTimerActive;

    public bool IsMoving   { get; private set; }
    public bool IsGrounded => _isGrounded;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        Debug.Log("[PlayerLocomotion] Awake.");
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[PlayerLocomotion] No target assigned.");
            return;
        }

        Vector3 offset     = transform.position - target.position;
        _currentDistance   = Mathf.Clamp(new Vector2(offset.x, offset.z).magnitude, minDistance, maxDistance);
        _currentOrbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

        _isGrounded  = CheckGrounded();
        _wasGrounded = _isGrounded;
        SnapToOrbit();

        Debug.Log($"[PlayerLocomotion] Start - angle:{_currentOrbitAngle:F1} dist:{_currentDistance:F2}");
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.onJumpDashPressed += OnJumpPressed;
        else
            Debug.LogWarning("[PlayerLocomotion] InputManager not ready on OnEnable.");
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.onJumpDashPressed -= OnJumpPressed;
    }

    private void Update()
    {
        if (target == null || InputManager.Instance == null) return;

        _isGrounded = CheckGrounded();

        Vector2 rawInput = InputManager.Instance.GetMoveInput();
        IsMoving      = rawInput.magnitude > inputThreshold;
        _currentInput = (IsMoving && _isGrounded) ? rawInput : Vector2.zero;

        HandleCoyoteTime();
        HandleJumpCooldown();
        HandleJumpTimeout();
        HandleLandingTimer();   // counts down and fires IsLanding at the right moment

        ApplyMovement();        // cc.Move runs here
        HandleLanding();        // reads CollisionFlags after cc.Move

        FaceTarget();
        DriveAnimator(_currentInput);

        _wasGrounded = _isGrounded;
    }

    private bool CheckGrounded()
    {
        if (groundCheckOrigin == null) return false;
        return Physics.CheckSphere(
            groundCheckOrigin.position,
            groundCheckRadius,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckOrigin == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheckOrigin.position, groundCheckRadius);
    }

    private void OnJumpPressed(object sender, System.EventArgs e)
    {
        if (e is not InputManager.DashJumpEventArgs args)
        {
            Debug.LogWarning("[PlayerLocomotion] Unexpected EventArgs type.");
            return;
        }

        if (!args.IsDoubleTap)
        {
            Debug.Log($"[PlayerLocomotion] Single tap - ignoring. Dir:{args.Direction}");
            return;
        }

        if (!CanJump())
        {
            Debug.Log($"[PlayerLocomotion] Jump blocked | grounded:{_isGrounded} coyote:{_coyoteTimer:F3} cooldown:{_jumpCooldownTimer:F3} jumping:{_isJumping}");
            return;
        }

        _jumpDirection   = args.Direction.magnitude > inputThreshold
            ? SnapToNearest(args.Direction.normalized, JumpBlendDirs)
            : Vector2.zero;

        _currentJumpType = ClassifyJump(args.Direction);

        float activeHeight = GetActiveHeight(_currentJumpType);
        float launchVel    = Mathf.Sqrt(2f * riseGravity * activeHeight);
        float totalAirTime = (launchVel / riseGravity) + Mathf.Sqrt(2f * activeHeight / fallGravity);

        // Schedule the landing animation trigger.
        // landingAnticipationTime seconds before landing we set IsLanding = true.
        // The timer starts at liftoff (OnJumpLiftoff), not at queue time,
        // because air time doesn't begin until the feet leave the ground.
        // We store the air time here and start the actual countdown in OnJumpLiftoff.
        _landingTriggerTimer = totalAirTime - landingAnticipationTime;
        _landingTimerActive  = false;   // activated in OnJumpLiftoff

        Debug.Log($"[PlayerLocomotion] Jump queued | type:{_currentJumpType} height:{activeHeight:F2} " +
                  $"airTime:{totalAirTime:F3}s | landing trigger in:{_landingTriggerTimer:F3}s after liftoff");

        switch (_currentJumpType)
        {
            case JumpType.Forward:
                _jumpApproachVelocity  =  forwardBackJumpDistance / totalAirTime;
                _jumpApproachRemaining =  forwardBackJumpDistance;
                _jumpOrbitVelocity     =  0f;
                _jumpOrbitRemaining    =  0f;
                break;
            case JumpType.Backward:
                _jumpApproachVelocity  = -(forwardBackJumpDistance / totalAirTime);
                _jumpApproachRemaining =   forwardBackJumpDistance;
                _jumpOrbitVelocity     =   0f;
                _jumpOrbitRemaining    =   0f;
                break;
            case JumpType.Left:
                _jumpOrbitVelocity     =  sideJumpOrbitDegrees / totalAirTime;
                _jumpOrbitRemaining    =  sideJumpOrbitDegrees;
                _jumpApproachVelocity  =  0f;
                _jumpApproachRemaining =  0f;
                break;
            case JumpType.Right:
                _jumpOrbitVelocity     = -(sideJumpOrbitDegrees / totalAirTime);
                _jumpOrbitRemaining    =   sideJumpOrbitDegrees;
                _jumpApproachVelocity  =  0f;
                _jumpApproachRemaining =  0f;
                break;
            default:
                _jumpApproachVelocity  = 0f;
                _jumpApproachRemaining = 0f;
                _jumpOrbitVelocity     = 0f;
                _jumpOrbitRemaining    = 0f;
                break;
        }

        _jumpQueued     = true;
        _jumpQueuedTime = Time.time;
        _isLanding      = false;

        animator.SetFloat(HashJumpX,    _jumpDirection.x);
        animator.SetFloat(HashJumpY,    _jumpDirection.y);
        animator.SetBool(HashIsJumping, true);
        animator.SetBool(HashIsFalling, false);
        animator.SetBool(HashIsLanding, false);
        animator.SetBool(HashGrounded,  false);
    }

    // Animation event on JumpRise clip at the liftoff frame
    public void OnJumpLiftoff()
    {
        if (!_jumpQueued)
        {
            Debug.Log("[PlayerLocomotion] OnJumpLiftoff fired but nothing queued - ignoring.");
            return;
        }

        float activeHeight    = GetActiveHeight(_currentJumpType);
        _verticalVelocity     = Mathf.Sqrt(2f * riseGravity * activeHeight);
        _isJumping            = true;
        _jumpQueued           = false;
        _coyoteTimer          = 0f;

        // Start the countdown to the landing anticipation trigger now that
        // the feet have actually left the ground
        _landingTimerActive = true;

        Debug.Log($"[PlayerLocomotion] LIFTOFF | type:{_currentJumpType} vel:{_verticalVelocity:F2} " +
                  $"| landing anim triggers in:{_landingTriggerTimer:F3}s");
    }

    // Animation event on JumpLand clip at the impact frame (for VFX/SFX)
    public void OnJumpImpact()
    {
        Debug.Log("[PlayerLocomotion] IMPACT event - trigger VFX/SFX here.");
    }

    // Counts down from liftoff. When it hits zero, start the landing animation.
    // Because we calculated the timer from the known arc duration at jump time,
    // this fires exactly landingAnticipationTime seconds before touchdown — every time,
    // regardless of framerate or ground detection latency.
    private void HandleLandingTimer()
    {
        if (!_landingTimerActive || _isLanding) return;

        _landingTriggerTimer -= Time.deltaTime;

        if (_landingTriggerTimer <= 0f)
        {
            _isLanding          = true;
            _landingTimerActive = false;

            animator.SetBool(HashIsFalling, false);
            animator.SetBool(HashIsLanding, true);

            Debug.Log($"[PlayerLocomotion] Landing anticipation triggered | " +
                      $"predicted contact in ~{landingAnticipationTime:F3}s");
        }
    }

    private void HandleJumpTimeout()
    {
        if (!_jumpQueued) return;
        if (Time.time - _jumpQueuedTime >= jumpEventTimeout)
        {
            Debug.LogWarning("[PlayerLocomotion] Jump event timed out - launching without event.");
            OnJumpLiftoff();
        }
    }

    // Handles physics state cleanup on the frame the CC contacts the ground
    private void HandleLanding()
    {
        if (!_isJumping) return;

        bool hitGroundThisFrame = (_lastCollisionFlags & CollisionFlags.Below) != 0;
        if (!hitGroundThisFrame) return;

        Debug.Log($"[PlayerLocomotion] Landed | type:{_currentJumpType} impactVel:{_verticalVelocity:F2}");

        _isJumping             = false;
        _isLanding             = false;
        _landingTimerActive    = false;
        _currentJumpType       = JumpType.Neutral;
        _jumpApproachVelocity  = 0f;
        _jumpApproachRemaining = 0f;
        _jumpOrbitVelocity     = 0f;
        _jumpOrbitRemaining    = 0f;
        _jumpCooldownTimer     = jumpCooldown;
        _verticalVelocity      = -2f;

        // IsLanding was already set by the timer so the clip is already
        // playing — we just confirm the final state
        animator.SetBool(HashIsJumping, false);
        animator.SetBool(HashIsFalling, false);
        animator.SetBool(HashIsLanding, true);
        animator.SetBool(HashGrounded,  true);
    }

    private void HandleCoyoteTime()
    {
        if (_isGrounded) _coyoteTimer = coyoteTime;
        else             _coyoteTimer -= Time.deltaTime;
    }

    private void HandleJumpCooldown()
    {
        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.deltaTime;
    }

    private void ApplyMovement()
    {
        if (_isJumping || !_isGrounded)
        {
            float g = _verticalVelocity < 0f ? fallGravity : riseGravity;
            _verticalVelocity -= g * Time.deltaTime;

            // Only set IsFalling if we haven't already moved into landing
            if (_verticalVelocity < 0f && !_isLanding && !animator.GetBool(HashIsFalling))
            {
                animator.SetBool(HashIsFalling, true);
                Debug.Log($"[PlayerLocomotion] Apex - now falling | vel:{_verticalVelocity:F2}");
            }
        }
        else
        {
            _verticalVelocity = -2f;
        }

        if (_isGrounded && !_isJumping)
        {
            if (Mathf.Abs(_currentInput.x) >= 0.01f)
                _currentOrbitAngle -= _currentInput.x * orbitSpeed * Time.deltaTime;

            if (Mathf.Abs(_currentInput.y) >= 0.01f)
            {
                _currentDistance -= _currentInput.y * approachSpeed * Time.deltaTime;
                _currentDistance  = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
            }
        }

        if (_isJumping)
        {
            if (_jumpApproachRemaining > 0f)
            {
                float step = _jumpApproachVelocity * Time.deltaTime;
                if (Mathf.Abs(step) > _jumpApproachRemaining)
                    step = _jumpApproachRemaining * Mathf.Sign(_jumpApproachVelocity);

                _currentDistance       -= step;
                _currentDistance        = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
                _jumpApproachRemaining -= Mathf.Abs(step);
            }

            if (_jumpOrbitRemaining > 0f)
            {
                float step = _jumpOrbitVelocity * Time.deltaTime;
                if (Mathf.Abs(step) > _jumpOrbitRemaining)
                    step = _jumpOrbitRemaining * Mathf.Sign(_jumpOrbitVelocity);

                _currentOrbitAngle  += step;
                _jumpOrbitRemaining -= Mathf.Abs(step);
            }
        }

        Vector3 orbitPos        = OrbitPosition(_currentOrbitAngle, _currentDistance);
        Vector3 horizontalDelta = new Vector3(
            orbitPos.x - transform.position.x,
            0f,
            orbitPos.z - transform.position.z
        );

        Vector3 motion      = horizontalDelta + Vector3.up * (_verticalVelocity * Time.deltaTime);
        _lastCollisionFlags = _cc.Move(motion);
    }

    private void FaceTarget()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(toTarget);
    }

    private bool CanJump() => (_isGrounded || _coyoteTimer > 0f)
                           && _jumpCooldownTimer <= 0f
                           && !_isJumping
                           && !_jumpQueued;

    private void DriveAnimator(Vector2 input)
    {
        if (animator == null) return;

        // The snap target — the correct final blend position for this input.
        // Zero when no input, nearest cardinal/diagonal when moving.
        Vector2 snapTarget = input.magnitude > inputThreshold
            ? SnapToNearest(input.normalized, LocoBlendDirs)
            : Vector2.zero;

        // Choose the blend speed based on whether we are stopping or moving.
        // Stopping uses a fast speed so it feels responsive and controlled.
        // Starting or changing direction uses a slower speed so the character
        // feels like it has weight rather than teleporting between poses.
        bool isStopping = snapTarget == Vector2.zero;
        float speed     = isStopping ? animBlendStopSpeed : animBlendStartSpeed;

        // MoveTowards moves the smoothed vector toward the snap target at a
        // fixed units-per-second rate. Unlike Lerp it reaches the target exactly
        // rather than approaching asymptotically, so the final pose is always clean.
        _smoothedBlendInput = Vector2.MoveTowards(
            _smoothedBlendInput,
            snapTarget,
            speed * Time.deltaTime
        );

        // Feed the smoothed value directly — no additional animator damp time
        // needed because we are already smoothing in code.
        animator.SetFloat(HashMoveX,   _smoothedBlendInput.x);
        animator.SetFloat(HashMoveY,   _smoothedBlendInput.y);
        animator.SetBool(HashGrounded, _isGrounded);
    }

    private JumpType ClassifyJump(Vector2 direction)
    {
        if (direction.magnitude <= inputThreshold) return JumpType.Neutral;
        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y > 0f ? JumpType.Forward : JumpType.Backward;
        else
            return direction.x > 0f ? JumpType.Right : JumpType.Left;
    }

    private float GetActiveHeight(JumpType type)
    {
        switch (type)
        {
            case JumpType.Forward:  return jumpHeight * forwardJumpHeightMult;
            case JumpType.Backward: return jumpHeight * backwardJumpHeightMult;
            case JumpType.Left:
            case JumpType.Right:    return jumpHeight * sideJumpHeightMult;
            default:                return jumpHeight;
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

    private Vector3 OrbitPosition(float angleDeg, float distance)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return target.position + new Vector3(Mathf.Sin(rad) * distance, 0f, Mathf.Cos(rad) * distance);
    }

    private void SnapToOrbit()
    {
        Vector3 snapPos = OrbitPosition(_currentOrbitAngle, _currentDistance);
        snapPos.y = transform.position.y;
        _cc.enabled = false;
        transform.position = snapPos;
        _cc.enabled = true;
        FaceTarget();
    }
}
