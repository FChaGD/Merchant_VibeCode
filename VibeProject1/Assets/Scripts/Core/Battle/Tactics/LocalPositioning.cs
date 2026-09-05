namespace Game.Core
{
    /// <summary>
    /// 역할군 축 - 활동 반경 안에서 전투 중 실제로 이동하는 세부 로직(Docs/기획/12번 §3.2).
    /// 명시적 정수값 = 데이터 테이블 Id(Docs/설계/18번 §3), 0은 비워둔다.
    /// </summary>
    public enum LocalPositioning
    {
        // 원거리딜러 - 사거리 밖이면 접근, 안이면 정지(Docs/기획/17번). 전열 Charge와 로직이
        // 완전히 같아 PositioningStrategyFactory가 같은 ChargePositioningStrategy 인스턴스를
        // 반환한다(클래스를 새로 만들지 않음, Docs/설계/22번 §3).
        ApproachAttack = 1,
        // 전열 - 적에게 직진 접근(기존 기본 동작).
        Charge = 2,
        // 전열 - 아군과 적 사이에 위치해 전열 형성.
        Blocking = 3,
        // 원거리딜러 - 제자리에서 사격.
        Stationary = 4,
        // 원거리딜러 - 같은 타겟을 노리는 동료와 각도를 나눠 타겟을 감싸듯 자리잡음(Docs/설계/12번 §13.3).
        // 기존 "사거리 유지형"을 대체(자기보호 카이팅과 개념 중복으로 폐기, Docs/기획/12번 §3.2-2).
        Surround = 5,
        // 원거리딜러 - 동료 원거리딜러와 일정 거리 이상 벌어져 사격(Docs/설계/12번 §13.2).
        Disperse = 6,
    }
}
