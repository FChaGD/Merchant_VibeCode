#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - Hub에서 보유 마차·시설 내구도를 −10% / +10% / 1%(0 직전)로 바꾼다(Docs/설계/91번 §7). 마구간 수리 확인용. 이벤트 파괴 확인은
    /// 설계 89 이벤트 디버그로 E3을 부른다. CaravanAssetDurabilityManager와 같은 GameObject(Bootstrap 상주)에 붙고 Hub가 로드된 동안에만 그린다.
    /// 값은 쓰기 계약으로만 바꾼다 - 매니저에 디버그 기능을 넣지 않아야 이 파일만 지워 걷어낼 수 있다.
    /// −10%로 0 이하가 되면 값을 0으로 쓰지 않고 전투와 같은 공용 파괴 처리로 보유 목록에서 뺀다 - "보유 중인데 내구도 0"은 정상 경로에
    /// 없는 상태라 다음 전투가 0으로 시작했다(2026-10-15 검진). 이벤트 파괴의 화물 이동·마지막 마차 궤주(설계 91번 묶음 D)는 하지 않는다.
    /// 걷어낼 때는 Remove 메뉴 → 이 파일·CaravanAssetDurabilityDebugInstaller.cs(+.meta) 삭제 → DebugToolsBulkInstaller 두 줄 삭제.
    /// </summary>
    public class CaravanAssetDurabilityDebugView : MonoBehaviour
    {
        private static readonly FormationUnitKind[] Kinds = { FormationUnitKind.Wagon, FormationUnitKind.Facility };
        // 화면 맨 아래 한 줄(왼쪽부터 토글 → ◀ 대상 ▶ → 버튼 3개). 마구간·용병 화면 패널은 아래 6%를 비워 두고(RosterShopUIBuilder), 720p에서도
        // 그 띠가 43px이라 높이 26 + 여백 8이 들어간다. 여러 줄 창은 마구간 보유 목록·마을 카테고리 열을 가렸다(2026-10-13 실측) - 대상은 ◀▶로 넘긴다.
        private const float Margin = 8f;
        private const float Height = 26f;
        private const float Gap = 4f;
        private const float ToggleWidth = 90f;
        private const float ArrowWidth = 26f;
        private const float LabelWidth = 220f;
        private const float ButtonWidth = 50f;
        private const float StepRatio = 0.1f;
        private const float NearZeroRatio = 0.01f;
        private static readonly string[] ButtonLabels = { "-10%", "+10%", "1%" };

        private readonly List<string> ids = new(); // OnGUI마다 새로 할당하지 않게 재사용
        private ICaravanAssetDurabilityReader reader;
        private ICaravanAssetDurabilityWriter writer;
        private IOwnedCaravanAssetReader owned;
        private CaravanAssetDestroyer destroyer;
        private bool isHubLoaded;
        private bool expanded;
        private int index;

        private void Awake()
        {
            reader = GetComponent<ICaravanAssetDurabilityReader>();
            writer = GetComponent<ICaravanAssetDurabilityWriter>();
        }

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies == null) return;
            dependencies.TryResolve(out owned);
            dependencies.TryResolve(out IFormationRepository formation);
            dependencies.TryResolve(out ICaravanRosterProvider roster);
            dependencies.TryResolve(out IFieldFormationActivityRepository activities);
            dependencies.TryResolve(out IOwnedCaravanAssetRemover remover);
            destroyer = new CaravanAssetDestroyer(formation, roster, activities, remover);
        }

        private void Decrease(string id, float current, float max)
        {
            var next = current - max * StepRatio;
            if (next > 0f) writer.TrySet(id, next);
            else destroyer?.Destroy(new[] { id });
        }

        // OnGUI는 프레임당 여러 번 불리므로 매번 씬을 조회하지 않고 로드/언로드 이벤트로 갱신한다.
        private void OnEnable()
        {
            isHubLoaded = SceneManager.GetSceneByName(SceneNames.Hub).isLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Hub) isHubLoaded = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name == SceneNames.Hub) isHubLoaded = false;
        }

        private void OnGUI()
        {
            if (!isHubLoaded) return;

            var y = Screen.height - Height - Margin;
            var x = Margin;
            if (GUI.Button(new Rect(x, y, ToggleWidth, Height), expanded ? "내구도 닫기" : "내구도")) expanded = !expanded;
            if (!expanded) return;
            x += ToggleWidth + Gap;

            if (reader == null || writer == null || owned == null)
            {
                GUI.Box(new Rect(x, y, LabelWidth, Height), "매니저 없음(Build Bootstrap)");
                return;
            }

            ids.Clear();
            foreach (var kind in Kinds) ids.AddRange(owned.GetOwnedIds(kind));
            if (ids.Count == 0)
            {
                GUI.Box(new Rect(x, y, LabelWidth, Height), "보유 마차·시설 없음");
                return;
            }
            index = (index % ids.Count + ids.Count) % ids.Count;

            if (GUI.Button(new Rect(x, y, ArrowWidth, Height), "◀")) index = (index - 1 + ids.Count) % ids.Count;
            x += ArrowWidth + Gap;
            var id = ids[index];
            owned.TryGetOwned(id, out var asset);
            var hasValue = reader.TryGet(id, out var current, out var max);
            GUI.Box(new Rect(x, y, LabelWidth, Height), hasValue ? $"{OwnedCaravanAssetNames.Format(asset)} {CaravanAssetDurabilityRules.Display(current)}/{max}" : id);
            x += LabelWidth + Gap;
            if (GUI.Button(new Rect(x, y, ArrowWidth, Height), "▶")) index = (index + 1) % ids.Count;
            x += ArrowWidth + Gap;
            if (!hasValue) return;

            if (GUI.Button(new Rect(x, y, ButtonWidth, Height), ButtonLabels[0])) Decrease(id, current, max);
            x += ButtonWidth + Gap;
            if (GUI.Button(new Rect(x, y, ButtonWidth, Height), ButtonLabels[1])) writer.TrySet(id, current + max * StepRatio);
            x += ButtonWidth + Gap;
            if (GUI.Button(new Rect(x, y, ButtonWidth, Height), ButtonLabels[2])) writer.TrySet(id, max * NearZeroRatio);
        }
    }
}
#endif
