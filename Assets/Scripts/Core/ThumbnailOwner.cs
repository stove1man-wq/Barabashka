using UnityEngine;

/// <summary>Держит текстуру и спрайт миниатюры в чате и освобождает их, когда карточка удалена (иначе память растёт).</summary>
public sealed class ThumbnailOwner : MonoBehaviour
{
    private Texture2D _texture;
    private Sprite _sprite;

    public void Init(Texture2D texture, Sprite sprite)
    {
        _texture = texture;
        _sprite = sprite;
    }

    private void OnDestroy()
    {
        if (_sprite != null) Destroy(_sprite);
        if (_texture != null) Destroy(_texture);
    }
}
