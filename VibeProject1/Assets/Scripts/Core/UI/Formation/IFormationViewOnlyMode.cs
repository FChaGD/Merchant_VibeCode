namespace Game.Core
{
    /// <summary>정비창 열람 모드 전환(설계 89 §6.3) - 이벤트 화면 묶음만 쓴다. 정리 모드(IFormationRepairMode)와 계약을 나눈 이유: 쓰는 곳과 수명이 다르다.</summary>
    public interface IFormationViewOnlyMode
    {
        void SetViewOnly(bool viewOnly);
    }
}
