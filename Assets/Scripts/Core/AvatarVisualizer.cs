using System.Collections;
using UnityEngine;

public class AvatarVisualizer : MonoBehaviour
{
    private GameObject _avatarMesh;
    private Coroutine _idleRoutine;
    private Coroutine _currentAnimationRoutine;

    private void Awake()
    {
        _avatarMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _avatarMesh.name = "Avatar_Mesh";
        _avatarMesh.transform.SetParent(this.transform);
        _avatarMesh.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        _avatarMesh.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

        Shader avatarShader = Shader.Find("Universal Render Pipeline/Lit");
        if (avatarShader == null) avatarShader = Shader.Find("Standard");
        if (avatarShader != null)
        {
            Material material = new Material(avatarShader);
            material.color = new Color(0.12f, 0.58f, 0.95f, 1f);
            Renderer renderer = _avatarMesh.GetComponent<Renderer>();
            if (renderer != null) renderer.material = material;
        }

        Collider avatarCollider = _avatarMesh.GetComponent<Collider>();
        if (avatarCollider != null) avatarCollider.enabled = false;

        _idleRoutine = StartCoroutine(IdleAnimation());

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0f, 0f, -6f);
            mainCamera.transform.rotation = Quaternion.identity;
            mainCamera.fieldOfView = 50f;
        }
    }

    public void TriggerLookUp()
    {
        StopCurrentAnimation();
        _currentAnimationRoutine = StartCoroutine(PlayLookUp());
    }

    public void TriggerGreet()
    {
        StopCurrentAnimation();
        _currentAnimationRoutine = StartCoroutine(PlayGreet());
    }

    public void TriggerIdle()
    {
        StopCurrentAnimation();
        _idleRoutine = StartCoroutine(IdleAnimation());
    }

    private void StopCurrentAnimation()
    {
        if (_idleRoutine != null)
        {
            StopCoroutine(_idleRoutine);
            _idleRoutine = null;
        }
        if (_currentAnimationRoutine != null)
        {
            StopCoroutine(_currentAnimationRoutine);
            _currentAnimationRoutine = null;
        }
    }

    private IEnumerator IdleAnimation()
    {
        float time = 0f;
        Vector3 baseScale = _avatarMesh.transform.localScale;
        Quaternion baseRotation = _avatarMesh.transform.localRotation;

        while (true)
        {
            time += Time.deltaTime;
            float breath = Mathf.Sin(time * Mathf.PI) * 0.025f;
            _avatarMesh.transform.localScale = baseScale + new Vector3(breath, breath, breath);
            _avatarMesh.transform.localRotation = baseRotation * Quaternion.Euler(0f, (time * 18f) % 360f, 0f);
            yield return null;
        }
    }

    private IEnumerator PlayLookUp()
    {
        Quaternion startRotation = _avatarMesh.transform.localRotation;
        Vector3 startPosition = _avatarMesh.transform.localPosition;
        Quaternion targetRotation = startRotation * Quaternion.Euler(-20f, 0f, 0f);
        Vector3 targetPosition = startPosition + new Vector3(0f, 0.15f, 0f);
        yield return AnimatePose(startRotation, targetRotation, startPosition, targetPosition, 0.3f);
        yield return new WaitForSeconds(0.8f);
        yield return AnimatePose(targetRotation, startRotation, targetPosition, startPosition, 0.3f);
        TriggerIdle();
    }

    private IEnumerator PlayGreet()
    {
        Quaternion startRotation = _avatarMesh.transform.localRotation;
        Vector3 startPosition = _avatarMesh.transform.localPosition;
        Vector3 startScale = _avatarMesh.transform.localScale;
        Quaternion targetRotation = startRotation * Quaternion.Euler(20f, 0f, 0f);
        Vector3 targetScale = startScale * 0.94f;
        float duration = 0.35f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            _avatarMesh.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);
        time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(targetRotation, startRotation, t);
            _avatarMesh.transform.localScale = Vector3.Lerp(targetScale, startScale, t);
            yield return null;
        }
        _avatarMesh.transform.localRotation = startRotation;
        _avatarMesh.transform.localPosition = startPosition;
        _avatarMesh.transform.localScale = startScale;
        TriggerIdle();
    }

    private IEnumerator AnimatePose(Quaternion fromRotation, Quaternion toRotation, Vector3 fromPosition, Vector3 toPosition, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
            _avatarMesh.transform.localPosition = Vector3.Lerp(fromPosition, toPosition, t);
            yield return null;
        }
        _avatarMesh.transform.localRotation = toRotation;
        _avatarMesh.transform.localPosition = toPosition;
    }
}