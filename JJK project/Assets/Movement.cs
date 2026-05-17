using System;
using System.Collections;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public static Movement Instance { get; private set; }
    public bool debugMode = true;
    public Animator playerAnimator;

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

        manager.onAbilityPrimed += HandleAbilityPrimed;
        manager.onAttackPressed += HandleAttackPressed;
    }

    private void OnDisable()
    {
        var manager = InputManager.Instance;
        if (manager == null)
            return;

        manager.onAbilityPrimed -= HandleAbilityPrimed;
        manager.onAttackPressed -= HandleAttackPressed;
    }

    private void HandleAttackPressed(object sender, EventArgs e)
    {
        if (InputManager.Instance.AbilityPrimed)
        {
            TriggerInnateTechnique1();
            return;
        }

        playerAnimator.SetTrigger("Attack");
    }

    private void TriggerInnateTechnique1()
    {
        Debug.Log("Innate Technique 1 activated!");
        // Add innate technique animation/effects here.
    }

    private void HandleAbilityPrimed(object sender, EventArgs e)
    {
        Debug.Log("Ability primed!");
    }

    private void Update()
    {
        if (!debugMode)
            return;

        Debug.Log(InputManager.Instance.GetMoveInput());
        Debug.Log(InputManager.Instance.ReinforcementActive ? "I am reinforced!" : "I am not reinforced.");
    }
}
