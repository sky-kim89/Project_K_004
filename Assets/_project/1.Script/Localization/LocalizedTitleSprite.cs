using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class LocalizedTitleSprite : MonoBehaviour
{
    [SerializeField] Sprite _korean;
    [SerializeField] Sprite _english;

    Image _image;

    void Awake() => _image = GetComponent<Image>();

    void OnEnable()
    {
        LocalizationManager.Instance.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        LocalizationManager.Instance.LanguageChanged -= Refresh;
    }

    public void Configure(Sprite korean, Sprite english)
    {
        _image ??= GetComponent<Image>();
        _korean = korean;
        _english = english;
        _image.color = Color.white;
        Refresh();
    }

    void Refresh()
    {
        _image.sprite = LocalizationManager.Instance.CurrentLanguage == GameLanguage.Korean
            ? _korean
            : _english;
    }
}
