using System;
using UnityEngine;
using UnityEngine.InputSystem;

using static Controls; // use the Controls class to access the input actions (auto created by input action code generator)


[CreateAssetMenu(fileName = "New Input Reader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{

    public event Action<bool> PrimaryFireEvent;
    public event Action<Vector2> MoveEvent;

    // create controls field
    private Controls controls;


    // use OnEnable for ScriptableObject instead of Awake/Start
    private void OnEnable()
    {

        // create and instace controls if it doesn't exist
        if (controls == null)
        {
            controls = new Controls();
            controls.Player.SetCallbacks(this); // register callbacks (we use "this" to reference the current IPlayerActions instance)
        }
        // enable controls if it's not already enabled
        // => this is to turn on and off controls at runtime
        // => if we have same bindings (maps) for other input readers, we can turn them off and on at runtime
        controls.Player.Enable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveEvent?.Invoke(context.ReadValue<Vector2>()); // context.ReadValue<Vector2>() is a helper function to read the value of the action
    }

    public void OnPrimaryFire(InputAction.CallbackContext context)
    {

        if (context.performed)
        {
            PrimaryFireEvent?.Invoke(true); // use the "?" as a check, it means if something is subscribed to the event, then invoke it
        }
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }

    }
}
