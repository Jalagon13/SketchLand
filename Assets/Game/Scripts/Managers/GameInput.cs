using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SketchLand
{
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        public event Action<Vector2> OnMoveInputPressed;
        public event Action OnJumpPressed;
        public event Action<InputAction.CallbackContext> OnPrimaryActionPressed;
        public event Action<InputAction.CallbackContext> OnSecondaryActionPressed;
        public event Action<InputAction.CallbackContext> OnScrollWheel;
        public event Action<InputAction.CallbackContext> OnSelectSlot;
        public event Action<InputAction.CallbackContext> OnTogglePauseMenu;
        public event Action OnToggleInventory;
        public event Action OnToggleBuildLayer;
        public event Action OnSprintPressed;

        public Vector2 MoveInput { get; private set; }
        public bool JumpHold { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool PrimaryActionHeld { get; private set; }
        public bool SecondaryActionHeld { get; private set; }

        private PlayerInput _playerInput;

        private void Awake()
        {
            Instance = this;

            _playerInput = new PlayerInput();
            _playerInput.Enable();

            _playerInput.Player.Move.started += GameInput_OnMove;
            _playerInput.Player.Move.performed += GameInput_OnMove;
            _playerInput.Player.Move.canceled += GameInput_OnMove;
            _playerInput.Player.Jump.started += GameInput_OnJump;
            _playerInput.Player.Jump.canceled += GameInput_OnJump;
            _playerInput.Player.PrimaryAction.started += GameInput_OnPrimaryAction;
            _playerInput.Player.PrimaryAction.canceled += GameInput_OnPrimaryAction;
            _playerInput.Player.SecondaryAction.started += GameInput_OnSecondaryAction;
            _playerInput.Player.SecondaryAction.canceled += GameInput_OnSecondaryAction;
            _playerInput.Player.ToggleBuildLayer.started += GameInput_OnToggleBuildLayer;
            _playerInput.Player.Sprint.started += GameInput_OnSprintAction;
            _playerInput.Player.Sprint.canceled += GameInput_OnSprintAction;
            
            _playerInput.UI.ScrollWheel.performed += PlayerInput_OnScrollWheel;
            _playerInput.UI.SelectSlot.started += PlayerInput_OnSelectSlot;
            _playerInput.UI.ToggleInventory.started += PlayerInput_OnToggleInventory;
            _playerInput.UI.TogglePauseMenu.started += PlayerInput_OnTogglePauseMenu;
        }

        private void OnDestroy()
        {
            if (_playerInput == null)
            {
                return;
            }

            _playerInput.Player.Move.started -= GameInput_OnMove;
            _playerInput.Player.Move.performed -= GameInput_OnMove;
            _playerInput.Player.Move.canceled -= GameInput_OnMove;
            _playerInput.Player.Jump.started -= GameInput_OnJump;
            _playerInput.Player.Jump.canceled -= GameInput_OnJump;
            _playerInput.Player.PrimaryAction.started -= GameInput_OnPrimaryAction;
            _playerInput.Player.PrimaryAction.canceled -= GameInput_OnPrimaryAction;
            _playerInput.Player.SecondaryAction.started -= GameInput_OnSecondaryAction;
            _playerInput.Player.SecondaryAction.canceled -= GameInput_OnSecondaryAction;
            _playerInput.Player.ToggleBuildLayer.started -= GameInput_OnToggleBuildLayer;
            _playerInput.Player.Sprint.started -= GameInput_OnSprintAction;
            _playerInput.Player.Sprint.canceled -= GameInput_OnSprintAction;

            _playerInput.UI.ScrollWheel.performed -= PlayerInput_OnScrollWheel;
            _playerInput.UI.SelectSlot.started -= PlayerInput_OnSelectSlot;
            _playerInput.UI.ToggleInventory.started -= PlayerInput_OnToggleInventory;
            _playerInput.UI.TogglePauseMenu.started -= PlayerInput_OnTogglePauseMenu;

            _playerInput.Disable();
        }

        private void GameInput_OnSprintAction(InputAction.CallbackContext context)
        {
            SprintHeld = context.phase == InputActionPhase.Started;
            OnSprintPressed?.Invoke();
        }

        private void PlayerInput_OnTogglePauseMenu(InputAction.CallbackContext context)
        {
            OnTogglePauseMenu?.Invoke(context);
        }

        private void GameInput_OnToggleBuildLayer(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                OnToggleBuildLayer?.Invoke();
            }
        }

        private void PlayerInput_OnToggleInventory(InputAction.CallbackContext context)
        {
            if(context.phase == InputActionPhase.Started)
            {
                OnToggleInventory?.Invoke();
            }
        }

        private void PlayerInput_OnScrollWheel(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnScrollWheel?.Invoke(context);
            }
        }

        private void PlayerInput_OnSelectSlot(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                OnSelectSlot?.Invoke(context);
            }
        }

        private void GameInput_OnPrimaryAction(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                PrimaryActionHeld = true;
                OnPrimaryActionPressed?.Invoke(context);
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                PrimaryActionHeld = false;
                OnPrimaryActionPressed?.Invoke(context);
            }
        }

        private void GameInput_OnSecondaryAction(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started)
            {
                SecondaryActionHeld = true;
                OnSecondaryActionPressed?.Invoke(context);
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                SecondaryActionHeld = false;
                OnSecondaryActionPressed?.Invoke(context);
            }
        }

        private void GameInput_OnJump(InputAction.CallbackContext context)
        {
            if(context.phase == InputActionPhase.Started)
            {
                JumpHold = true;
            }
            else if(context.phase == InputActionPhase.Canceled)
            {
                JumpHold = false;
            }
        
            if (context.phase == InputActionPhase.Started)
            {
                OnJumpPressed?.Invoke();
            }
        }

        private void GameInput_OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();

            if (context.phase == InputActionPhase.Started || context.phase == InputActionPhase.Performed || context.phase == InputActionPhase.Canceled)
            {
                OnMoveInputPressed?.Invoke(MoveInput);
            }
        }
    }
}
