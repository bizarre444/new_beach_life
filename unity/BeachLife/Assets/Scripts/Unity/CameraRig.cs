using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Classic management-sim camera: WASD / middle-drag to pan, wheel to zoom, Q/E to orbit.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public float PanSpeed = 18f;
        public float ZoomSpeed = 14f;
        public float RotateSpeed = 90f;
        public float MinHeight = 6f;
        public float MaxHeight = 60f;
        public float Pitch = 52f;

        private float _yaw = 45f;
        private float _distance = 28f;
        private Vector3 _focus;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
        }

        public void LookAt(Vector3 focus, float distance)
        {
            _focus = focus;
            _distance = distance;
            Apply();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move -= forward;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += right;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= right;
            _focus += move.normalized * PanSpeed * dt * (_distance / 25f);

            if (Input.GetMouseButton(2))
            {
                float dx = -Input.GetAxis("Mouse X") * 0.6f;
                float dy = -Input.GetAxis("Mouse Y") * 0.6f;
                _focus += (right * dx + forward * dy) * (_distance / 25f);
            }

            if (Input.GetKey(KeyCode.Q)) _yaw -= RotateSpeed * dt;
            if (Input.GetKey(KeyCode.E)) _yaw += RotateSpeed * dt;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                _distance = Mathf.Clamp(_distance - scroll * ZoomSpeed * 4f, MinHeight, MaxHeight);

            Apply();
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(Pitch, _yaw, 0f);
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * _distance;
        }
    }
}
