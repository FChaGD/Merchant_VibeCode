using System;

namespace Game.Core
{
    /// <summary>
    /// 로스터 개체(IFormationUnit)를 배치 상한 계산용 카테고리로 묶는 키(설계 16번) - 캐릭터는
    /// 직업(MercenaryClass)까지, 그 외(마차/시설)는 종류(Kind)만으로 구분한다. 정비창 팔레트가
    /// "개체 하나당 아이콘 하나"가 아니라 "카테고리당 1줄+잔여수"로 보여줘야 해서 필요해졌다
    /// (기획 11번 §4).
    /// </summary>
    public readonly struct FormationCategoryKey : IEquatable<FormationCategoryKey>
    {
        public FormationUnitKind Kind { get; }

        /// <summary>Kind가 Character일 때만 값이 있다 - 그 외엔 null(N/A).</summary>
        public MercenaryClass? MercenaryClass { get; }

        private FormationCategoryKey(FormationUnitKind kind, MercenaryClass? mercenaryClass)
        {
            Kind = kind;
            MercenaryClass = mercenaryClass;
        }

        public static FormationCategoryKey Of(IFormationUnit unit) =>
            unit is IMercenaryUnit mercenary
                ? new FormationCategoryKey(FormationUnitKind.Character, mercenary.Class)
                : new FormationCategoryKey(unit.Kind, null);

        public bool Equals(FormationCategoryKey other) => Kind == other.Kind && MercenaryClass == other.MercenaryClass;

        public override bool Equals(object obj) => obj is FormationCategoryKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, MercenaryClass);
    }
}
