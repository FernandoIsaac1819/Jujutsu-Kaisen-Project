using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private InputSystem_Actions inputSystem;

    public bool AbilityPrimed { get; private set; }
    public EventHandler onAbilityPrimed;
    public float abilityPrimeDuration = 3f;

    public EventHandler onLightAttackPressed;
    public EventHandler onHeavyAttackPressed;
    public EventHandler onJumpPressed;
    public event EventHandler onLeftCharacterPressed;
    public event EventHandler onRightCharacterPressed;

    public event EventHandler onDetectionPressed;
    public event EventHandler onGuardPressed;
    public event EventHandler onReinforcementToggled;
    public event EventHandler onCounterPressed;
    public event EventHandler onDashPressed;


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        inputSystem = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputSystem ??= new InputSystem_Actions();
        inputSystem.Player.Enable();

        inputSystem.Player.Reinforcement.performed += OnReinforcementPerformed;
        inputSystem.Player.InnateTechnique.performed += OnInnatePowerPressed;
        inputSystem.Player.Attack.performed += OnLightAttackPressed;
        inputSystem.Player.Heavyattack.performed += OnHeavyAttackPressed;
        inputSystem.Player.LeftCharacter.performed += OnLeftCharacterPressed;
        inputSystem.Player.RightCharacter.performed += OnRightCharacterPressed;
        inputSystem.Player.Detection.performed += OnDetectionPressed;
        inputSystem.Player.Guard.performed += OnGuardPressed;
        inputSystem.Player.Counter.performed += OnCounterPressed;
        inputSystem.Player.Dash.performed += OnDashPressed;
    }

    private void OnDisable()
    {
        if (inputSystem == null)
            return;

        inputSystem.Player.Reinforcement.performed -= OnReinforcementPerformed;
        inputSystem.Player.InnateTechnique.performed -= OnInnatePowerPressed;
        inputSystem.Player.Attack.performed -= OnLightAttackPressed;
        inputSystem.Player.Heavyattack.performed -= OnHeavyAttackPressed;
        inputSystem.Player.LeftCharacter.performed -= OnLeftCharacterPressed;
        inputSystem.Player.RightCharacter.performed -= OnRightCharacterPressed;
        inputSystem.Player.Detection.performed -= OnDetectionPressed;
        inputSystem.Player.Guard.performed -= OnGuardPressed;
        inputSystem.Player.Counter.performed -= OnCounterPressed;
        inputSystem.Player.Dash.performed -= OnDashPressed;
        inputSystem.Player.Disable();
    }

    private void OnCounterPressed(InputAction.CallbackContext context)
        => onCounterPressed?.Invoke(this, EventArgs.Empty);

    private void OnDashPressed(InputAction.CallbackContext context)
        => onDashPressed?.Invoke(this, EventArgs.Empty);

    private void OnGuardPressed(InputAction.CallbackContext context)
        => onGuardPressed?.Invoke(this, EventArgs.Empty);

    private void OnDetectionPressed(InputAction.CallbackContext context)
        => onDetectionPressed?.Invoke(this, EventArgs.Empty);
    

    private void OnLeftCharacterPressed(InputAction.CallbackContext context)
        => onLeftCharacterPressed?.Invoke(this, EventArgs.Empty);

    private void OnRightCharacterPressed(InputAction.CallbackContext context)
        => onRightCharacterPressed?.Invoke(this, EventArgs.Empty); 
    
    private void OnLightAttackPressed(InputAction.CallbackContext context)
        => onLightAttackPressed?.Invoke(this, EventArgs.Empty);

    private void OnHeavyAttackPressed(InputAction.CallbackContext context)
        => onHeavyAttackPressed?.Invoke(this, EventArgs.Empty);

    private void OnReinforcementPerformed(InputAction.CallbackContext context)
       => onReinforcementToggled?.Invoke(this, EventArgs.Empty);
    

    private void OnInnatePowerPressed(InputAction.CallbackContext context)
    {
        if (AbilityPrimed)
            return;

        StartCoroutine(PrimeAbility(abilityPrimeDuration));
    }

    private IEnumerator PrimeAbility(float timer)
    {
        AbilityPrimed = true;
        onAbilityPrimed?.Invoke(this, EventArgs.Empty);
        yield return new WaitForSeconds(timer);
        AbilityPrimed = false;
    }

    public Vector2 GetMoveInput() => inputSystem.Player.Move.ReadValue<Vector2>();
}
