using System.Collections.Generic;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>
/// Барабашка: светящийся дух, который целиком строится кодом (без внешних моделей).
/// Тело, вихор и рот - настоящие меши, глаза, брови, руки и свечение - спрайты на разной глубине.
/// Всё снимает отдельная камера в RenderTexture (AvatarTexture), интерфейс показывает её через RawImage.
/// Поведение управляется состоянием агента (AgentActivityBus): слушает, думает, говорит, ошибка.
/// Класс сохранён под старым именем, чтобы остальной код (AppBootstrap, роутер) не менялся.
/// </summary>
public class AvatarVisualizer : MonoBehaviour
{
    public RenderTexture AvatarTexture { get; private set; }

    private const int TextureSize = 768;
    private const float BodyHeight = 2.4f;
    private const int BodyRows = 56;
    private const int BodyCols = 28;
    private const int HairSegments = 32;
    private const int MouthSegments = 18;
    private const int TongueSegments = 10;
    private const int SparkleCount = 14;
    private const float EyeY = 1.52f;
    private const float EyeX = 0.30f;
    private const float FaceZ = -0.62f;

    // Далеко от начала координат, чтобы аватар не пересекался с остальной сценой.
    private static readonly Vector3 WorldPosition = new Vector3(1000f, 0f, 0f);

    private GameObject _world;
    private Transform _pivot;   // центр тела: качается и парит
    private Transform _body;    // смещение, чтобы центр тела был в pivot
    private Camera _camera;
    private Material _material;
    private readonly List<Texture2D> _textures = new List<Texture2D>();

    // Меши
    private Mesh _bodyMesh;
    private Vector3[] _bodyBase;
    private Vector3[] _bodyVerts;
    private float[] _bodyTail;
    private Mesh _hairMesh;
    private Vector3[] _hairVerts;
    private Mesh _mouthMesh;
    private Vector3[] _mouthVerts;

    // Части
    private Transform _eyeL, _eyeR, _irisL, _irisR;
    private Transform _browL, _browR;
    private Transform _shoulderL, _shoulderR;
    private SpriteRenderer _aura, _auraLow;
    private Transform _mouthTransform;
    private readonly Transform[] _sparkles = new Transform[SparkleCount];
    private readonly SpriteRenderer[] _sparkleRenderers = new SpriteRenderer[SparkleCount];
    private readonly float[] _sparklePhase = new float[SparkleCount];
    private readonly float[] _sparkleRadius = new float[SparkleCount];
    private readonly float[] _sparkleSpeed = new float[SparkleCount];

    // Текущее состояние
    private AgentActivity _activity = AgentActivity.Idle;
    private float _time;
    private float _greetUntil = -1f;
    private float _lookUpUntil = -1f;
    private float _nextLook;
    private float _wanderX, _wanderY;
    private float _nextBlink = 2f;
    private float _blinkStart = -10f;

    // Цели и сглаженные значения мимики
    private float _tEyeOpen = 1f, _eyeOpen = 1f;
    private float _tBrow, _brow;
    private float _tBrowTilt, _browTilt;
    private float _tSmile = 0.5f, _smile = 0.5f;
    private float _tMouthOpen, _mouthOpen;
    private float _tLookX, _lookX;
    private float _tLookY, _lookY;
    private float _tTilt, _tilt;
    private float _tArmR = 25f, _armR = 25f;
    private float _tArmL = -25f, _armL = -25f;
    private float _tGlow = 1f, _glow = 1f;
    private float _tError, _error;
    private float _hairSpeed = 1f;

    // ---------------------------------------------------------------- создание

    private void Awake()
    {
        _material = CreateSpriteMaterial();
        if (_material == null)
            Debug.LogWarning("[Avatar] Не найден материал спрайтов: тело аватара не будет отображаться.");

        BuildWorld();
        BuildGlow();
        BuildHair();
        BuildBody();
        BuildArms();
        BuildFace();
        BuildSparkles();
    }

    private void OnEnable()
    {
        AgentActivityBus.Changed += OnActivityChanged;
        _activity = AgentActivityBus.Current;
    }

    private void OnDisable()
    {
        AgentActivityBus.Changed -= OnActivityChanged;
    }

