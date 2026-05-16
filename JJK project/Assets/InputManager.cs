using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private InputSystem_Actions inputActions;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }
        inputActions.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        // Read the Move action value
        Vector3 moveInput = inputActions.Player.Move.ReadValue<Vector3>();
        
        // Log the Move input to console
        if (moveInput != Vector3.zero)
        {
            Debug.Log($"Move Input: X={moveInput.x}, Y={moveInput.y}, Z={moveInput.z}");
        }
    }

    private void OnDestroy()
    {
        // Clean up input actions
        inputActions?.Disable();
        inputActions?.Dispose();
    }
}
