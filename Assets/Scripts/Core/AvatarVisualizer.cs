using System.Collections;
using UnityEngine;

public class AvatarVisualizer : MonoBehaviour
{
    private GameObject _avatarMesh;
    private Coroutine _idleRoutine;
    private Coroutine _currentAnimationRoutine;

    private void Awake()
    {
        // 1. Создание примитива капсулы для аватара
        _avatarMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _avatarMesh.name = "Avatar_Mesh";
        _avatarMesh.transform.SetParent(this.transform);
        
        // Позиционируем аватар в ЛЕВОЙ части экрана (x = -2), немного выше центра
        _avatarMesh.transform.localPosition = new Vector3(-2f, 0f, 0f);
        
        // Уменьшаем капсулу: была (1, 2, 1), стала (0.8, 1.6, 0.8) — компактнее
        _avatarMesh.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);

        // Убираем коллизию, если не нужна
        var collider = _avatarMesh.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        // 2. Запускаем Idle-анимацию ("дыхание")
        _idleRoutine = StartCoroutine(IdleAnimation());

        // 3. Настраиваем Главную Камеру для гармоничного кадра:
        // Камера смещена вправо (x = 1.5), чтобы захватить аватар слева и UI справа
        // Высота y = 1.2, дистанция z = -6 для широкого обзора
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(1.5f, 1.2f, -6f);
            mainCam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            // Небольшой field of view для более плоского, "UI-подобного" вида
            mainCam.fieldOfView = 50f;
        }
    }

    // Публичные методы, которые вызывает AgentCommandRouter
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
        // Принудительно останавливаем Idle-дыхание
        if (_idleRoutine != null)
        {
            StopCoroutine(_idleRoutine);
            _idleRoutine = null;
        }
        // И останавливаем любую другую активную анимацию (на случай быстрых кликов)
        if (_currentAnimationRoutine != null)
        {
            StopCoroutine(_currentAnimationRoutine);
            _currentAnimationRoutine = null;
        }
    }

    // --- Анимации через корутины ---

    private IEnumerator IdleAnimation()
    {
        // Бесконечное "дыхание" + медленный поворот
        float time = 0f;
        // Захватываем базовые значения ПРЯМО ПЕРЕД стартом цикла (важно после возврата из анимаций)
        Vector3 baseScale = _avatarMesh.transform.localScale;
        Quaternion baseRot = _avatarMesh.transform.localRotation;

        while (true)
        {
            time += Time.deltaTime;
            
            // Дыхание: масштаб по Y 1.0 -> 1.02 -> 1.0 за 2 сек
            float breath = Mathf.Sin(time * Mathf.PI) * 0.02f;
            _avatarMesh.transform.localScale = baseScale + new Vector3(0f, breath, 0f);

            // Медленный поворот по Y (0..360 за 20 сек)
            float rotY = (time * 18f) % 360f;
            _avatarMesh.transform.localRotation = baseRot * Quaternion.Euler(0f, rotY, 0f);

            yield return null;
        }
    }

    private IEnumerator PlayLookUp()
    {
        // "Поднять голову вверх" — наклон по X -30°, лёгкое приподнятие
        Quaternion startRot = _avatarMesh.transform.localRotation;
        Vector3 startPos = _avatarMesh.transform.localPosition;
        Quaternion targetRot = startRot * Quaternion.Euler(-30f, 0f, 0f);
        Vector3 targetPos = startPos + new Vector3(0f, 0.2f, 0f);

        float t = 0f;
        float duration = 0.3f;

        // Вход в позу
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(startRot, targetRot, k);
            _avatarMesh.transform.localPosition = Vector3.Lerp(startPos, targetPos, k);
            yield return null;
        }

        // Удержание позы 1 сек
        yield return new WaitForSeconds(1f);

        // Возврат
        t = 0f;
        duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(targetRot, startRot, k);
            _avatarMesh.transform.localPosition = Vector3.Lerp(targetPos, startPos, k);
            yield return null;
        }

        _avatarMesh.transform.localRotation = startRot;
        _avatarMesh.transform.localPosition = startPos;

        // Возврат к Idle
        TriggerIdle();
    }

    private IEnumerator PlayGreet()
    {
        // "Поклон" — наклон вперёд по X +45°, сжатие по Y
        Quaternion startRot = _avatarMesh.transform.localRotation;
        Vector3 startScale = _avatarMesh.transform.localScale;
        Quaternion targetRot = startRot * Quaternion.Euler(45f, 0f, 0f);
        Vector3 targetScale = startScale;
        targetScale.y *= 0.8f;

        float t = 0f;
        float duration = 0.4f;

        // Наклон
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(startRot, targetRot, k);
            _avatarMesh.transform.localScale = Vector3.Lerp(startScale, targetScale, k);
            yield return null;
        }

        // Удержание 0.8 сек
        yield return new WaitForSeconds(0.8f);

        // Возврат
        t = 0f;
        duration = 0.4f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _avatarMesh.transform.localRotation = Quaternion.Slerp(targetRot, startRot, k);
            _avatarMesh.transform.localScale = Vector3.Lerp(targetScale, startScale, k);
            yield return null;
        }

        _avatarMesh.transform.localRotation = startRot;
        _avatarMesh.transform.localScale = startScale;

        // Возврат к Idle
        TriggerIdle();
    }
}
