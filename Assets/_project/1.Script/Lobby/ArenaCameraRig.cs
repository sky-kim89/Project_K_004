using System.Collections;
using UnityEngine;

// ============================================================
//  ArenaCameraRig.cs
//  전장 카메라를 옮기는 최소한의 도구.
//
//  ■ 좌표를 상수로 박지 않는다
//    전투 뷰의 정답은 씬에 이미 있다 — InGame 씬 카메라가 놓인 그 자리다.
//    처음 상주할 때 그 값을 '집(Home)' 으로 기억해 두고,
//    대기 뷰는 집에서 얼마나 옆으로 밀지로만 표현한다.
//    이렇게 해야 씬에서 카메라를 옮겨도 코드가 따라온다.
//
//  ■ 시간은 unscaled 로 센다
//    배속·일시정지가 timeScale 을 건드리므로 연출이 같이 느려지면 안 된다.
// ============================================================

public static class ArenaCameraRig
{
    static bool  _homeSet;
    static float _homeX;
    static float _homeSize;

    /// <summary>전투 뷰(씬에 놓인 원래 카메라 위치)를 기억한다. 처음 한 번만 먹는다.</summary>
    public static void CaptureHome(Camera cam)
    {
        if (_homeSet || cam == null) return;
        _homeX    = cam.transform.position.x;
        _homeSize = cam.orthographicSize;
        _homeSet  = true;
    }

    public static float HomeX    => _homeX;
    public static float HomeSize => _homeSize;

    /// <summary>즉시 이동 (연출 없이 자리만 잡을 때).</summary>
    public static void Snap(Camera cam, float x, float orthoSize)
    {
        if (cam == null) return;

        Vector3 p = cam.transform.position;
        p.x = x;
        cam.transform.position = p;

        if (orthoSize > 0f) cam.orthographicSize = orthoSize;
    }

    /// <summary>부드럽게 이동 — 대기 뷰 → 전투 뷰 무빙에 쓴다.</summary>
    public static IEnumerator MoveTo(Camera cam, float x, float orthoSize, float seconds)
    {
        if (cam == null) yield break;
        if (seconds <= 0f) { Snap(cam, x, orthoSize); yield break; }

        float startX    = cam.transform.position.x;
        float startSize = cam.orthographicSize;
        float endSize   = orthoSize > 0f ? orthoSize : startSize;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;

            // 부드럽게 출발해 부드럽게 멈춘다 (SmoothStep)
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));

            Vector3 p = cam.transform.position;
            p.x = Mathf.Lerp(startX, x, k);
            cam.transform.position = p;
            cam.orthographicSize   = Mathf.Lerp(startSize, endSize, k);

            yield return null;
        }

        Snap(cam, x, endSize);
    }
}
