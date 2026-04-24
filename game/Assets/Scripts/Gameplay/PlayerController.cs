// 2D top-down player movement for the Day 3 prototype kitchen.
// Uses the new Input System (WASD + arrow keys) — legacy Input Manager
// is forbidden by technical-preferences.md.

using UnityEngine;
using UnityEngine.InputSystem;

namespace DayOneChef.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;

        private Rigidbody2D _rb;
        private InputAction _moveAction;
        private Vector2 _moveInput;

        public float MoveSpeed
        {
            get => _moveSpeed;
            set => _moveSpeed = Mathf.Max(0f, value);
        }

        public Vector2 MoveInput => _moveInput;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
        }

        private void OnEnable() => _moveAction.Enable();
        private void OnDisable() => _moveAction.Disable();

        private void OnDestroy() => _moveAction?.Dispose();

        private void Update()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
        }

        private void FixedUpdate()
        {
            var dir = _moveInput.sqrMagnitude > 1f ? _moveInput.normalized : _moveInput;
            _rb.linearVelocity = dir * _moveSpeed;
        }
    }
}
