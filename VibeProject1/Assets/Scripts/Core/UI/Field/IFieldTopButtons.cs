namespace Game.Core
{
    /// <summary>
    /// Field 상단 재호출 버튼(정비창·방향성 지시·대화)의 활성 일괄 설정. 씬 전환 커튼(사용자 확정) 외에 인카운터~전투 진행 중에도 막는다(사용자 확정,
    /// 2026-09-07) - 인카운터 흐름이 발생 시 끄고, 결과 정리 마무리·이벤트 화면이 되돌린다. 이동 화면 조율자 계약에서 떼어 냈다(Docs/Refactor/2026-10-26_씬전용.md SS-2).
    /// </summary>
    public interface IFieldTopButtons
    {
        void SetTopLevelButtonsInteractable(bool interactable);
    }
}
