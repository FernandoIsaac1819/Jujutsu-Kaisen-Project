using System;
using System.Collections;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public static Movement Instance { get; private set; }
    public bool debugMode = true;
    public Animator playerAnimator;
    private bool isReinforced = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        playerAnimator = GetComponent<Animator>();

        var manager = InputManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("InputManager instance not found. Ability and attack events will not be registered.");
            return;
        }

        manager.onReinforcementToggled += ReinforcementPressed;
        manager.onAbilityPrimed += HandleAbilityPrimed;
        manager.onLightAttackPressed += LightAttackPressed;
        manager.onHeavyAttackPressed += HeavyAttackPressed;
        manager.onLeftCharacterPressed += (s, e) => Debug.Log("Left character selected.");
        manager.onRightCharacterPressed += (s, e) => Debug.Log("Right character selected.");
        manager.onDetectionPressed += (s, e) => Debug.Log("Detection cursed energy.");
        manager.onGuardPressed += (s, e) => Debug.Log("Guard pressed.");
        manager.onCounterPressed += (s, e) => Debug.Log("Counter pressed.");
        manager.onDashPressed += (s, e) => Debug.Log("Dash pressed.");
    }

    private void OnDisable()
    {
        var manager = InputManager.Instance;
        if (manager == null)
            return;

        manager.onReinforcementToggled -= ReinforcementPressed;
        manager.onAbilityPrimed -= HandleAbilityPrimed;
        manager.onLightAttackPressed -= LightAttackPressed;
        manager.onHeavyAttackPressed -= HeavyAttackPressed;
        manager.onLeftCharacterPressed -= (s, e) => Debug.Log("Left character selected.");
        manager.onRightCharacterPressed -= (s, e) => Debug.Log("Right character selected.");
        manager.onDetectionPressed -= (s, e) => Debug.Log("Detection cursed energy.");
        manager.onGuardPressed -= (s, e) => Debug.Log("Guard pressed.");
        manager.onCounterPressed -= (s, e) => Debug.Log("Counter pressed.");
        manager.onDashPressed -= (s, e) => Debug.Log("Dash pressed.");
    }

    private void HandleAbilityPrimed(object sender, EventArgs e)
    {
        Debug.Log("Ability primed!");
    }

    private void ReinforcementPressed(object sender, EventArgs e)
    {
        isReinforced = !isReinforced;
        Debug.Log(isReinforced);
    }

    private void LightAttackPressed(object sender, EventArgs e)
    {
        Debug.Log("Light attack pressed.");

        if (InputManager.Instance.AbilityPrimed)
        {
            TriggerInnateTechnique1();
            return;
        }

        //playerAnimator.SetTrigger("Attack");
    }

    private void HeavyAttackPressed(object sender, EventArgs e)
    {
        Debug.Log("Heavy attack pressed.");

        if (InputManager.Instance.AbilityPrimed)
        {
            TriggerInnateTechnique2();
            return;
        }

        //playerAnimator.SetTrigger("HeavyAttack");
    }

    private void TriggerInnateTechnique2()
    {
        Debug.Log("Innate Technique 2 activated!");
        // Add innate technique animation/effects here.
    }

    private void TriggerInnateTechnique1()
    {
        Debug.Log("Innate Technique 1 activated!");
        // Add innate technique animation/effects here.
    }

    private void Update()
    {
        if (!debugMode)
            return;

        Debug.Log(InputManager.Instance.GetMoveInput());
    }
}
