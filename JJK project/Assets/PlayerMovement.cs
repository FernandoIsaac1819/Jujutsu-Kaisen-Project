using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    CharacterController playerController;
    Animator playerAnimator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    void Start()
    {
        playerController = GetComponent<CharacterController>();
        playerAnimator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(locomotion());
    }

    private Vector3 locomotion()
    {
        var moveInput = InputManager.Instance.GetMoveInput();
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        return moveDirection;
    }

    void OnEnable()
    {
        var manager = InputManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("InputManager instance not found.");
            return;
        }

    }

    void OnDisable()
    {
        var manager = InputManager.Instance;
        if (manager == null)
        return;

    }
}
