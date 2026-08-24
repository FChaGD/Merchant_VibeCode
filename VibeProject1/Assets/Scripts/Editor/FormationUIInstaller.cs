using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 씬에 배치(Formation) UI 하이어라키를 생성/동기화한다. 실제 조립 로직은 FormationUIBuilder(Hub/Field
    /// 공용)에 있다 - Field 씬에서도 "정비창 재호출"이 동작하려면 같은 화면이 필요해서 공용화했다
    /// (FieldUIInstaller 참고). 이 파일은 Hub 씬 검증 + 프리팹 준비 + 빌더 호출만 담당한다.
    /// </summary>
    public static class FormationUIInstaller
    {
        [MenuItem("Tools/Game/Build Formation UI")]
        public static void BuildFormationUI()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.Hub)
            {
                Debug.LogError($"'{SceneNames.Hub}' 씬이 활성 씬이어야 한다. 현재 활성 씬: '{activeScene.name}'. Hub.unity를 열고 다시 실행하라.");
                return;
            }

            var sceneUIRoot = Object.FindFirstObjectByType<SceneUIRoot>(FindObjectsInactive.Include);
            if (sceneUIRoot == null)
            {
                Debug.LogError($"씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            // 그리드 드래그 등 UI 입력이 정상 동작하려면 이 씬 자신의 EventSystem이 있어야 한다 -
            // 없으면 만든다(SceneLoader가 전환 시 중복을 방지한다. EditorUIBuilder 참고).
            EditorUIBuilder.EnsureSceneEventSystem(activeScene);

            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();

            FormationUIBuilder.Build(sceneUIRoot, slotPrefab, iconPrefab);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Formation UI 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + "FormationPanel.dragGhostPrefab에는 'Assets/Prefabs/UI/Formation/FormationUnitIcon.prefab'을 "
                + "수동으로 연결하라(FormationPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다).");
        }
    }
}
