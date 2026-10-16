using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime, asset-free first prototype of Barabashka.
/// Attach to an empty GameObject. This is a safe visual prototype and does not
/// replace AvatarVisualizer or connect to agent events yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class BarabashkaAvatarPrototype : MonoBehaviour
{
    public enum AvatarState { Idle, Listening, Thinking, Speaking, Success, Error }

    [Header("Appearance")]
    [SerializeField] private float characterHeight = 1.8f;
    [SerializeField] private bool createLightParticles = true;
    [SerializeField] private AvatarState initialState = AvatarState.Idle;

    private Transform _root;
    private Transform _body;
    private Transform _head;
    private Transform _leftArm;
    private Transform _rightArm;
    private Transform _leftEye;
    private Transform _rightEye;
    private Transform _mouth;
    private Transform _curl;
    private LineRenderer _swirl;
    private readonly List<Transform> _particles = new List<Transform>();
    private readonly List<Material> _ownedMaterials = new List<Material>();
    private AvatarState _state;
    private float _stateTime;
    private float _blinkTimer = 2.3f;
    private float _blink;
    private Vector3 _mouthBaseScale;
    private Color _cyan = new Color(0.12f, 0.82f, 1f);
    private Color _blue = new Color(0.12f, 0.34f, 1f);
    private Color _violet = new Color(0.62f, 0.25f, 1f);
    private Color _pink = new Color(1f, 0.35f, 0.76f);

    public AvatarState CurrentState => _state;

    private void Awake()
    {
        BuildCharacter();
        SetState(initialState);
    }

    private void Update()
    {
        if (_root == null) return;
        float dt = Time.deltaTime;
        _stateTime += dt;
        float t = Time.time;

        // Gentle living motion, deliberately restrained for an assistant UI.
        _root.localPosition = new Vector3(0f, Mathf.Sin(t * 1.45f) * 0.045f, 0f);
        _root.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.45f) * 5f, Mathf.Sin(t * 0.8f) * 1.5f);
        _body.localScale = new Vector3(0.78f, 0.88f + Mathf.Sin(t * 2.0f) * 0.018f, 0.62f);
        _head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 2.0f, Mathf.Sin(t * 0.6f) * 5f, Mathf.Sin(t * 0.9f) * 2f);

        UpdateBlink(dt);
        UpdateStateMotion(t);
        UpdateSwirl(t);
        UpdateParticles(t);
    }

    /// <summary>Call this from the future agent-event bridge.</summary>
    public void SetState(AvatarState next)
    {
        _state = next;
        _stateTime = 0f;
        if (_mouth == null) return;

        switch (next)
        {
            case AvatarState.Idle:
                SetGlow(_cyan, 0.8f);
                _mouth.localScale = _mouthBaseScale;
                break;
            case AvatarState.Listening:
                SetGlow(_cyan, 1.5f);
                _mouth.localScale = new Vector3(_mouthBaseScale.x * 0.72f, _mouthBaseScale.y * 0.72f, _mouthBaseScale.z);
                break;
            case AvatarState.Thinking:
                SetGlow(_violet, 1.35f);
                _mouth.localScale = new Vector3(_mouthBaseScale.x * 0.65f, _mouthBaseScale.y * 0.8f, _mouthBaseScale.z);
                break;
            case AvatarState.Speaking:
                SetGlow(_pink, 1.8f);
                break;
            case AvatarState.Success:
                SetGlow(new Color(0.25f, 1f, 0.75f), 1.8f);
                break;
            case AvatarState.Error:
                SetGlow(new Color(1f, 0.25f, 0.32f), 1.3f);
                break;
        }
    }

    private void BuildCharacter()
    {
        _root = new GameObject("Barabashka_Root").transform;
        _root.SetParent(transform, false);
        float s = Mathf.Max(0.1f, characterHeight) / 1.8f;
        _root.localScale = Vector3.one * s;

        _body = CreateSphere("Spirit_Body", _root, new Vector3(0f, 0.02f, 0f),
            new Vector3(0.78f, 0.88f, 0.62f), _blue, true);
        _head = CreateSphere("Spirit_Head", _root, new Vector3(0f, 0.57f, -0.015f),
            new Vector3(1.08f, 0.96f, 0.72f), _cyan, true);

        // Soft cheek highlights.
        CreateSphere("Cheek_L", _head, new Vector3(-0.39f, -0.16f, -0.32f),
            new Vector3(0.20f, 0.11f, 0.035f), _pink, true);
        CreateSphere("Cheek_R", _head, new Vector3(0.39f, -0.16f, -0.32f),
            new Vector3(0.20f, 0.11f, 0.035f), _pink, true);

        _leftEye = CreateEye("Eye_L", _head, new Vector3(-0.235f, 0.08f, -0.345f));
        _rightEye = CreateEye("Eye_R", _head, new Vector3(0.235f, 0.08f, -0.345f));

        _mouth = CreateSphere("Mouth", _head, new Vector3(0f, -0.245f, -0.355f),
            new Vector3(0.24f, 0.105f, 0.045f), new Color(0.12f, 0.025f, 0.12f), false);
        _mouthBaseScale = _mouth.localScale;
        CreateSphere("Mouth_Glow", _head, new Vector3(0f, -0.292f, -0.365f),
            new Vector3(0.11f, 0.025f, 0.02f), _pink, true);

        _leftArm = CreateSphere("Arm_L", _root, new Vector3(-0.46f, -0.08f, -0.02f),
            new Vector3(0.18f, 0.43f, 0.18f), _cyan, true);
        _leftArm.localRotation = Quaternion.Euler(0f, 0f, -28f);
        _rightArm = CreateSphere("Arm_R", _root, new Vector3(0.46f, -0.04f, -0.02f),
            new Vector3(0.18f, 0.43f, 0.18f), _cyan, true);
        _rightArm.localRotation = Quaternion.Euler(0f, 0f, -25f);

        // Signature floating curl / forelock.
        _curl = CreateSphere("Luminous_Curl", _root, new Vector3(0.06f, 1.12f, 0.02f),
            new Vector3(0.22f, 0.43f, 0.20f), _violet, true);
        _curl.localRotation = Quaternion.Euler(0f, 0f, -22f);
        CreateSwirl();

        if (createLightParticles)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Transform p = CreateSphere("Glow_Speck_" + i, _root,
                    new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.55f, -0.1f),
                    Vector3.one * Random.Range(0.025f, 0.05f), i % 2 == 0 ? _cyan : _pink, true);
                _particles.Add(p);
            }
        }
    }

    private Transform CreateEye(string objectName, Transform parent, Vector3 position)
    {
        Transform eye = CreateSphere(objectName, parent, position,
            new Vector3(0.29f, 0.34f, 0.12f), Color.white, true);
        CreateSphere(objectName + "_Iris", eye, new Vector3(0f, -0.01f, -0.49f),
            new Vector3(0.72f, 0.78f, 0.32f), _blue, true);
        CreateSphere(objectName + "_Pupil", eye, new Vector3(0f, -0.015f, -0.66f),
            new Vector3(0.43f, 0.52f, 0.22f), new Color(0.015f, 0.035f, 0.12f), false);
        CreateSphere(objectName + "_Spark", eye, new Vector3(-0.055f, 0.085f, -0.78f),
            new Vector3(0.13f, 0.15f, 0.08f), Color.white, true);
        return eye;
    }

    private Transform CreateSphere(string objectName, Transform parent, Vector3 position,
        Vector3 scale, Color color, bool glow)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            if (glow)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.65f);
            }
            go.GetComponent<Renderer>().material = mat;
            _ownedMaterials.Add(mat);
        }
        return go.transform;
    }

    private void CreateSwirl()
    {
        GameObject lineObject = new GameObject("Luminous_Swirl_Line");
        lineObject.transform.SetParent(_root, false);
        _swirl = lineObject.AddComponent<LineRenderer>();
        _swirl.useWorldSpace = false;
        _swirl.loop = false;
        _swirl.positionCount = 48;
        _swirl.startWidth = 0.025f;
        _swirl.endWidth = 0.004f;
        _swirl.numCapVertices = 3;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = _pink;
            _swirl.material = mat;
            _ownedMaterials.Add(mat);
        }
    }

    private void UpdateSwirl(float t)
    {
        if (_swirl == null) return;
        for (int i = 0; i < _swirl.positionCount; i++)
        {
            float u = i / (float)(_swirl.positionCount - 1);
            float angle = u * Mathf.PI * 1.65f + t * 0.4f;
            float radius = 0.07f + u * 0.18f;
            _swirl.SetPosition(i, new Vector3(0.04f + Mathf.Cos(angle) * radius,
                0.88f + u * 0.52f, Mathf.Sin(angle) * radius - 0.03f));
        }
    }

    private void UpdateBlink(float dt)
    {
        _blinkTimer -= dt;
        if (_blinkTimer <= 0f)
        {
            _blink = Mathf.Min(1f, _blink + dt * 12f);
            if (_blink >= 1f) _blinkTimer = -0.08f;
        }
        if (_blinkTimer < 0f)
        {
            _blink = Mathf.Max(0f, _blink - dt * 12f);
            if (_blink <= 0f) _blinkTimer = Random.Range(2.0f, 4.2f);
        }
        float eyeY = Mathf.Lerp(0.34f, 0.045f, _blink);
        if (_leftEye != null) _leftEye.localScale = new Vector3(0.29f, eyeY, 0.12f);
        if (_rightEye != null) _rightEye.localScale = new Vector3(0.29f, eyeY, 0.12f);
    }

    private void UpdateStateMotion(float t)
    {
        if (_leftArm == null) return;
        float wave = 0f;
        if (_state == AvatarState.Listening) wave = Mathf.Sin(t * 2.4f) * 5f;
        if (_state == AvatarState.Speaking) wave = Mathf.Sin(t * 8f) * 12f;
        if (_state == AvatarState.Success) wave = Mathf.Sin(_stateTime * 8f) * 25f;
        _leftArm.localRotation = Quaternion.Euler(0f, 0f, -28f - wave);
        _rightArm.localRotation = Quaternion.Euler(0f, 0f, -25f + wave * 0.7f);

        if (_state == AvatarState.Speaking)
        {
            float open = 0.75f + Mathf.Abs(Mathf.Sin(t * 11f)) * 1.7f;
            _mouth.localScale = new Vector3(_mouthBaseScale.x, _mouthBaseScale.y * open, _mouthBaseScale.z);
        }
        else if (_state == AvatarState.Success)
        {
            _root.localPosition += Vector3.up * (Mathf.Abs(Mathf.Sin(_stateTime * 7f)) * 0.035f);
        }
        else if (_state == AvatarState.Error)
        {
            _head.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(_stateTime * 3f) * 2f);
        }
    }

    private void UpdateParticles(float t)
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            Transform p = _particles[i];
            if (p == null) continue;
            Vector3 basePos = new Vector3(Mathf.Cos(i * 0.9f) * 0.62f, Mathf.Sin(i * 0.9f) * 0.55f, -0.1f);
            p.localPosition = basePos + Vector3.up * (Mathf.Sin(t * 1.7f + i) * 0.06f);
        }
    }

    private void SetGlow(Color color, float intensity)
    {
        foreach (Material mat in _ownedMaterials)
        {
            if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
            mat.SetColor("_EmissionColor", color * intensity);
        }
    }

    private void OnDestroy()
    {
        foreach (Material mat in _ownedMaterials)
            if (mat != null) Destroy(mat);
    }
}
