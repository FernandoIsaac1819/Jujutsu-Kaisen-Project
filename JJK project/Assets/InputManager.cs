using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private InputSystem_Actions inputSystem;

    public bool ReinforcementActive { get; private set; }
    public bool AbilityPrimed { get; private set; }
    public EventHandler onAbilityPrimed;
    public EventHandler onAttackPressed;
    public float abilityPrimeDuration = 3f;

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
        inputSystem.Player.Attack.performed += OnAttackPressed;
    }

    private void OnDisable()
    {
        if (inputSystem == null)
            return;

        inputSystem.Player.Reinforcement.performed -= OnReinforcementPerformed;
        inputSystem.Player.InnateTechnique.performed -= OnInnatePowerPressed;
        inputSystem.Player.Attack.performed -= OnAttackPressed;
        inputSystem.Player.Disable();
    }

    private void OnAttackPressed(InputAction.CallbackContext context)
        => onAttackPressed?.Invoke(this, EventArgs.Empty);

    private void OnReinforcementPerformed(InputAction.CallbackContext context)
        => ReinforcementActive = !ReinforcementActive;

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
