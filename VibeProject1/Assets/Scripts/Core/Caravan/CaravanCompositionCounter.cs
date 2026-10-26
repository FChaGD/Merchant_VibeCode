using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>보유 목록의 종류별 수. 재화 HUD의 보유 현황 칸이 정비창 팔레트와 같은 목록을 같은 기준으로 센다(Docs/설계/101번 §4.2).</summary>
    public readonly struct CaravanComposition
    {
        public readonly int Wagons;
        public readonly int Facilities;
        public readonly int Characters;

        public CaravanComposition(int wagons, int facilities, int characters)
        {
            Wagons = wagons;
            Facilities = facilities;
            Characters = characters;
        }
    }

    /// <summary>
    /// 보유 목록 → 마차·시설·캐릭터 수와 HUD 표기. 용병과 시설은 실제 전투에 나서는 유닛이라 한 칸에 "용병/시설"로 묶는다(기획 98).
    /// </summary>
    public static class CaravanCompositionCounter
    {
        public static CaravanComposition Count(IReadOnlyList<IFormationUnit> roster)
        {
            int wagons = 0, facilities = 0, characters = 0;
            if (roster != null)
            {
                foreach (var unit in roster)
                {
                    if (unit == null) continue;
                    switch (unit.Kind)
                    {
                        case FormationUnitKind.Wagon: wagons++; break;
                        case FormationUnitKind.Facility: facilities++; break;
                        case FormationUnitKind.Character: characters++; break;
                    }
                }
            }
            return new CaravanComposition(wagons, facilities, characters);
        }

        public static string FormatWagons(int wagons) => wagons.ToString();

        public static string FormatUnits(int characters, int facilities) => $"{characters}/{facilities}";
    }
}
