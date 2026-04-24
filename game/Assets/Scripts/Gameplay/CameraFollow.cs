// Simple top-down camera follow for the prototype.
// Uses SmoothDamp so the camera lags slightly behind the player for feel.

using UnityEngine;

namespace DayOneChef.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField, Min(0f)] private float _smoothTime = 0.15f;
        [SerializeField] private Vector2 _offset = Vector2.zero;

        private Vector3 _velocity;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            var desired = new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);
        }
    }
}
