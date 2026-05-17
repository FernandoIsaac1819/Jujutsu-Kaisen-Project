using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private InputSystem_Actions inputSystem;

    private bool reinforcementActive;
    public bool ReinforcementActive => reinforcementActive;

    private bool abilityPrimed;
    public bool AbilityPrimed => abilityPrimed;
    public EventHandler onAbilityPrimed;
    public float abilityPrimeDuration = 3f;

    public EventHandler onAttackPressed;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            inputSystem = new InputSystem_Actions();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        if (inputSystem == null)
            inputSystem = new InputSystem_Actions();

        inputSystem.Player.Enable();
        inputSystem.Player.Reinforcement.performed += OnReinforcementPerformed;
        inputSystem.Player.Innatepower.performed += OnInnatePowerPressed;
        inputSystem.Player.Attack.performed += OnAttackPressed;
    }

    private void OnDisable()
    {
        if (inputSystem == null) return;

        inputSystem.Player.Reinforcement.performed -= OnReinforcementPerformed;
        inputSystem.Player.Innatepower.performed -= OnInnatePowerPressed;
        inputSystem.Player.Attack.performed -= OnAttackPressed;
        inputSystem.Player.Disable();
    }

    private void OnAttackPressed(InputAction.CallbackContext context)
    {
        onAttackPressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnReinforcementPerformed(InputAction.CallbackContext context)
    {
        reinforcementActive = !reinforcementActive;
    }

    private void OnInnatePowerPressed(InputAction.CallbackContext context)
    {
        if (abilityPrimed)
            return;

        StartCoroutine(PrimeAbility(abilityPrimeDuration));
    }

    private IEnumerator PrimeAbility(float timer)
    {
        abilityPrimed = true;
        onAbilityPrimed?.Invoke(this, EventArgs.Empty);
        yield return new WaitForSeconds(timer);
        abilityPrimed = false;
    }

    public Vector2 GetMoveInput() 
    {
        return inputSystem.Player.Move.ReadValue<Vector2>();
    }
}