    private void OnDestroy()
    {
        if (AvatarTexture != null)
        {
            AvatarTexture.Release();
            Destroy(AvatarTexture);
        }
        for (int i = 0; i < _textures.Count; i++)
            if (_textures[i] != null) Destroy(_textures[i]);
        if (_world != null) Destroy(_world);
    }

    private void OnActivityChanged(AgentActivity activity)
    {
        _activity = activity;
    }

    private static Material CreateSpriteMaterial()
    {
        // Берём стандартный материал спрайтов: он есть в любой сборке и в любом render pipeline.
        GameObject temp = new GameObject("AvatarMaterialProbe");
        SpriteRenderer probe = temp.AddComponent<SpriteRenderer>();
        Material shared = probe.sharedMaterial;
        Destroy(temp);

        if (shared != null) return new Material(shared);

        Shader shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private void BuildWorld()
    {
        _world = new GameObject("[Avatar3D]");
        _world.transform.position = WorldPosition;

        AvatarTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32);
        AvatarTexture.name = "Barabashka_AvatarRT";
        AvatarTexture.antiAliasing = 4;
        AvatarTexture.Create();

        GameObject cameraObject = new GameObject("AvatarCamera");
        cameraObject.transform.SetParent(_world.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.45f, -7.2f);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        // Цвет фона совпадает с фоном интерфейса: края аватара сливаются с ним даже без прозрачности.
        _camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 0f);
        _camera.fieldOfView = 28f;
        _camera.nearClipPlane = 0.3f;
        _camera.farClipPlane = 40f;
        _camera.allowHDR = false;
        _camera.depth = -50f;
        _camera.targetTexture = AvatarTexture;

        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(_world.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        _pivot = pivot.transform;

        GameObject body = new GameObject("Body");
        body.transform.SetParent(_pivot, false);
        body.transform.localPosition = new Vector3(0f, -1.3f, 0f);
        _body = body.transform;
    }

    // ---------------------------------------------------------------- спрайты

