using UnityEngine;

/// <summary>
/// 씬 전환 시 오브젝트가 파괴되지 않도록 DontDestroyOnLoad 를 적용하는 컴포넌트.
/// 루트 오브젝트에만 배치할 것 (Unity 제한).
/// </summary>
public class DontDestroyOnLoad : MonoBehaviour
{
    void Awake()
    {
        if (transform.parent != null)
        {
            Debug.LogWarning($"[DontDestroyOnLoad] '{name}' 은 루트 오브젝트여야 합니다.", this);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }
}
