using System;

namespace Game.Core
{
    public enum RosterEffectResult { Done, UnknownCharacter, AlreadyHired, NotHired, Failed }

    /// <summary>
    /// 이벤트 합류·이탈(설계 89 §5.1·§5.5). 합류는 고용 명단에만 넣는다(자동 배치 없음 - 정비창 팔레트에 나타남, 상한 없음 기획 80 §3-10). 이탈은 진행 중
    /// 배치·이동을 취소하고 대열 칸을 비운 뒤 명단에서 뺀다 - 고용 후보 복귀는 후보 제공자가 명단을 보고 자연히 처리한다. 배치 활동은 Field 전용이라
    /// 저장소 대신 조회·취소 함수만 받는다(없으면 건너뜀). 실행 불가는 결과 값으로 알리고 호출자가 경고 후 건너뛴다.
    /// </summary>
    public sealed class EventRosterEffects
    {
        private readonly ICharacterCatalogReader catalog;
        private readonly IHiredCharacterRoster roster;
        private readonly IHiredCharacterRemover remover;
        private readonly IFormationRepository formation;
        private readonly Func<string, bool> isActivityBusy;
        private readonly Action<string> cancelActivity;

        public EventRosterEffects(ICharacterCatalogReader catalog, IHiredCharacterRoster roster, IHiredCharacterRemover remover, IFormationRepository formation,
            Func<string, bool> isActivityBusy, Action<string> cancelActivity)
        {
            this.catalog = catalog;
            this.roster = roster;
            this.remover = remover;
            this.formation = formation;
            this.isActivityBusy = isActivityBusy ?? (_ => false);
            this.cancelActivity = cancelActivity ?? (_ => { });
        }

        public bool IsHired(string characterId) => roster != null && roster.IsHired(characterId);

        public string NameOf(string characterId) => catalog != null && catalog.TryGet(characterId, out var profile) ? profile.Name : characterId;

        public RosterEffectResult Join(string characterId)
        {
            if (catalog == null || characterId == null || !catalog.TryGet(characterId, out _)) return RosterEffectResult.UnknownCharacter;
            if (roster == null) return RosterEffectResult.Failed;
            if (roster.IsHired(characterId)) return RosterEffectResult.AlreadyHired;
            return roster.TryAddHired(characterId) ? RosterEffectResult.Done : RosterEffectResult.Failed;
        }

        public RosterEffectResult Leave(string characterId)
        {
            if (roster == null || remover == null) return RosterEffectResult.Failed;
            if (!roster.IsHired(characterId)) return RosterEffectResult.NotHired;
            if (isActivityBusy(characterId)) cancelActivity(characterId);
            FormationLayoutClearing.ClearUnits(formation, new[] { characterId });
            return remover.TryRemoveHired(characterId) ? RosterEffectResult.Done : RosterEffectResult.Failed;
        }
    }
}
