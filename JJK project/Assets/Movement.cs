using System;
using System.Collections;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public static Movement Instance { get; private set; }

    bool isReinforced = false;

    public bool debugMode = true;

    public Animator playerAnimator;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    [Obsolete]
    private void Start()
    {
        playerAnimator = GetComponent<Animator>();

        SubscribeToAbilityEvent();
    }

    [Obsolete]
    private void SubscribeToAbilityEvent()
    {
        var manager = InputManager.Instance;
        if (manager == null)
            manager = FindObjectOfType<InputManager>();

        if (manager != null)
            manager.onAbilityPrimed += HandleAbilityPrimed;
            manager.onAttackPressed += HandleAttackPressed;
    }

    [Obsolete]
    private void OnDisable()
    {
        var manager = InputManager.Instance;
        if (manager == null)
            manager = FindObjectOfType<InputManager>();

        if (manager != null)
            manager.onAbilityPrimed -= HandleAbilityPrimed;
            manager.onAttackPressed -= HandleAttackPressed;
    }

    //THIS IS MEANT TO TRIGGER ANIMATIONS AND DEAL DAMAGE IF THERE IS A HIT
    private void HandleAttackPressed(object sender, EventArgs e)
    {
        playerAnimator.SetTrigger("Attack");
    }

    private void HandleAbilityPrimed(object sender, EventArgs e)
    {
        // This method will be called when the ability is primed.
        //Cursed energy effect will trigger here
        //while ability is primed, player can click other buttons to activate abilities.
        Debug.Log("Ability primed!");
    }

    // Update is called once per frame
    void Update()
    {

        if(debugMode)
        {
            Vector2 moveInput = InputManager.Instance.GetMoveInput();
        Debug.Log(moveInput);

        bool reinforcementActive = InputManager.Instance.ReinforcementActive;
        isReinforced = reinforcementActive;

        if (isReinforced)
        {
            Debug.Log("I am reinforced!");
        }
        else
        {
            Debug.Log("I am not reinforced.");
        }
        }

    }


}