    private Sprite MakeSprite(int width, int height, System.Func<float, float, Color> pixel, Vector2 pivot, float pixelsPerUnit)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        _textures.Add(texture);
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, pixelsPerUnit);
    }

    private static float Radius(float u, float v)
    {
        float dx = (u - 0.5f) * 2f;
        float dy = (v - 0.5f) * 2f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private Sprite _discSprite, _glowSprite, _irisSprite, _capsuleSprite;

    private Sprite Disc()
    {
        if (_discSprite == null)
            _discSprite = MakeSprite(128, 128, (u, v) =>
                new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.86f, 1f, Radius(u, v))),
                new Vector2(0.5f, 0.5f), 128f);
        return _discSprite;
    }

    private Sprite Glow()
    {
        if (_glowSprite == null)
            _glowSprite = MakeSprite(128, 128, (u, v) =>
            {
                float r = Mathf.Clamp01(1f - Radius(u, v));
                return new Color(1f, 1f, 1f, Mathf.Pow(r, 2.2f));
            }, new Vector2(0.5f, 0.5f), 128f);
        return _glowSprite;
    }

    private Sprite Iris()
    {
        if (_irisSprite == null)
            _irisSprite = MakeSprite(128, 128, (u, v) =>
            {
                float r = Radius(u, v);
                float alpha = 1f - Mathf.SmoothStep(0.94f, 1f, r);
                Color inner = new Color(0.05f, 0.20f, 0.70f);
                Color middle = new Color(0.12f, 0.50f, 1.00f);
                Color outer = new Color(0.35f, 0.85f, 1.00f);
                Color c = r < 0.5f
                    ? Color.Lerp(inner, middle, r / 0.5f)
                    : Color.Lerp(middle, outer, (r - 0.5f) / 0.4f);
                // тёмный ободок радужки
                c = Color.Lerp(c, new Color(0.02f, 0.08f, 0.40f), Mathf.SmoothStep(0.82f, 0.96f, r));
                c.a = alpha;
                return c;
            }, new Vector2(0.5f, 0.5f), 128f);
        return _irisSprite;
    }

    // Капсула 0.5 x 1.0, ось вниз от опорной точки (верхний центр).
    private Sprite Capsule()
    {
        if (_capsuleSprite == null)
            _capsuleSprite = MakeSprite(64, 128, (u, v) =>
            {
                float px = u * 64f;
                float py = v * 128f;
                float cy = Mathf.Clamp(py, 32f, 96f);
                float d = Mathf.Sqrt((px - 32f) * (px - 32f) + (py - cy) * (py - cy));
                return new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(24f, 32f, d));
            }, new Vector2(0.5f, 1f), 128f);
        return _capsuleSprite;
    }

    private SpriteRenderer AddSprite(string name, Transform parent, Sprite sprite, Vector3 localPosition,
        Vector2 scale, Color color, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private MeshRenderer AddMesh(string name, Transform parent, Mesh mesh, Vector3 localPosition, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // ---------------------------------------------------------------- свечение и искры

    private void BuildGlow()
    {
        _aura = AddSprite("Aura", _body, Glow(), new Vector3(0f, 1.35f, 1.0f),
            new Vector2(4.6f, 4.6f), new Color(0.25f, 0.45f, 1f, 0.30f), 0);
        _auraLow = AddSprite("AuraLow", _body, Glow(), new Vector3(0f, 0.7f, 0.9f),
            new Vector2(3.4f, 3.4f), new Color(0.65f, 0.35f, 1f, 0.22f), 1);
    }

    private void BuildSparkles()
    {
        for (int i = 0; i < SparkleCount; i++)
        {
            float size = Random.Range(0.05f, 0.12f);
            _sparkleRenderers[i] = AddSprite("Sparkle" + i, _body, Glow(), Vector3.zero,
                new Vector2(size * 3f, size * 3f), new Color(0.8f, 0.95f, 1f, 0.8f), 30);
            _sparkles[i] = _sparkleRenderers[i].transform;
            _sparklePhase[i] = Random.Range(0f, Mathf.PI * 2f);
            _sparkleRadius[i] = Random.Range(0.95f, 1.7f);
            _sparkleSpeed[i] = Random.Range(0.25f, 0.6f) * (Random.value < 0.5f ? -1f : 1f);
        }
    }

    // ---------------------------------------------------------------- тело

    private static float BodyHalfWidth(float y)
    {
        float hy = (y - 1.55f) / 0.85f;
        float head = Mathf.Abs(hy) < 1f ? 0.80f * Mathf.Sqrt(1f - hy * hy) : 0f;
        float tail = y < 1.3f ? 0.36f * Mathf.Pow(Mathf.Clamp01(y / 1.3f), 1.15f) : 0f;
        // мягкое объединение головы и хвоста
        return Mathf.Pow(Mathf.Pow(head, 4f) + Mathf.Pow(tail, 4f), 0.25f);
    }

    private void BuildBody()
    {
        int vertexCount = (BodyRows + 1) * (BodyCols + 1);
        _bodyBase = new Vector3[vertexCount];
        _bodyVerts = new Vector3[vertexCount];
        _bodyTail = new float[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[BodyRows * BodyCols * 6];

        Color top = new Color(0.45f, 0.80f, 1.00f);
        Color mid = new Color(0.28f, 0.52f, 1.00f);
        Color low = new Color(0.62f, 0.38f, 0.98f);
        Color rimColor = new Color(0.75f, 0.92f, 1.00f);
        Color belly = new Color(1.00f, 0.72f, 0.35f);

        int index = 0;
        for (int r = 0; r <= BodyRows; r++)
        {
            float y = BodyHeight * r / BodyRows;
            float yn = y / BodyHeight;
            float w = BodyHalfWidth(y);
            float d = w * 0.85f;
            for (int c = 0; c <= BodyCols; c++)
            {
                float u = c / (float)BodyCols * 2f - 1f;
                float x = u * w;
                float z = -d * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); // выпуклость к камере (камера смотрит вдоль +z)

                _bodyBase[index] = new Vector3(x, y, z);
                _bodyTail[index] = Mathf.Clamp01((0.95f - y) / 0.95f);

                Color baseColor = yn > 0.55f
                    ? Color.Lerp(mid, top, (yn - 0.55f) / 0.45f)
                    : Color.Lerp(low, mid, yn / 0.55f);
                float rim = Mathf.Pow(Mathf.Abs(u), 3f);
                baseColor = Color.Lerp(baseColor, rimColor, rim * 0.55f);
                float shade = 0.84f + 0.16f * (-u * 0.4f + (yn - 0.5f) * 0.6f);
                baseColor = new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 1f);

                float bx = (x - 0.06f) / 0.24f;
                float by = (y - 0.95f) / 0.40f;
                float glow = Mathf.Exp(-(bx * bx + by * by));
                baseColor = Color.Lerp(baseColor, belly, glow * 0.8f);

                float alpha = Mathf.Clamp01(y / 0.55f);
                alpha *= 1f - Mathf.SmoothStep(0.80f, 1f, Mathf.Abs(u)) * 0.85f;
                baseColor.a = alpha;
                colors[index] = baseColor;
                index++;
            }
        }

        int t = 0;
        for (int r = 0; r < BodyRows; r++)
        {
            for (int c = 0; c < BodyCols; c++)
            {
                int a = r * (BodyCols + 1) + c;
                int b = a + 1;
                int cc = a + BodyCols + 1;
                int dd = cc + 1;
                triangles[t++] = a; triangles[t++] = cc; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = cc; triangles[t++] = dd;
            }
        }

        _bodyMesh = new Mesh();
        _bodyMesh.name = "Barabashka_Body";
        _bodyMesh.MarkDynamic();
        _bodyMesh.vertices = _bodyBase;
        _bodyMesh.colors = colors;
        _bodyMesh.triangles = triangles;
        _bodyMesh.RecalculateBounds();
        if (_material != null) AddMesh("BodyMesh", _body, _bodyMesh, Vector3.zero, 10);
    }

    private void UpdateBody()
    {
        float t = _time;
        for (int i = 0; i < _bodyBase.Length; i++)
        {
            Vector3 v = _bodyBase[i];
            float tail = _bodyTail[i];
            float sway = Mathf.Sin(t * 2.1f - v.y * 3.2f) * 0.13f * tail * tail;
            v.x += sway + tail * tail * 0.10f; // небольшой изгиб хвоста в сторону
            _bodyVerts[i] = v;
        }
        _bodyMesh.vertices = _bodyVerts;
        _bodyMesh.RecalculateBounds();
    }

    // ---------------------------------------------------------------- вихор

    private void BuildHair()
    {
        int vertexCount = (HairSegments + 1) * 2;
        _hairVerts = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[HairSegments * 6];

        for (int i = 0; i <= HairSegments; i++)
        {
            float s = i / (float)HairSegments;
            Color c = Color.Lerp(new Color(0.30f, 0.60f, 1f, 1f), new Color(0.80f, 0.95f, 1f, 0.55f), s);
            colors[i * 2] = c;
            colors[i * 2 + 1] = c;
        }
        int t = 0;
        for (int i = 0; i < HairSegments; i++)
        {
            int a = i * 2;
            triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
            triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
        }

        _hairMesh = new Mesh();
        _hairMesh.name = "Barabashka_Hair";
        _hairMesh.MarkDynamic();
        _hairMesh.vertices = _hairVerts;
        _hairMesh.colors = colors;
        _hairMesh.triangles = triangles;
        if (_material != null) AddMesh("HairMesh", _body, _hairMesh, new Vector3(0f, 0f, -0.30f), 5);
        UpdateHair();
    }

    private void UpdateHair()
    {
        // Спираль: начинается на макушке, поднимается и закручивается влево.
        Vector2 center = new Vector2(-0.25f, 2.45f);
        float wave = Mathf.Sin(_time * 1.7f * _hairSpeed) * 0.14f;
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= HairSegments; i++)
        {
            float s = i / (float)HairSegments;
            float angle = (-31f + s * 260f) * Mathf.Deg2Rad + wave * s;
            float radius = 0.29f * (1f - 0.58f * s);
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            p.x += Mathf.Sin(_time * 2.3f * _hairSpeed + s * 4f) * 0.025f * s;

            Vector2 tangent = i == 0 ? Vector2.up : (p - previous);
            if (tangent.sqrMagnitude < 1e-8f) tangent = Vector2.up;
            tangent.Normalize();
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float width = 0.17f * Mathf.Pow(1f - s, 0.7f) + 0.012f;

            _hairVerts[i * 2] = new Vector3(p.x + normal.x * width * 0.5f, p.y + normal.y * width * 0.5f, 0f);
            _hairVerts[i * 2 + 1] = new Vector3(p.x - normal.x * width * 0.5f, p.y - normal.y * width * 0.5f, 0f);
            previous = p;
        }
        _hairMesh.vertices = _hairVerts;
        _hairMesh.RecalculateBounds();
    }

    // ---------------------------------------------------------------- руки

    private void BuildArms()
    {
        Color armColor = new Color(0.38f, 0.64f, 1f, 0.95f);
        _shoulderL = BuildArm("ArmL", new Vector3(-0.66f, 1.12f, -0.30f), armColor);
        _shoulderR = BuildArm("ArmR", new Vector3(0.66f, 1.12f, -0.30f), armColor);
    }

    private Transform BuildArm(string name, Vector3 position, Color color)
    {
        GameObject shoulder = new GameObject(name);
        shoulder.transform.SetParent(_body, false);
        shoulder.transform.localPosition = position;
        AddSprite("Limb", shoulder.transform, Capsule(), Vector3.zero, new Vector2(0.34f, 0.52f), color, 12);
        AddSprite("Hand", shoulder.transform, Disc(), new Vector3(0f, -0.52f, 0f), new Vector2(0.27f, 0.27f),
            new Color(0.55f, 0.78f, 1f, 1f), 13);
        return shoulder.transform;
    }

    // ---------------------------------------------------------------- лицо

    private void BuildFace()
    {
        _eyeL = BuildEye("EyeL", -EyeX, out _irisL);
        _eyeR = BuildEye("EyeR", EyeX, out _irisR);

        Color browColor = new Color(0.12f, 0.20f, 0.62f, 0.90f);
        _browL = AddSprite("BrowL", _body, Disc(), new Vector3(-EyeX, EyeY + 0.36f, FaceZ), new Vector2(0.27f, 0.055f), browColor, 25).transform;
        _browR = AddSprite("BrowR", _body, Disc(), new Vector3(EyeX, EyeY + 0.36f, FaceZ), new Vector2(0.27f, 0.055f), browColor, 25).transform;

        Color blush = new Color(1f, 0.45f, 0.75f, 0.28f);
        AddSprite("CheekL", _body, Glow(), new Vector3(-0.52f, 1.20f, FaceZ + 0.04f), new Vector2(0.36f, 0.24f), blush, 18);
        AddSprite("CheekR", _body, Glow(), new Vector3(0.52f, 1.20f, FaceZ + 0.04f), new Vector2(0.36f, 0.24f), blush, 18);

        // Рот: меш, форма пересчитывается каждый кадр.
        int mouthVertices = (MouthSegments + 1) * 2 + (TongueSegments + 1) * 2;
        _mouthVerts = new Vector3[mouthVertices];
        Color[] colors = new Color[mouthVertices];
        int[] triangles = new int[(MouthSegments + TongueSegments) * 6];
        Color dark = new Color(0.07f, 0.03f, 0.26f, 1f);
        Color tongue = new Color(1f, 0.45f, 0.62f, 1f);
        for (int i = 0; i < mouthVertices; i++)
            colors[i] = i < (MouthSegments + 1) * 2 ? dark : tongue;

        int t = 0;
        for (int i = 0; i < MouthSegments; i++)
        {
            int a = i * 2;
            triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
            triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
        }
        int offset = (MouthSegments + 1) * 2;
        for (int i = 0; i < TongueSegments; i++)
        {
            int a = offset + i * 2;
            triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
            triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
        }

        _mouthMesh = new Mesh();
        _mouthMesh.name = "Barabashka_Mouth";
        _mouthMesh.MarkDynamic();
        _mouthMesh.vertices = _mouthVerts;
        _mouthMesh.colors = colors;
        _mouthMesh.triangles = triangles;
        if (_material != null)
            _mouthTransform = AddMesh("MouthMesh", _body, _mouthMesh, new Vector3(0f, 1.08f, FaceZ + 0.02f), 26).transform;
        UpdateMouth();
    }

    private Transform BuildEye(string name, float x, out Transform irisGroup)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(_body, false);
        root.transform.localPosition = new Vector3(x, EyeY, FaceZ);

        AddSprite("Sclera", root.transform, Disc(), Vector3.zero, new Vector2(0.42f, 0.52f),
            new Color(0.96f, 0.98f, 1f, 1f), 20);

        GameObject iris = new GameObject("IrisGroup");
        iris.transform.SetParent(root.transform, false);
        iris.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        AddSprite("Iris", iris.transform, Iris(), Vector3.zero, new Vector2(0.31f, 0.38f), Color.white, 21);
        AddSprite("Pupil", iris.transform, Disc(), Vector3.zero, new Vector2(0.14f, 0.17f),
            new Color(0.02f, 0.03f, 0.14f, 1f), 22);
        AddSprite("Highlight", iris.transform, Disc(), new Vector3(-0.06f, 0.08f, -0.01f), new Vector2(0.10f, 0.10f),
            Color.white, 23);
        AddSprite("HighlightSmall", iris.transform, Disc(), new Vector3(0.06f, -0.07f, -0.01f), new Vector2(0.045f, 0.045f),
            new Color(1f, 1f, 1f, 0.85f), 23);

        irisGroup = iris.transform;
        return root.transform;
    }

    private void UpdateMouth()
    {
        float smile = _smile;
        float open = Mathf.Clamp01(_mouthOpen);
        float half = 0.19f + 0.05f * Mathf.Clamp01(smile) + 0.03f * open;

        int index = 0;
        for (int i = 0; i <= MouthSegments; i++)
        {
            float t = i / (float)MouthSegments * 2f - 1f;
            float x = t * half;
            float upper = smile * 0.075f * t * t;
            float lower = upper - 0.020f - open * 0.19f * Mathf.Pow(Mathf.Max(0f, 1f - t * t), 0.7f);
            _mouthVerts[index++] = new Vector3(x, upper, 0f);
            _mouthVerts[index++] = new Vector3(x, lower, 0f);
        }

        for (int j = 0; j <= TongueSegments; j++)
        {
            float tt = j / (float)TongueSegments * 2f - 1f;
            float x = tt * half * 0.55f;
            float tm = tt * 0.55f;
            float upper = smile * 0.075f * tm * tm;
            float lower = upper - 0.020f - open * 0.19f * Mathf.Pow(Mathf.Max(0f, 1f - tm * tm), 0.7f);
            float bottom = lower + 0.006f;
            float topY = open > 0.15f ? bottom + open * 0.085f * (1f - tt * tt) : bottom;
            _mouthVerts[index++] = new Vector3(x, bottom, -0.005f);
            _mouthVerts[index++] = new Vector3(x, topY, -0.005f);
        }

        _mouthMesh.vertices = _mouthVerts;
        _mouthMesh.RecalculateBounds();
    }

    // ---------------------------------------------------------------- вызовы из роутера

    public void TriggerLookUp()
    {
        _lookUpUntil = Time.time + 1.2f;
    }

    public void TriggerGreet()
    {
        _greetUntil = Time.time + 2.0f;
    }

    public void TriggerIdle()
    {
        // Состояние Idle теперь определяется шиной состояний агента, отдельного действия не требуется.
    }

    // ---------------------------------------------------------------- анимация

    private void Update()
    {
        float dt = Time.deltaTime;
        _time += dt;

        UpdateTargets();

        float k = 1f - Mathf.Exp(-dt * 7f);
        _eyeOpen = Mathf.Lerp(_eyeOpen, _tEyeOpen, k);
        _brow = Mathf.Lerp(_brow, _tBrow, k);
        _browTilt = Mathf.Lerp(_browTilt, _tBrowTilt, k);
        _smile = Mathf.Lerp(_smile, _tSmile, k);
        _mouthOpen = Mathf.Lerp(_mouthOpen, _tMouthOpen, 1f - Mathf.Exp(-dt * 18f));
        _lookX = Mathf.Lerp(_lookX, _tLookX, k);
        _lookY = Mathf.Lerp(_lookY, _tLookY, k);
        _tilt = Mathf.Lerp(_tilt, _tTilt, k);
        _armR = Mathf.Lerp(_armR, _tArmR, k);
        _armL = Mathf.Lerp(_armL, _tArmL, k);
        _glow = Mathf.Lerp(_glow, _tGlow, k);
        _error = Mathf.Lerp(_error, _tError, k);

        ApplyPose();
        UpdateBody();
        UpdateHair();
        UpdateMouth();
        UpdateSparkles();
    }

    private void UpdateTargets()
    {
        float now = Time.time;
        float t = _time;
        bool greet = now < _greetUntil;
        bool lookUp = now < _lookUpUntil;

        if (now > _nextLook)
        {
            _wanderX = Random.Range(-0.4f, 0.4f);
            _wanderY = Random.Range(-0.25f, 0.25f);
            _nextLook = now + Random.Range(1.5f, 3.5f);
        }

        // Базовое состояние (Idle).
        _tEyeOpen = 1f;
        _tBrow = 0f;
        _tBrowTilt = 0f;
        _tSmile = 0.55f;
        _tMouthOpen = 0f;
        _tLookX = _wanderX * 0.6f;
        _tLookY = _wanderY * 0.6f;
        _tTilt = Mathf.Sin(t * 0.5f) * 2f;
        _tArmR = 25f + Mathf.Sin(t * 1.3f) * 4f;
        _tArmL = -25f - Mathf.Sin(t * 1.1f + 1f) * 4f;
        _tGlow = 1f;
        _tError = 0f;
        _hairSpeed = 1f;

        switch (_activity)
        {
            case AgentActivity.Listening:
                _tEyeOpen = 1.12f;
                _tBrow = 0.5f;
                _tSmile = 0.45f;
                _tLookX = _wanderX * 0.15f;
                _tLookY = 0.05f;
                _tTilt = 5f + Mathf.Sin(t * 0.8f) * 3f;
                _tArmR = 38f;
                _tGlow = 1.35f;
                break;

            case AgentActivity.Thinking:
                _tEyeOpen = 0.92f;
                _tBrow = 0.35f;
                _tBrowTilt = -0.3f;
                _tSmile = 0.05f;
                _tLookX = -0.6f + Mathf.Sin(t * 1.5f) * 0.15f;
                _tLookY = 0.7f;
                _tTilt = -5f;
                _tGlow = 0.9f + Mathf.Sin(t * 5f) * 0.1f;
                _hairSpeed = 2.4f;
                break;

            case AgentActivity.Speaking:
                {
                    float syllable = Mathf.PerlinNoise(t * 7f, 3.7f);
                    _tMouthOpen = Mathf.Clamp01((syllable - 0.25f) * 1.7f) * 0.9f;
                    _tSmile = 0.6f;
                    _tBrow = 0.15f + Mathf.Sin(t * 2.2f) * 0.1f;
                    _tLookX = _wanderX * 0.2f;
                    _tLookY = 0.0f;
                    _tGlow = 1.2f;
                    if (Mathf.Sin(t * 0.8f) > 0.1f)
                        _tArmR = 118f + Mathf.Sin(t * 5.5f) * 14f;
                    break;
                }

            case AgentActivity.Error:
                _tEyeOpen = 0.85f;
                _tBrow = 0.2f;
                _tBrowTilt = 1f;
                _tSmile = -0.55f;
                _tLookX = 0f;
                _tLookY = -0.4f;
                _tArmR = 14f;
                _tArmL = -14f;
                _tGlow = 0.6f;
                _tError = 1f;
                break;
        }

        if (lookUp)
        {
            _tLookX = 0.3f;
            _tLookY = 0.8f;
            _tBrow = 0.5f;
            _tSmile = 0.2f;
        }

        if (greet)
        {
            _tSmile = 0.95f;
            _tMouthOpen = Mathf.Max(_tMouthOpen, 0.3f + Mathf.Sin(t * 6f) * 0.1f);
            _tBrow = 0.4f;
            _tEyeOpen = 1.05f;
            _tLookX = 0f;
            _tLookY = 0.05f;
            _tArmR = 140f + Mathf.Sin(t * 10f) * 18f;
            _tGlow = 1.4f;
        }
    }

    private void ApplyPose()
    {
        float t = _time;

        // Парение и лёгкое покачивание: из-за разной глубины частей тела появляется параллакс.
        _pivot.localPosition = new Vector3(0f, 1.3f + Mathf.Sin(t * 1.6f) * 0.05f, 0f);
        float swayY = Mathf.Sin(t * 0.9f) * 7f + _lookX * 6f;
        float swayX = Mathf.Sin(t * 0.7f + 1f) * 2.5f - _lookY * 3f;
        _pivot.localRotation = Quaternion.Euler(swayX, swayY, -_tilt);

        // Моргание.
        if (t > _nextBlink)
        {
            _blinkStart = t;
            _nextBlink = t + Random.Range(2.5f, 5f);
        }
        float blinkPhase = (t - _blinkStart) / 0.16f;
        float blink = blinkPhase >= 0f && blinkPhase <= 1f ? Mathf.Sin(blinkPhase * Mathf.PI) : 0f;
        float eyeScale = Mathf.Max(0.06f, _eyeOpen * (1f - blink * 0.94f));
        Vector3 eyeVector = new Vector3(1f, eyeScale, 1f);
        _eyeL.localScale = eyeVector;
        _eyeR.localScale = eyeVector;

        Vector3 irisOffset = new Vector3(_lookX * 0.06f, _lookY * 0.07f, -0.01f);
        _irisL.localPosition = irisOffset;
        _irisR.localPosition = irisOffset;

        // Брови: подъём, наклон (беспокойство) и асимметрия при размышлении.
        float browUp = EyeY + 0.34f + _brow * 0.10f;
        float tiltDeg = _browTilt * 16f;
        _browL.localPosition = new Vector3(-EyeX, browUp + (_browTilt < 0f ? -_browTilt * 0.04f : 0f), FaceZ);
        _browR.localPosition = new Vector3(EyeX, browUp + (_browTilt < 0f ? _browTilt * 0.08f : 0f), FaceZ);
        _browL.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
        _browR.localRotation = Quaternion.Euler(0f, 0f, -tiltDeg);

        // Руки.
        _shoulderL.localRotation = Quaternion.Euler(0f, 0f, _armL);
        _shoulderR.localRotation = Quaternion.Euler(0f, 0f, _armR);

        // Свечение: ярче при активности, холоднее и темнее при ошибке.
        float pulse = 1f + Mathf.Sin(t * 2.0f) * 0.06f;
        Color auraColor = Color.Lerp(new Color(0.25f, 0.45f, 1f, 1f), new Color(0.80f, 0.30f, 0.50f, 1f), _error);
        auraColor.a = Mathf.Clamp01(0.14f + 0.16f * _glow) * pulse;
        _aura.color = auraColor;
        Color lowColor = Color.Lerp(new Color(0.65f, 0.35f, 1f, 1f), new Color(0.65f, 0.25f, 0.45f, 1f), _error);
        lowColor.a = Mathf.Clamp01(0.10f + 0.12f * _glow);
        _auraLow.color = lowColor;
    }

    private void UpdateSparkles()
    {
        float speedFactor = _activity == AgentActivity.Thinking ? 2.2f : 1f;
        for (int i = 0; i < SparkleCount; i++)
        {
            float angle = _sparklePhase[i] + _time * _sparkleSpeed[i] * speedFactor;
            float radius = _sparkleRadius[i];
            float x = Mathf.Cos(angle) * radius;
            float y = 1.4f + Mathf.Sin(angle) * radius * 0.85f + Mathf.Sin(_time * 1.3f + i) * 0.08f;
            _sparkles[i].localPosition = new Vector3(x, y, 0.3f);
            float twinkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_time * 1.8f + i * 1.7f));
            Color c = _sparkleRenderers[i].color;
            c.a = twinkle * Mathf.Clamp01(0.45f + 0.4f * _glow);
            _sparkleRenderers[i].color = c;
        }
    }
}
