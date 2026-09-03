namespace Game.Core
{
    // 명시적 정수값 = 데이터 테이블 Id(Docs/설계/18번 §3) - 멤버 이름을 바꿔도 이 값은 유지해야
    // 엑셀 데이터가 깨지지 않는다. 0은 비워둔다(미초기화 필드와 유효 Id를 구분하기 위함).
    public enum MercenaryClass
    {
        Warrior = 1,
        Archer = 2,
        ShieldBearer = 3,
    }
}
