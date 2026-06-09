// InputReader
// -----------
// A ScriptableObject wrapper around the auto-generated Controls class.
// Game systems subscribe to its C# events (MoveEvent, PrimaryFireEvent) and
// read its public properties (AimPosition) instead of touching the input
// system directly. This decouples gameplay code from the input backend and
// makes swapping bindings easy (just edit the .inputactions asset).
//
// Bindings (defined in Assets/Input/Controls.inputactions):
// - Move          (Vector2) -> fires MoveEvent(Vector2) on started/performed/canceled.
// - Primary Fire  (Button)  -> fires PrimaryFireEvent(true) on performed, (false) on canceled.
// - Aim           (Vector2) -> updates AimPosition on every change.
//
// Usage:
// - Create an asset via "Assets > Create > Input > Input Reader".
// - Reference the same asset from any system that needs input (e.g. PlayerMovement,
//   PlayerAiming, ProjectileLauncher). All of them will receive the same input.
//
// Lifecycle:
// - OnEnable creates the Controls instance, registers callbacks, and enables the map.
// - OnDisable disables the map (prevents input while the asset is unloaded).
using System;
using UnityEngine;
using UnityEngine.InputSystem;

using static Controls; // use the Controls class to access the input actions (auto created by input action code generator)


[CreateAssetMenu(fileName = "New Input Reader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{

    // Fired when the Move action's value changes.
    // The Vector2 is in the -1..1 range for each axis (WASD).
    public event Action<bool> PrimaryFireEvent;
    public event Action<Vector2> MoveEvent;

    // Current aim/cursor position in screen space (pixels). Updated by OnAim.
    public Vector2 AimPosition { get; private set; }


    // create controls field
    private Controls controls;


    // use OnEnable for ScriptableObject instead of Awake/Start
    private void OnEnable()
    {
        // create and instace controls if it doesn't exist
        if (controls == null)
        {
            controls = new Controls();
            // Register callbacks (we use "this" to reference the current IPlayerActions instance).
            controls.Player.SetCallbacks(this);
        }
        // enable controls if it's not already enabled
        // => this is to turn on and off controls at runtime
        // => if we have same bindings (maps) for other input readers, we can turn them off and on at runtime
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        // Stop receiving input when this asset is unloaded/disabled.
        controls.Player.Disable();
    }


    // Called by the Input System whenever the Move action changes.
    public void OnMove(InputAction.CallbackContext context)
    {
        // context.ReadValue<Vector2>() is a helper function to read the value of the action
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }


    // Called by the Input System whenever the Primary Fire action changes.
    // We translate the started/performed/canceled callbacks into a single
    // "should fire" bool (true while the button is held, false when released).
    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // use the "?" as a check, it means if something is subscribed to the event, then invoke it
            PrimaryFireEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }
    }


    // Called by the Input System whenever the Aim action changes.
    // Stores the latest cursor position for any system that needs to read it.
    public void OnAim(InputAction.CallbackContext context)
    {
        AimPosition = context.ReadValue<Vector2>();
    }
}
