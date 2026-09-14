using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SpatialGridGizmo.cs
//  UnitTargetSearchSystem 이 사용하는 공간 분할 Grid 를 Scene 뷰에 시각화.
//
//  사용법:
//    빈 GameObject 에 이 컴포넌트를 추가하면 됨.
//
//  표시 내용:
//    - 회색 선  : Grid 격자 (CellSize = 3)
//    - 파란 셀  : 아군 유닛이 있는 셀
//    - 빨간 셀  : 적군 유닛이 있는 셀
//    - 노란 셀  : 양 팀 모두 있는 셀
//    - 셀 좌표  : 각 셀의 int2 좌표 (ShowCellCoords 활성 시)
// ============================================================

public class SpatialGridGizmo : MonoBehaviour
{
    [Header("표시 범위")]
    [Tooltip("카메라 중심 기준 가로 셀 개수 (양방향)")]
    public int DrawRadiusX = 8;
    [Tooltip("카메라 중심 기준 세로 셀 개수 (양방향)")]
    public int DrawRadiusY = 5;

    [Header("표시 옵션")]
    public bool ShowGridLines    = true;
    public bool ShowOccupiedCells = true;
    public bool ShowCellCoords   = false;

    [Header("색상")]
    public Color GridLineColor  = new Color(1f, 1f, 1f, 0.12f);
    public Color AllyColor      = new Color(0.2f, 0.5f, 1f,  0.25f);
    public Color EnemyColor     = new Color(1f,  0.2f, 0.2f, 0.25f);
    public Color MixedColor     = new Color(1f,  0.9f, 0.1f, 0.3f);
    public Color CoordTextColor = new Color(1f,  1f,   1f,   0.6f);

    const float CellSize = UnitGridConstants.CellSize;

    void OnDrawGizmos()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;

        // 카메라 위치를 셀 좌표로 변환 → 중심 셀
        int2 centerCell = WorldToCell(new float3(camPos.x, camPos.y, 0f));

        // ── 점유 셀 수집 (플레이 중에만) ──────────────────────────
        System.Collections.Generic.Dictionary<int2, (bool hasAlly, bool hasEnemy)> occupied = null;

        if (ShowOccupiedCells && Application.isPlaying)
            occupied = CollectOccupiedCells();

        // ── 격자 그리기 ───────────────────────────────────────────
        int x0 = centerCell.x - DrawRadiusX;
        int x1 = centerCell.x + DrawRadiusX;
        int y0 = centerCell.y - DrawRadiusY;
        int y1 = centerCell.y + DrawRadiusY;

        // 점유 셀 색칠
        if (occupied != null)
        {
            foreach (var kv in occupied)
            {
                int2 cell = kv.Key;
                if (cell.x < x0 || cell.x > x1 || cell.y < y0 || cell.y > y1) continue;

                bool ally  = kv.Value.hasAlly;
                bool enemy = kv.Value.hasEnemy;
                Gizmos.color = (ally && enemy) ? MixedColor : ally ? AllyColor : EnemyColor;

                Vector3 cellCenter = CellCenter(cell);
                Gizmos.DrawCube(cellCenter, new Vector3(CellSize - 0.05f, CellSize - 0.05f, 0f));
            }
        }

        // 격자 선
        if (ShowGridLines)
        {
            Gizmos.color = GridLineColor;

            // 세로선
            for (int x = x0; x <= x1 + 1; x++)
            {
                float wx = x * CellSize;
                float wy0 = y0 * CellSize;
                float wy1 = (y1 + 1) * CellSize;
                Gizmos.DrawLine(new Vector3(wx, wy0, 0f), new Vector3(wx, wy1, 0f));
            }

            // 가로선
            for (int y = y0; y <= y1 + 1; y++)
            {
                float wy = y * CellSize;
                float wx0 = x0 * CellSize;
                float wx1 = (x1 + 1) * CellSize;
                Gizmos.DrawLine(new Vector3(wx0, wy, 0f), new Vector3(wx1, wy, 0f));
            }
        }

#if UNITY_EDITOR
        // 셀 좌표 텍스트
        if (ShowCellCoords)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = CoordTextColor;
            style.fontSize = 9;
            style.alignment = TextAnchor.MiddleCenter;

            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            {
                Vector3 center = CellCenter(new int2(x, y));
                UnityEditor.Handles.Label(center, $"{x},{y}", style);
            }
        }
#endif
    }

    // ── ECS 에서 유닛 셀 수집 ────────────────────────────────────

    static System.Collections.Generic.Dictionary<int2, (bool, bool)> CollectOccupiedCells()
    {
        var result = new System.Collections.Generic.Dictionary<int2, (bool hasAlly, bool hasEnemy)>();

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return result;

        EntityManager em = world.EntityManager;

        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<GridCellComponent>(),
            ComponentType.ReadOnly<UnitIdentityComponent>(),
            ComponentType.Exclude<DeadTag>());

        var cells      = query.ToComponentDataArray<GridCellComponent>  (Unity.Collections.Allocator.Temp);
        var identities = query.ToComponentDataArray<UnitIdentityComponent>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < cells.Length; i++)
        {
            int2    cell = cells[i].Cell;
            TeamType team = identities[i].Team;

            result.TryGetValue(cell, out var cur);
            bool ally  = cur.hasAlly  || team == TeamType.Ally;
            bool enemy = cur.hasEnemy || team == TeamType.Enemy;
            result[cell] = (ally, enemy);
        }

        cells.Dispose();
        identities.Dispose();
        query.Dispose();

        return result;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static int2    WorldToCell(float3 pos) => (int2)math.floor(pos.xy / CellSize);
    static Vector3 CellCenter(int2 cell)   => new Vector3((cell.x + 0.5f) * CellSize,
                                                           (cell.y + 0.5f) * CellSize, 0f);
}
