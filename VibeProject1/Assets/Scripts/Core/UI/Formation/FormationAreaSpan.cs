using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 대열 영역이 중심 칸에서 방향별로 뻗는 칸 수(Docs/기획/61번, 설계 62번 §2). 마차·시설·디버그 핀이 같은 값을 쓴다 - int 4개를 계약마다
    /// 나열하면 순서가 어긋날 여지가 생겨 한 타입으로 묶었다. 방향은 정비창 격자 화면 기준(상 = 행 감소)이며 회전하지 않는다.
    /// 음수는 여기서 한 번만 0으로 올린다 - 입력 경로(테이블·디버그 입력)마다 막지 않아도 영역 계산이 깨지지 않게.
    /// </summary>
    public readonly struct FormationAreaSpan
    {
        public int Up { get; }
        public int Down { get; }
        public int Left { get; }
        public int Right { get; }

        public FormationAreaSpan(int up, int down, int left, int right)
        {
            Up = Mathf.Max(0, up);
            Down = Mathf.Max(0, down);
            Left = Mathf.Max(0, left);
            Right = Mathf.Max(0, right);
        }

        public static FormationAreaSpan Uniform(int value) => new(value, value, value, value);

        /// <summary>네 방향 중 가장 먼 거리 - 마을 정비창 칸 표시 범위(기획 61번 §3.3).</summary>
        public int MaxReach => Mathf.Max(Mathf.Max(Up, Down), Mathf.Max(Left, Right));
    }
}
