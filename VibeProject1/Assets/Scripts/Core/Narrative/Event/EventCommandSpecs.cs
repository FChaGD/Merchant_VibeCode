using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>효과 명령 인자 종류(설계 89 §5.6). 품목·캐릭터 존재는 검증기가 테이블 조회 함수로 확인한다.</summary>
    public enum EventArgKind { Int, PositiveInt, Seconds, ItemId, ItemIdOrRandom, CharacterId, DifficultyMode }

    /// <summary>명령을 쓸 수 있는 이벤트 장소(설계 109 §3.4) - 상행 시계·도로·전투가 없는 휴식에서 상행 명령을 막는다.</summary>
    public enum EventCommandPlaces { Trip, Rest, Both }

    public sealed class EventCommandSpec
    {
        public string Name { get; }
        /// <summary>검증 오류에 보여 줄 작성 형식.</summary>
        public string Usage { get; }
        public int MinArgs { get; }
        public IReadOnlyList<EventArgKind> Args { get; }
        public EventCommandPlaces Places { get; }

        public EventCommandSpec(string name, string usage, int minArgs, params EventArgKind[] args) : this(name, usage, EventCommandPlaces.Both, minArgs, args) { }

        public EventCommandSpec(string name, string usage, EventCommandPlaces places, int minArgs, params EventArgKind[] args)
        {
            Places = places;
            Name = name;
            Usage = usage;
            MinArgs = minArgs;
            Args = args;
        }
    }

    public sealed class EventFunctionSpec
    {
        public string Name { get; }
        public Type Returns { get; }
        public IReadOnlyList<Type> Parameters { get; }

        public EventFunctionSpec(string name, Type returns, params Type[] parameters)
        {
            Name = name;
            Returns = returns;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// 효과 명령·조건 함수 명세(설계 89 §5.6, 2026-10-16 검진). 효과 등록(EventEffectCommands)·대본 검증(EncounterContentValidator)·Yarn 컴파일 선언
    /// (EventYarnFunctionDeclarations)이 모두 이 표를 읽는다 - 세 곳에 따로 적으면 명령 하나를 더할 때 하나가 빠져 Yarn 컴파일 오류나 검증 오탐이 났다.
    /// 명세는 "무엇을 받는가"만 갖고, 인자 해석·효과 적용은 명령별 메서드가 맡는다. 등록 결과와의 일치는 EditMode 테스트가 확인한다.
    /// </summary>
    public static class EventCommandSpecs
    {
        public const string Battle = "battle";
        public const string Gold = "gold";
        public const string GoodsAdd = "goods_add";
        public const string GoodsRemove = "goods_remove";
        public const string Delay = "delay";
        public const string Difficulty = "difficulty";
        public const string Join = "join";
        public const string Leave = "leave";
        public const string RestEnd = "rest_end";

        public const string GoldFunction = "gold";
        public const string GoodsFunction = "goods";
        public const string HiredFunction = "hired";

        public static readonly IReadOnlyList<EventCommandSpec> Commands = new[]
        {
            new EventCommandSpec(Battle, "<<battle>>", EventCommandPlaces.Trip, 0),
            new EventCommandSpec(Gold, "<<gold 정수>>", 1, EventArgKind.Int),
            new EventCommandSpec(GoodsAdd, "<<goods_add 품목Id 양수>>", 2, EventArgKind.ItemId, EventArgKind.PositiveInt),
            new EventCommandSpec(GoodsRemove, "<<goods_remove 품목Id|random 양수>>", 2, EventArgKind.ItemIdOrRandom, EventArgKind.PositiveInt),
            new EventCommandSpec(Delay, "<<delay 초>>", EventCommandPlaces.Trip, 1, EventArgKind.Seconds),
            // days 방식만 3번째 인자(일수)를 받는다 - 방식별 개수는 DifficultyArgCount.
            new EventCommandSpec(Difficulty, "<<difficulty 증감 leg|permanent>> 또는 <<difficulty 증감 days 양수>>", EventCommandPlaces.Trip, 2,
                EventArgKind.Int, EventArgKind.DifficultyMode, EventArgKind.PositiveInt),
            new EventCommandSpec(Join, "<<join 캐릭터Id>>", 1, EventArgKind.CharacterId),
            new EventCommandSpec(Leave, "<<leave 캐릭터Id>>", 1, EventArgKind.CharacterId),
            // 휴식 강제 종료(설계 109 §5.4) - 그날을 미성립으로 끝낸다. 대본은 끝까지 진행한다.
            new EventCommandSpec(RestEnd, "<<rest_end>>", EventCommandPlaces.Rest, 0),
        };

        public static readonly IReadOnlyList<EventFunctionSpec> Functions = new[]
        {
            new EventFunctionSpec(GoldFunction, typeof(int)),
            new EventFunctionSpec(GoodsFunction, typeof(int), typeof(string)),
            new EventFunctionSpec(HiredFunction, typeof(bool), typeof(string)),
        };

        public static bool TryGetCommand(string name, out EventCommandSpec spec)
        {
            foreach (var candidate in Commands)
            {
                if (candidate.Name != name) continue;
                spec = candidate;
                return true;
            }
            spec = null;
            return false;
        }

        public static bool IsAllowedAt(EventCommandSpec spec, EventPlace place)
            => spec.Places == EventCommandPlaces.Both || (spec.Places == EventCommandPlaces.Rest) == (place == EventPlace.Rest);

        /// <summary>인자 종류가 Yarn 명령 처리기에서 받는 형식.</summary>
        public static Type ClrTypeOf(EventArgKind kind) => kind switch
        {
            EventArgKind.Int or EventArgKind.PositiveInt => typeof(int),
            EventArgKind.Seconds => typeof(float),
            _ => typeof(string),
        };

        /// <summary>difficulty 방식별 인자 개수 - leg·permanent 2개, days 3개.</summary>
        public static int DifficultyArgCount(string mode) => mode == EventRoadDifficultyEffects.DaysMode ? 3 : 2;
    }
}
