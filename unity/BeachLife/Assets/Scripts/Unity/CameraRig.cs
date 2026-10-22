using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// Management-sim camera: WASD / middle-drag to pan, wheel to zoom, Q/E to orbit, and
    /// hold the right mouse button to look around (yaw + pitch) like in the game view.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public float PanSpeed = 18f;
        public float ZoomSpeed = 14f;
        public float RotateSpeed = 90f;
        public float MinHeight = 6f;
        public float MaxHeight = 60f;
        public float MinPitch = 25f;
        public float MaxPitch = 65f;
        [Tooltip("Starting tilt in degrees; lower means a more ground-level view.")]
        public float Pitch = 38f;

        private float _yaw = 45f;
        private float _distance = 30f;
        private Vector3 _focus;
        private Camera _camera;
        private Vector2 _mapMin;
        private Vector2 _mapMax;
        private bool _clamped;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
        }

        private void Start()
        {
            // The scene may have been saved with the camera already framed on the resort;
            // recover the focus point so the first Apply() does not jump the view to origin.
            if (_focus.sqrMagnitude < 0.001f)
            {
                var rotation = Quaternion.Euler(Pitch, _yaw, 0f);
                Vector3 back = rotation * Vector3.forward * _distance;
                _focus = back.sqrMagnitude > 0.001f
                    ? transform.position + back
                    : transform.position + Vector3.forward * 20f;
            }
            ClampFocus();
            Apply();
        }

        public void LookAt(Vector3 focus, float distance)
        {
            _focus = focus;
            _distance = distance;
            Apply();
        }

        /// <summary>Keeps panning inside the resort plot so the view never leaves the map.</summary>
        public void SetMapBounds(float width, float height)
        {
            _mapMin = new Vector2(0f, 0f);
            _mapMax = new Vector2(width, height);
            _clamped = true;
            // No Apply() here: this can run before Start() restored the focus from the
            // saved camera transform; Start() clamps and applies afterwards.
        }

        private void ClampFocus()
        {
            if (!_clamped) return;
            _focus.x = Mathf.Clamp(_focus.x, _mapMin.x, _mapMax.x);
            _focus.z = Mathf.Clamp(_focus.z, _mapMin.y, _mapMax.y);
        }

        private void LateUpdate()
        {
            if (MainMenu.Visible)
            {
                _yaw += 5f * Time.unscaledDeltaTime; // slow scenic orbit behind the menu
                Apply();
                return;
            }

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

            // Right mouse button: look around - spin and tilt, clamped so the sky never flips.
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * 2.2f;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxis("Mouse Y") * 1.8f, MinPitch, MaxPitch);
            }

            if (Input.GetKey(KeyCode.Q)) _yaw -= RotateSpeed * dt;
            if (Input.GetKey(KeyCode.E)) _yaw += RotateSpeed * dt;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                _distance = Mathf.Clamp(_distance - scroll * ZoomSpeed * 4f, MinHeight, MaxHeight);

            ClampFocus();
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
