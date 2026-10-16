using UnityEngine;

/// <summary>
/// Additive layout controller for Barabashka's three presentation modes.
/// Place this component on the avatar root (or a parent of it).
/// Configure local positions/scales in the Inspector for the actual scene.
/// It deliberately does not modify the existing application UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdaptiveAvatarLayout : MonoBehaviour
{
    public enum PresentationMode
    {
        Icon,
        Voice,
        Workspace
    }

    [Header("Mode targets (local space)")]
    [SerializeField] private Vector3 iconLocalPosition = new Vector3(0.72f, -0.72f, 0f);
    [SerializeField] private Vector3 iconLocalScale = new Vector3(0.22f, 0.22f, 0.22f);
    [SerializeField] private Vector3 voiceLocalPosition = new Vector3(0f, -0.05f, 0f);
    [SerializeField] private Vector3 voiceLocalScale = Vector3.one;
    [SerializeField] private Vector3 workspaceLocalPosition = new Vector3(0.58f, 0.18f, 0f);
    [SerializeField] private Vector3 workspaceLocalScale = new Vector3(0.48f, 0.48f, 0.48f);

    [Header("Transition")]
    [SerializeField, Min(0.05f)] private float transitionDuration = 0.28f;
    [SerializeField] private bool useUnscaledTime = true;

    public PresentationMode CurrentMode { get; private set; } = PresentationMode.Voice;

    private Vector3 _targetPosition;
    private Vector3 _targetScale;
    private Vector3 _startPosition;
    private Vector3 _startScale;
    private float _transitionElapsed;
    private bool _transitioning;

    private void Awake()
    {
        _targetPosition = transform.localPosition;
        _targetScale = transform.localScale;
        _startPosition = _targetPosition;
        _startScale = _targetScale;
    }

    private void Update()
    {
        if (!_transitioning) return;
        _transitionElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = Mathf.Clamp01(_transitionElapsed / transitionDuration);
        // SmoothStep avoids abrupt starts/stops when changing UI modes.
        float eased = t * t * (3f - 2f * t);
        transform.localPosition = Vector3.LerpUnclamped(_startPosition, _targetPosition, eased);
        transform.localScale = Vector3.LerpUnclamped(_startScale, _targetScale, eased);

        if (t >= 1f)
        {
            transform.localPosition = _targetPosition;
            transform.localScale = _targetScale;
            _transitioning = false;
        }
    }

    public void SetMode(PresentationMode mode)
    {
        CurrentMode = mode;
        switch (mode)
        {
            case PresentationMode.Icon:
                BeginTransition(iconLocalPosition, iconLocalScale);
                break;
            case PresentationMode.Voice:
                BeginTransition(voiceLocalPosition, voiceLocalScale);
                break;
            case PresentationMode.Workspace:
                BeginTransition(workspaceLocalPosition, workspaceLocalScale);
                break;
        }
    }

    private void BeginTransition(Vector3 position, Vector3 scale)
    {
        _startPosition = transform.localPosition;
        _startScale = transform.localScale;
        _targetPosition = position;
        _targetScale = scale;
        _transitionElapsed = 0f;
        _transitioning = true;
    }
}
