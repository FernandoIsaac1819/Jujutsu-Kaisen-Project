using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private InputSystem_Actions inputSystem;

    public bool AbilityPrimed { get; private set; }
    public event EventHandler onAbilityPrimed;
    public float abilityPrimeDuration = 3f;
    private Coroutine _primeRoutine;

    public event EventHandler onLightAttackPressed;
    public event EventHandler onHeavyAttackPressed;
    public event EventHandler<DashJumpEventArgs> onJumpDashPressed;
    public event EventHandler onLeftCharacterPressed;
    public event EventHandler onRightCharacterPressed;
    public event EventHandler onGuardPressed;
    public event EventHandler onReinforcementToggled;
    public event EventHandler onCounterPressed;

    [Header("Jump/Dash Input")]
    [Tooltip("Time allowed between presses for a double-tap jump")]
    public float doubleTapWindow = 0.25f;

    [Tooltip("Minimum stick magnitude to count as directional input")]
    [Range(0.1f, 1f)]
    public float moveInputDeadzone = 0.2f;

    private float _lastJumpDashPressTime = -999f;
    private Vector2 _lastJumpDashDirection = Vector2.zero;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[InputManager] Duplicate instance detected — destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        inputSystem = new InputSystem_Actions();
        Debug.Log("[InputManager] Awake — singleton initialised.");
    }

    private void OnEnable()
    {
        inputSystem ??= new InputSystem_Actions();
        inputSystem.Player.Enable();

        inputSystem.Player.Reinforcement.performed   += OnReinforcementPerformed;
        inputSystem.Player.InnateTechnique.performed += OnInnatePowerPressed;
        inputSystem.Player.Attack.performed          += OnLightAttackPressed;
        inputSystem.Player.Heavyattack.performed     += OnHeavyAttackPressed;
        inputSystem.Player.LeftCharacter.performed   += OnLeftCharacterPressed;
        inputSystem.Player.RightCharacter.performed  += OnRightCharacterPressed;
        inputSystem.Player.Guard.performed           += OnGuardPressed;
        inputSystem.Player.DashJump.performed        += OnDashJumpPressed;

       // Debug.Log("[InputManager] OnEnable — all input actions subscribed.");
    }

    private void OnDisable()
    {
        if (inputSystem == null)
        {
            //Debug.LogWarning("[InputManager] OnDisable called but inputSystem is null — skipping unsubscribe.");
            return;
        }

        inputSystem.Player.Reinforcement.performed   -= OnReinforcementPerformed;
        inputSystem.Player.InnateTechnique.performed -= OnInnatePowerPressed;
        inputSystem.Player.Attack.performed          -= OnLightAttackPressed;
        inputSystem.Player.Heavyattack.performed     -= OnHeavyAttackPressed;
        inputSystem.Player.LeftCharacter.performed   -= OnLeftCharacterPressed;
        inputSystem.Player.RightCharacter.performed  -= OnRightCharacterPressed;
        inputSystem.Player.Guard.performed           -= OnGuardPressed;
        inputSystem.Player.DashJump.performed        -= OnDashJumpPressed;

        inputSystem.Player.Disable();
        Debug.Log("[InputManager] OnDisable — all input actions unsubscribed.");
    }

    // ── Dash/Jump input ──────────────────────────────────────────────────

    private void OnDashJumpPressed(InputAction.CallbackContext ctx)
    {
        Vector2 moveInput = GetMoveInput();
        Vector2 direction = moveInput.magnitude >= moveInputDeadzone
            ? moveInput.normalized
            : Vector2.zero;

        float timeSinceLastPress = Time.time - _lastJumpDashPressTime;
        bool isDoubleTap = timeSinceLastPress <= doubleTapWindow;

        Debug.Log($"[InputManager] DashJump pressed | moveInput: {moveInput} (magnitude: {moveInput.magnitude:F2}) | direction: {direction} | timeSinceLastPress: {timeSinceLastPress:F3}s | doubleTapWindow: {doubleTapWindow}s | isDoubleTap: {isDoubleTap}");

        if (isDoubleTap)
        {
            Debug.Log($"[InputManager] JUMP confirmed — consuming double-tap. Resetting press time and direction.");
            _lastJumpDashPressTime = -999f;
            _lastJumpDashDirection = Vector2.zero;
        }
        else
        {
            Debug.Log($"[InputManager] DASH confirmed — first tap registered. Waiting for potential second tap within {doubleTapWindow}s.");
            _lastJumpDashPressTime = Time.time;
            _lastJumpDashDirection = direction;
        }

        onJumpDashPressed?.Invoke(this, new DashJumpEventArgs(direction, isDoubleTap));
        //Debug.Log($"[InputManager] onJumpDashPressed fired | direction: {direction} | isDoubleTap: {isDoubleTap} | listeners: {(onJumpDashPressed == null ? "NONE" : "present")}");
    }

    // ── Other input callbacks ─────────────────────────────────────────────

    private void OnGuardPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Guard pressed.");
        onGuardPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onGuardPressed fired | listeners: {(onGuardPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnLeftCharacterPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Left character pressed.");
        onLeftCharacterPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onLeftCharacterPressed fired | listeners: {(onLeftCharacterPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnRightCharacterPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Right character pressed.");
        onRightCharacterPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onRightCharacterPressed fired | listeners: {(onRightCharacterPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnLightAttackPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Light attack pressed.");
        onLightAttackPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onLightAttackPressed fired | listeners: {(onLightAttackPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnHeavyAttackPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Heavy attack pressed.");
        onHeavyAttackPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onHeavyAttackPressed fired | listeners: {(onHeavyAttackPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnReinforcementPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Reinforcement toggled.");
        onReinforcementToggled?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onReinforcementToggled fired | listeners: {(onReinforcementToggled == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnCounterPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[InputManager] Counter pressed.");
        onCounterPressed?.Invoke(this, EventArgs.Empty);
        //Debug.Log($"[InputManager] onCounterPressed fired | listeners: {(onCounterPressed == null ? "NONE — nothing is subscribed!" : "present")}");
    }

    private void OnInnatePowerPressed(InputAction.CallbackContext ctx)
    {
        if (AbilityPrimed)
        {
            Debug.Log("[InputManager] InnateTechnique pressed but ability is already primed — ignoring.");
            return;
        }

        Debug.Log($"[InputManager] InnateTechnique pressed — starting prime coroutine ({abilityPrimeDuration}s window).");
        _primeRoutine = StartCoroutine(PrimeAbility(abilityPrimeDuration));
    }

    private IEnumerator PrimeAbility(float timer)
    {
        AbilityPrimed = true;
        Debug.Log($"[InputManager] Ability PRIMED — window open for {timer}s.");
        onAbilityPrimed?.Invoke(this, EventArgs.Empty);
        Debug.Log($"[InputManager] onAbilityPrimed fired | listeners: {(onAbilityPrimed == null ? "NONE — nothing is subscribed!" : "present")}");

        float elapsed = 0f;
        while (elapsed < timer)
        {
            elapsed += Time.deltaTime;
            float remaining = timer - elapsed;

            if (Mathf.FloorToInt(remaining * 2f) != Mathf.FloorToInt((remaining + Time.deltaTime) * 2f))
                Debug.Log($"[InputManager] Ability prime window closing in {remaining:F1}s...");
            yield return null;
        }

        AbilityPrimed = false;
        _primeRoutine = null;
        //Debug.Log("[InputManager] Ability prime window EXPIRED — AbilityPrimed reset to false.");
    }

    // ── Public helpers ────────────────────────────────────────────────────

    public void CancelPrimedAbility()
    {
        if (!AbilityPrimed)
        {
            Debug.Log("[InputManager] CancelPrimedAbility called but no ability is currently primed — no-op.");
            return;
        }

        if (_primeRoutine != null)
        {
            StopCoroutine(_primeRoutine);
            _primeRoutine = null;
        }

        AbilityPrimed = false;
        //Debug.Log("[InputManager] Ability prime CANCELLED externally — AbilityPrimed reset to false.");
    }

    public Vector2 GetMoveInput() => inputSystem.Player.Move.ReadValue<Vector2>();

    public Vector2 GetLastJumpDashDirection() => _lastJumpDashDirection;

    // ── Event args ────────────────────────────────────────────────────────

    public class DashJumpEventArgs : EventArgs
    {
        public Vector2 Direction { get; }
        public bool IsDoubleTap { get; }

        public DashJumpEventArgs(Vector2 direction, bool isDoubleTap)
        {
            Direction = direction;
            IsDoubleTap = isDoubleTap;
        }
    }
}
