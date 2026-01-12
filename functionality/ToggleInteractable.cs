using UnityEngine;
using TMPro;

namespace SwitchesLeversAndButtons
{
    public class ToggleInteractable : BooleanInteractable
    {
        [Header("State")]
        [SerializeField] private bool _isOn = false;

        [Header("Rotation")]
        [SerializeField] private float _onXAngle = -90f;
        [SerializeField] private float _offXAngle = 90f;
        [SerializeField] private float _yAngle = 0f;
        [SerializeField] private float _zAngle = 0f;

        [Header("Player interaction")]
        [SerializeField] private Transform player;           // optional; auto-found if null
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private int mouseButton = 1;        // 0=LMB, 1=RMB
        [SerializeField] private KeyCode altKey = KeyCode.None; // optional extra key

        private Quaternion _onRotation;
        private Quaternion _offRotation;
        private float _transitionTime = 0.25f;
        private float _elapsedTime = 0f;
        private bool _transitioning = false;

        public bool IsOn() => _isOn;

        // ... inside class ToggleInteractable ...
        [Header("Feedback (optional)")]
        [SerializeField] private TextMeshProUGUI feedbackLabel;
        // [SerializeField] private string lockedMsg = "Door is locked";
        // [SerializeField] private float lockedMsgSeconds = 1.0f;

        private Coroutine _feedbackCo;

        public void ShowFeedback(string msg, float seconds)
        {
            if (!feedbackLabel) return;
            if (_feedbackCo != null) StopCoroutine(_feedbackCo);
            _feedbackCo = StartCoroutine(FeedbackRoutine(msg, seconds));
        }

        private System.Collections.IEnumerator FeedbackRoutine(string msg, float seconds)
        {
            feedbackLabel.gameObject.SetActive(true);
            feedbackLabel.text = msg;
            float end = Time.unscaledTime + Mathf.Max(0.1f, seconds);
            while (Time.unscaledTime < end) yield return null;   // unscaled so overlays/pauses don't affect
            feedbackLabel.gameObject.SetActive(false);
        }

        void Awake()
        {
            if (!player && autoFindPlayer)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go) player = go.transform;
                else if (Camera.main) player = Camera.main.transform; // fallback
            }
        }

        void Start()
        {
            _onRotation = Quaternion.Euler(_onXAngle, _yAngle, _zAngle);
            _offRotation = Quaternion.Euler(_offXAngle, _yAngle, _zAngle);
            transform.rotation = _isOn ? _onRotation : _offRotation;
        }

        void Update()
        {
            if (_transitioning && _elapsedTime < _transitionTime)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    _isOn ? _onRotation : _offRotation,
                    _elapsedTime / _transitionTime
                );
                _elapsedTime += Time.deltaTime;
            }
            else if (_elapsedTime > _transitionTime)
            {
                _transitioning = false;
                _elapsedTime = 0f;
            }

            // Input + distance gate
            bool pressed = Input.GetMouseButtonDown(mouseButton) ||
                           (altKey != KeyCode.None && Input.GetKeyDown(altKey));

            if (!_transitioning && pressed && player)
            {
                if (Vector3.Distance(transform.position, player.position) <= interactionRange)
                {



                    _isOn = !_isOn;
                    _transitioning = true;
                    OnInteract?.Invoke(_isOn);           // existing event
                    // ShowFeedback(lockedMsg, lockedMsgSeconds);  // NEW line
                }
            }
        }

        /// <summary>
        /// Immediately set lever state and optionally invoke OnInteract.
        /// Useful when respawning/resetting the puzzle.
        /// </summary>
        public void ForceSet(bool on, bool invokeEvent)
        {
            _isOn = on;
            _transitioning = false;
            _elapsedTime = 0f;

            // Ensure rotations are initialized (in case called before Start)
            if (_onRotation == Quaternion.identity && _offRotation == Quaternion.identity)
            {
                _onRotation = Quaternion.Euler(_onXAngle, _yAngle, _zAngle);
                _offRotation = Quaternion.Euler(_offXAngle, _yAngle, _zAngle);
            }

            // Snap rotation immediately
            transform.rotation = _isOn ? _onRotation : _offRotation;

            if (invokeEvent) OnInteract?.Invoke(_isOn);
        }

        /// <summary>
        /// Allow external systems (e.g., SessionDirector) to set the player explicitly.
        /// </summary>
        public void SetPlayer(Transform t) => player = t;
    }
}
