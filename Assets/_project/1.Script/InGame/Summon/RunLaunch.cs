using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  RunLaunch.cs
//  "어느 소환사로 런을 시작할 것인가" 를 선택 화면 → 인게임으로 넘기는 창구.
//
//  ■ 왜 필요한가 — 두 씬이 동시에 살아 있다
//    Splash 가 Lobby 와 InGame 을 **둘 다 additive 로** 올린다. 그래서
//    RunBootstrap.Start 는 플레이어가 캐릭터를 고르기도 전에 돌아 버린다.
//    (예전에는 그 자리에서 아무 소환사나 자동으로 집어 세웠다)
//
//    여기에 요청이 들어올 때까지 RunBootstrap 이 기다리게 해서 순서를 바로잡는다.
//
//  ■ ⚠ 인게임 씬만 열어도 게임이 돌아가야 한다
//    에디터에서 InGame.unity 만 열고 재생하는 것이 일상적인 확인 방법이다.
//    그때는 고를 화면이 없으므로 **기다리지 않고** 자동으로 시작해야 한다.
//    그 판단이 LobbyPresent — 로비 씬이 올라와 있는지로 가른다.
//
//  ■ 세이브에 남기지 않는다
//    선택은 런 한 번의 입력이지 저장할 상태가 아니다. 게임을 껐다 켜면
//    다시 고르는 것이 맞다.
// ============================================================

public static class RunLaunch
{
    /// <summary>이 이름의 씬이 올라와 있으면 "고를 화면이 있다" 로 본다.</summary>
    const string LobbySceneName = "Lobby";

    /// <summary>선택된 소환사 ID. 비어 있으면 아직 고르지 않았다.</summary>
    public static string SummonerId { get; private set; }

    /// <summary>시작이 요청됐는가.</summary>
    public static bool Requested { get; private set; }

    /// <summary>
    /// 선택 화면이 있는가.
    ///
    /// ⚠ 매번 다시 본다 — 캐시하지 않는다
    ///   씬은 런타임에 올라오고 내려간다. 부팅 순간의 값을 붙들면
    ///   "로비가 나중에 올라온" 경우를 놓친다.
    /// </summary>
    public static bool LobbyPresent
    {
        get
        {
            Scene lobby = SceneManager.GetSceneByName(LobbySceneName);
            return lobby.IsValid() && lobby.isLoaded;
        }
    }

    /// <summary>
    /// 선택 화면이 소환사를 확정했다. RunBootstrap 이 이 신호를 기다린다.
    /// </summary>
    public static void Request(string summonerId)
    {
        SummonerId = summonerId;
        Requested  = true;

        Debug.Log($"[RunLaunch] 런 시작 요청 — 소환사 '{summonerId}'");
    }

    /// <summary>
    /// 저장된 런을 이어서 시작해 달라 — **소환사를 지정하지 않는다.**
    ///
    /// ⚠ Request(id) 를 쓰면 안 된다
    ///   RunBootstrap 은 "SummonerId 가 비어 있는가" 로 새 런과 이어하기를
    ///   가른다. 여기서 ID 를 실어 보내면 새 런으로 읽혀 **마나가 다시 지급되고**
    ///   시작 카드가 다시 깔린다 — 죽기 직전에 앱을 껐다 켜면 회복되는 셈이다.
    ///   무엇으로 이어할지는 세이브(SummonRunData)가 이미 알고 있다.
    /// </summary>
    public static void RequestResume()
    {
        SummonerId = null;
        Requested  = true;

        Debug.Log("[RunLaunch] 이어하기 요청 — 저장된 런을 복원합니다.");
    }

    /// <summary>
    /// 요청을 소비한다. RunBootstrap 이 실제로 런을 세운 뒤 부른다.
    ///
    /// ⚠ 비워 두지 않으면 다음 런이 지난 선택을 물려받는다
    ///   패배 후 선택 화면으로 돌아갔을 때, 요청이 남아 있으면 고르기도 전에
    ///   인게임이 다시 시작된다.
    /// </summary>
    public static void Consume() => Requested = false;

    /// <summary>선택 자체를 지운다 (새 게임·타이틀 복귀).</summary>
    public static void Clear()
    {
        SummonerId = null;
        Requested  = false;
    }
}
