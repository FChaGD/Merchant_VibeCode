using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Editor
{
    /// <summary>
    /// 프로젝트 전용 입력 액션 자산을 전역 액션(InputSystem.actions)으로 지정하고 템플릿 자산을 지운다.
    /// 전역 액션 지정은 ProjectSettings(EditorBuildSettings)에 저장되므로 텍스트 편집 대신 이 메뉴로만 바꾼다.
    /// InputSystem.actions 설정은 Edit 모드에서만 허용돼 Play 중에는 중단한다(Docs/설계/99번 §6.1).
    /// </summary>
    public static class GameInputInstaller
    {
        private const string AssetPath = "Assets/Settings/Input/GameInput.inputactions";
        private const string TemplateAssetPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Tools/Game/Input/Register Game Input")]
        public static void RegisterGameInput()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GameInputInstaller] Play 중에는 전역 입력 액션을 지정할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (asset == null)
            {
                Debug.LogError($"[GameInputInstaller] 입력 액션 자산이 없습니다: {AssetPath}");
                return;
            }

            if (InputSystem.actions != asset)
                InputSystem.actions = asset;

            bool templateDeleted = AssetDatabase.LoadAssetAtPath<InputActionAsset>(TemplateAssetPath) != null
                && AssetDatabase.DeleteAsset(TemplateAssetPath);

            Debug.Log($"[GameInputInstaller] 전역 입력 액션 = {InputSystem.actions?.name}" + (templateDeleted ? ", 템플릿 자산 삭제" : string.Empty));
        }
    }
}
