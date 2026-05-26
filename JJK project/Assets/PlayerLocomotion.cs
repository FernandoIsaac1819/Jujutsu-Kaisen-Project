using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerLocomotion : MonoBehaviour
{
    // ── Target ────────────────────────────────────────────────────────────
    [Header("Target")]
    public Transform target;

    // ── Orbit ─────────────────────────────────────────────────────────────
    [Header("Orbit Settings")]
    public float orbitSpeed    = 120f;
    public float approachSpeed = 6f;

    [Header("Distance Constraints")]
    public float minDistance = 2f;
    public float maxDistance = 12f;

    // ── Input ─────────────────────────────────────────────────────────────
    [Header("Input")]
    public float inputThreshold = 0.2f;

    // ── Jump — General ────────────────────────────────────────────────────
    [Header("Jump — General")]
    [Tooltip("Height of a neutral jump in world units.")]
    public float jumpHeight = 2.5f;

    [Tooltip("How long after leaving the ground the player can still jump.")]
    public float coyoteTime = 0.15f;

    [Tooltip("Seconds after landing before the player can jump again.")]
    public float jumpCooldown = 0.4f;

    // ── Jump — Directional Travel ─────────────────────────────────────────
    [Header("Jump — Directional Travel")]
    [Tooltip("How far the player travels horizontally on a forward or backward jump, in world units.")]
    public float forwardBackJumpDistance = 5f;

    [Tooltip("How far the player travels laterally (orbit degrees) on a side jump.")]
    public float sideJumpOrbitDegrees = 40f;

    [Tooltip("Height multiplier for a forward jump — lower feels like a lunge.")]
    [Range(0.3f, 1.5f)]
    public float forwardJumpHeightMult = 0.75f;

    [Tooltip("Height multiplier for a backward jump — higher feels like a retreat.")]
    [Range(0.3f, 1.5f)]
    public float backwardJumpHeightMult = 1.2f;

    [Tooltip("Height multiplier for a side jump.")]
    [Range(0.3f, 1.5f)]
    public float sideJumpHeightMult = 0.9f;

    // ── Gravity ───────────────────────────────────────────────────────────
    [Header("Custom Gravity")]
    [Tooltip("Gravity while rising (positive, units/s²).")]
    public float riseGravity = 25f;

    [Tooltip("Gravity while falling (positive, units/s²). Higher = snappier descent.")]
    public float fallGravity = 45f;

    // ── Ground Check ──────────────────────────────────────────────────────
    [Header("Ground Check")]
    public Transform groundCheckOrigin;
    public float     groundCheckRadius = 0.25f;
    public LayerMask groundLayers;

    // ── Animator ──────────────────────────────────────────────────────────
    [Header("Animator")]
    public Animator animator;
    public float    animatorDampTime = 0.1f;

    // ── Animator param hashes ─────────────────────────────────────────────
    private static readonly int HashMoveX     = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY     = Animator.StringToHash("MoveY");
    private static readonly int HashJumpX     = Animator.StringToHash("JumpX");
    private static readonly int HashJumpY     = Animator.StringToHash("JumpY");
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");
    private static readonly int HashIsFalling = Animator.StringToHash("IsFalling");
    private static readonly int HashGrounded  = Animator.StringToHash("IsGrounded");

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

    private static readonly Vector2[] JumpBlendDirs =
    {
        new Vector2( 0f,  1f),
        new Vector2( 0f, -1f),
        new Vector2(-1f,  0f),
        new Vector2( 1f,  0f),
    };

    // ── Jump type ─────────────────────────────────────────────────────────
    private enum JumpType { Neutral, Forward, Backward, Left, Right }

    // ── Internal state ────────────────────────────────────────────────────
    private CharacterController _cc;
    private float               _currentOrbitAngle;
    private float               _currentDistance;
    private float               _verticalVelocity;
    private bool                _isJumping;
    private bool                _jumpRequested;
    private float               _coyoteTimer;
    private float               _jumpCooldownTimer;
    private bool                _isGrounded;
    private bool                _wasGrounded;
    private Vector2             _jumpDirection;
    private Vector2             _currentInput;

    // Directional jump — constant velocity model
    private JumpType            _currentJumpType;
    private float               _jumpApproachVelocity;
    private float               _jumpApproachRemaining;
    private float               _jumpOrbitVelocity;
    private float               _jumpOrbitRemaining;

    // ── Public state ──────────────────────────────────────────────────────
    public bool IsMoving   { get; private set; }
    public bool IsGrounded => _isGrounded;

    // ── Unity lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        Debug.Log("[PlayerLocomotion] Awake — CharacterController found.");
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

        Debug.Log($"[PlayerLocomotion] Start — orbit angle: {_currentOrbitAngle:F1}°, distance: {_currentDistance:F2}");
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.onJumpDashPressed += OnJumpPressed;
        else
            Debug.LogWarning("[PlayerLocomotion] OnEnable — InputManager not ready.");
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.onJumpDashPressed -= OnJumpPressed;
    }

    // ── Update ────────────────────────────────────────────────────────────
    // Everything now runs in Update — CharacterController.Move() is called
    // once per frame and moves the Transform directly. No FixedUpdate needed.
    private void Update()
    {
        if (target == null || InputManager.Instance == null) return;

        // CharacterController has its own grounded check but we keep ours
        // for the coyote timer and landing detection
        _isGrounded = CheckGrounded();

        Vector2 rawInput = InputManager.Instance.GetMoveInput();
        IsMoving     = rawInput.magnitude > inputThreshold;
        _currentInput = (IsMoving && _isGrounded) ? rawInput : Vector2.zero;

        HandleCoyoteTime();
        HandleLanding();
        HandleJumpCooldown();

        ApplyMovement();
        FaceTarget();
        DriveAnimator(_currentInput);

        _wasGrounded = _isGrounded;
    }

    // ── Ground check ──────────────────────────────────────────────────────
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

    // ── Jump input callback ───────────────────────────────────────────────
    private void OnJumpPressed(object sender, System.EventArgs e)
    {
        if (e is not InputManager.DashJumpEventArgs args)
        {
            Debug.LogWarning("[PlayerLocomotion] OnJumpPressed — unexpected EventArgs type.");
            return;
        }

        if (!args.IsDoubleTap)
        {
            Debug.Log($"[PlayerLocomotion] Single tap (dash) — ignoring. Direction: {args.Direction}");
            return;
        }

        if (!CanJump())
        {
            Debug.Log($"[PlayerLocomotion] Jump blocked | grounded: {_isGrounded} | coyote: {_coyoteTimer:F3} | cooldown: {_jumpCooldownTimer:F3} | isJumping: {_isJumping}");
            return;
        }

        _jumpDirection   = args.Direction.magnitude > inputThreshold
            ? SnapToNearest(args.Direction.normalized, JumpBlendDirs)
            : Vector2.zero;

        _currentJumpType = ClassifyJump(args.Direction);

        // Compute air time so horizontal velocity covers the full distance
        // in exactly the time the parabola takes to complete
        float activeHeight;
        switch (_currentJumpType)
        {
            case JumpType.Forward:  activeHeight = jumpHeight * forwardJumpHeightMult;  break;
            case JumpType.Backward: activeHeight = jumpHeight * backwardJumpHeightMult; break;
            case JumpType.Left:
            case JumpType.Right:    activeHeight = jumpHeight * sideJumpHeightMult;     break;
            default:                activeHeight = jumpHeight;                           break;
        }

        float launchVelocity = Mathf.Sqrt(2f * riseGravity * activeHeight);
        float riseTime       = launchVelocity / riseGravity;
        float fallTime       = Mathf.Sqrt(2f * activeHeight / fallGravity);
        float totalAirTime   = riseTime + fallTime;

        Debug.Log($"[PlayerLocomotion] Jump type: {_currentJumpType} | height: {activeHeight:F2} | launchVel: {launchVelocity:F2} | airTime: {totalAirTime:F3}s");

        switch (_currentJumpType)
        {
            case JumpType.Forward:
                _jumpApproachVelocity  =  forwardBackJumpDistance / totalAirTime;
                _jumpApproachRemaining =  forwardBackJumpDistance;
                _jumpOrbitVelocity     =  0f;
                _jumpOrbitRemaining    =  0f;
                Debug.Log($"[PlayerLocomotion] Forward | approachVel: {_jumpApproachVelocity:F2} u/s");
                break;

            case JumpType.Backward:
                _jumpApproachVelocity  = -(forwardBackJumpDistance / totalAirTime);
                _jumpApproachRemaining =   forwardBackJumpDistance;
                _jumpOrbitVelocity     =   0f;
                _jumpOrbitRemaining    =   0f;
                Debug.Log($"[PlayerLocomotion] Backward | approachVel: {_jumpApproachVelocity:F2} u/s");
                break;

            case JumpType.Left:
                _jumpOrbitVelocity     =  sideJumpOrbitDegrees / totalAirTime;
                _jumpOrbitRemaining    =  sideJumpOrbitDegrees;
                _jumpApproachVelocity  =  0f;
                _jumpApproachRemaining =  0f;
                Debug.Log($"[PlayerLocomotion] Left | orbitVel: {_jumpOrbitVelocity:F2} deg/s");
                break;

            case JumpType.Right:
                _jumpOrbitVelocity     = -(sideJumpOrbitDegrees / totalAirTime);
                _jumpOrbitRemaining    =   sideJumpOrbitDegrees;
                _jumpApproachVelocity  =   0f;
                _jumpApproachRemaining =   0f;
                Debug.Log($"[PlayerLocomotion] Right | orbitVel: {_jumpOrbitVelocity:F2} deg/s");
                break;

            default:
                _jumpApproachVelocity  = 0f;
                _jumpApproachRemaining = 0f;
                _jumpOrbitVelocity     = 0f;
                _jumpOrbitRemaining    = 0f;
                Debug.Log("[PlayerLocomotion] Neutral jump.");
                break;
        }

        _jumpRequested = true;
    }

    // ── Classify jump direction ───────────────────────────────────────────
    private JumpType ClassifyJump(Vector2 direction)
    {
        if (direction.magnitude <= inputThreshold)
            return JumpType.Neutral;

        if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x))
            return direction.y > 0f ? JumpType.Forward : JumpType.Backward;
        else
            return direction.x > 0f ? JumpType.Right : JumpType.Left;
    }

    // ── Timers ────────────────────────────────────────────────────────────
    private void HandleCoyoteTime()
    {
        if (_isGrounded) _coyoteTimer = coyoteTime;
        else             _coyoteTimer -= Time.deltaTime;
    }

    private void HandleLanding()
    {
        if (!_isJumping || !_isGrounded || _wasGrounded) return;

        Debug.Log($"[PlayerLocomotion] Landed | type: {_currentJumpType} | impact velocity: {_verticalVelocity:F2}");

        _isJumping             = false;
        _currentJumpType       = JumpType.Neutral;
        _jumpApproachVelocity  = 0f;
        _jumpApproachRemaining = 0f;
        _jumpOrbitVelocity     = 0f;
        _jumpOrbitRemaining    = 0f;
        _jumpCooldownTimer     = jumpCooldown;

        animator.SetBool(HashIsJumping, false);
        animator.SetBool(HashIsFalling, false);
        animator.SetBool(HashGrounded,  true);
    }

    private void HandleJumpCooldown()
    {
        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.deltaTime;
    }

    // ── Main movement (replaces FixedUpdate + Rigidbody) ─────────────────
    private void ApplyMovement()
    {
        // ── Launch ────────────────────────────────────────────────────────
        if (_jumpRequested)
        {
            float activeHeight;
            switch (_currentJumpType)
            {
                case JumpType.Forward:  activeHeight = jumpHeight * forwardJumpHeightMult;  break;
                case JumpType.Backward: activeHeight = jumpHeight * backwardJumpHeightMult; break;
                case JumpType.Left:
                case JumpType.Right:    activeHeight = jumpHeight * sideJumpHeightMult;     break;
                default:                activeHeight = jumpHeight;                           break;
            }

            _verticalVelocity = Mathf.Sqrt(2f * riseGravity * activeHeight);
            _isJumping        = true;
            _jumpRequested    = false;
            _coyoteTimer      = 0f;

            animator.SetFloat(HashJumpX,    _jumpDirection.x);
            animator.SetFloat(HashJumpY,    _jumpDirection.y);
            animator.SetBool(HashIsJumping, true);
            animator.SetBool(HashIsFalling, false);
            animator.SetBool(HashGrounded,  false);

            Debug.Log($"[PlayerLocomotion] Jump launched | verticalVelocity: {_verticalVelocity:F2}");
        }

        // ── Gravity ───────────────────────────────────────────────────────
        if (_isJumping || !_isGrounded)
        {
            float g = _verticalVelocity < 0f ? fallGravity : riseGravity;
            _verticalVelocity -= g * Time.deltaTime;

            if (_verticalVelocity < 0f && !animator.GetBool(HashIsFalling))
            {
                animator.SetBool(HashIsFalling, true);
                Debug.Log($"[PlayerLocomotion] Apex passed — falling | velocity: {_verticalVelocity:F2}");
            }
        }
        else
        {
            // Small constant downward push keeps CharacterController grounded
            // reliably on slopes and steps without fighting gravity
            _verticalVelocity = -2f;
        }

        // ── Horizontal orbit/approach (grounded) ──────────────────────────
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

        // ── Directional jump horizontal travel (constant velocity) ─────────
        if (_isJumping)
        {
            // Forward / Backward — travel along the approach axis
            if (_jumpApproachRemaining > 0f)
            {
                float step = _jumpApproachVelocity * Time.deltaTime;

                if (Mathf.Abs(step) > _jumpApproachRemaining)
                    step = _jumpApproachRemaining * Mathf.Sign(_jumpApproachVelocity);

                _currentDistance       -= step;
                _currentDistance        = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
                _jumpApproachRemaining -= Mathf.Abs(step);

                Debug.Log($"[PlayerLocomotion] Approach step: {step:F4} | dist: {_currentDistance:F3} | remaining: {_jumpApproachRemaining:F3}");
            }

            // Left / Right — orbit around the target
            if (_jumpOrbitRemaining > 0f)
            {
                float step = _jumpOrbitVelocity * Time.deltaTime;

                if (Mathf.Abs(step) > _jumpOrbitRemaining)
                    step = _jumpOrbitRemaining * Mathf.Sign(_jumpOrbitVelocity);

                _currentOrbitAngle  += step;
                _jumpOrbitRemaining -= Mathf.Abs(step);

                Debug.Log($"[PlayerLocomotion] Orbit step: {step:F4}° | angle: {_currentOrbitAngle:F2}° | remaining: {_jumpOrbitRemaining:F3}°");
            }
        }

        // ── Compute desired world position from orbit state ────────────────
        Vector3 orbitPos = OrbitPosition(_currentOrbitAngle, _currentDistance);

        // The horizontal delta is the difference between where orbit puts us
        // and where we currently are — CharacterController.Move takes a delta
        Vector3 horizontalDelta = new Vector3(
            orbitPos.x - transform.position.x,
            0f,
            orbitPos.z - transform.position.z
        );

        // Combine horizontal orbit movement with vertical velocity
        Vector3 motion = horizontalDelta + Vector3.up * (_verticalVelocity * Time.deltaTime);

        _cc.Move(motion);
    }

    // ── Face target ───────────────────────────────────────────────────────
    private void FaceTarget()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(toTarget);
    }

    private bool CanJump() => (_isGrounded || _coyoteTimer > 0f)
                           && _jumpCooldownTimer <= 0f
                           && !_isJumping;

    // ── Animator ──────────────────────────────────────────────────────────
    private void DriveAnimator(Vector2 input)
    {
        if (animator == null) return;

        Vector2 snapped = input.magnitude > inputThreshold
            ? SnapToNearest(input.normalized, LocoBlendDirs)
            : Vector2.zero;

        animator.SetFloat(HashMoveX,   snapped.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(HashMoveY,   snapped.y, animatorDampTime, Time.deltaTime);
        animator.SetBool(HashGrounded, _isGrounded);
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

    private Vector3 OrbitPosition(float angleDeg, float distance)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return target.position + new Vector3(Mathf.Sin(rad) * distance, 0f, Mathf.Cos(rad) * distance);
    }

    private void SnapToOrbit()
    {
        Vector3 snapPos = OrbitPosition(_currentOrbitAngle, _currentDistance);
        snapPos.y = transform.position.y;

        // CharacterController ignores Transform.position sets while enabled,
        // so we disable it briefly to snap the position cleanly
        _cc.enabled = false;
        transform.position = snapPos;
        _cc.enabled = true;

        FaceTarget();
    }
}
