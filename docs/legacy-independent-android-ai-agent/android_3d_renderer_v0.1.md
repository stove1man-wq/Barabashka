# Independent Android AI Agent — Android 3D Renderer Integration v0.1

> **Статус: Superseded (D-005, 2026-10-12).** SceneView и Jetpack Compose не применяются: аватар реализуется в Unity. Требования к состояниям аватара — в `avatar_layer_architecture_v0.1.md`.

**Статус:** Архитектурный контракт (Stage 3 — Final Production Setup)  
**Версия:** 0.1  
**Дата:** 2026-10-08  
**Назначение:** Спецификация замены текстового аватара на нативный 3D-компонент SceneView (Google Filament).

---

## 1. Контракт Отображения 3D-Объекта (Jetpack Compose Integration)

Вместо текстового компонента `Text(text = "🧘")` внутрь контейнера `Box` слоя `Persona & Avatar Layer` интегрируется специализированная декларативная поверхность `ModelView`, поставляемая графическим движком Filament:

```kotlin
// Концептуальный синтаксис 3D-поверхности внутри Jetpack Compose
ModelView(
    modifier = Modifier.size(140.dp),
    modelGlbPath = "assets/models/avatar_core.glb", // Путь к glTF/GLB скину в библиотеке наборов
    animationName = when (currentVisualState) {
        TaskState.PLANNING -> "thinking"
        TaskState.WORKING -> "processing"
        TaskState.NEED_USER -> "attention"
        else -> "idle"
    },
    lifecycle = LocalLifecycleOwner.current.lifecycle
)
```

---

## 2. Инварианты управления Скелетной Анимацией (Rigging & State)

1. **Изоляция потока рендеринга:** Пересчет костей 3D-модели (Skeletal Animation blending) выполняется силами графического процессора (`GPU`) и нативного C++ конвейера Filament, полностью освобождая процессорное время `Agent Core`.
2. **Амплитудный Lip-Sync:** При активации голосового ответа или режима видеоконференции, Shell-слой перехватывает RMS-амплитуду звука и передает ее в `ModelView` для деформации ключа формы рта модели (`Blend Shapes`) на частоте 60 FPS, минуя «мозг» ИИ.
3. **Легкая замена скинов (Hot-Swapping):** Пользователь имеет право загрузить любой кастомный `.glb` файл, соответствующий стандартному костному ригу (`Humanoid Rig`). Метод `loadSkin` рантайма переинициализирует меш без перезапуска ИИ-сервисов.

---
**Статус контракта:** Утвержден. База для графического конвейера Filament зафиксирована.
