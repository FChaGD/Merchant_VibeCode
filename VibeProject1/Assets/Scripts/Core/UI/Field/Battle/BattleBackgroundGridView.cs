using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 배경을 그리드 타일(SpriteRenderer)로 깐다(Docs/설계/13번, 사용자 확정) - 하나의 큰 배경
    /// 오브젝트 대신 개별 타일로 나눠, 나중에 특정 타일의 스프라이트/색만 바꿔도(지형 종류 등) 이
    /// 시스템 자체는 건드리지 않아도 되게 한다. 배치 영역은 적 스폰 링(BattleSimulationLoop.
    /// SpawnRadius)을 전부 감싸는 정사각형이다 - 그보다 좁으면 카메라가 스폰 지점 쪽으로 팬할 때
    /// 타일 밖(빈 배경)이 드러난다. 전투마다 크기가 달라지므로(대형 크기에 연동) 타일을 매번
    /// Destroy+Instantiate하지 않고 재사용 풀로 관리한다(CLAUDE.md 최적화 규칙).
    /// </summary>
    public class BattleBackgroundGridView : MonoBehaviour
    {
        private const float TileSize = 2f; // 사용자 확정(Docs/설계/13번) - 오브젝트 수/디테일의 절충.
        // 유닛(Y기반 정렬, BattleCharacterUnitView 기준 대략 수백~수천 범위)보다 항상 아래에
        // 그려지도록 충분히 작은 고정값 - 타일끼리는 정렬 순서를 다툴 필요가 없어 전부 동일하다.
        // SpriteRenderer.sortingOrder는 public API는 int지만 실제로는 short(-32768~32767) 범위만
        // 유효하다 - 처음에 -100000을 넣었더니 16비트로 잘리며 부호가 뒤집혀(+31072대) 타일이
        // 오히려 유닛보다 앞에 그려지는 버그가 실전투에서 확인됐다(2026-08-29). 유효 범위 안에서
        // 충분히 작은 값으로 교체.
        private const int TileSortingOrder = -30000;
        // 실제 아트가 준비되면 이 색만 바꾸거나, 개별 타일의 SpriteRenderer.sprite/color를 직접
        // 바꾸면 된다(Placeholder 컨벤션) - 풀 안의 모든 타일이 이 색으로 초기화된다.
        private static readonly Color DefaultTileColor = new(0.32f, 0.4f, 0.28f, 1f);

        private readonly List<SpriteRenderer> tilePool = new();
        private Transform tileParent;
        private int battleLayer;

        private void Awake()
        {
            battleLayer = LayerMask.NameToLayer(BattleFieldGeometry.BattleLayerName);
            tileParent = new GameObject("BackgroundLayer").transform;
            tileParent.SetParent(transform, false);
        }

        /// <summary>
        /// 전투 시작마다 BattleViewPresenter가 호출한다 - 스폰 링 전체를 감싸는 정사각형 그리드를
        /// 다시 잡는다. 필요한 타일 수가 이전 전투보다 늘면 풀에 추가로 만들고, 줄면 남는 타일은
        /// 재사용을 위해 숨기기만 한다(파괴하지 않음).
        /// </summary>
        public void ConfigureField(float spawnRadius)
        {
            var tilesPerAxis = Mathf.Max(1, Mathf.CeilToInt(spawnRadius * 2f / TileSize));
            var neededCount = tilesPerAxis * tilesPerAxis;

            while (tilePool.Count < neededCount)
            {
                tilePool.Add(CreateTile());
            }

            var index = 0;
            for (var row = 0; row < tilesPerAxis; row++)
            {
                for (var col = 0; col < tilesPerAxis; col++)
                {
                    var tile = tilePool[index++];
                    var x = (col - (tilesPerAxis - 1) / 2f) * TileSize;
                    var y = (row - (tilesPerAxis - 1) / 2f) * TileSize;
                    tile.transform.position = new Vector3(x, y, 0f);
                    tile.gameObject.SetActive(true);
                }
            }

            for (; index < tilePool.Count; index++)
            {
                tilePool[index].gameObject.SetActive(false);
            }
        }

        private SpriteRenderer CreateTile()
        {
            var go = new GameObject("Tile", typeof(SpriteRenderer));
            go.transform.SetParent(tileParent, false);
            go.layer = battleLayer;
            go.transform.localScale = Vector3.one * TileSize;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = BattlePlaceholderSprite.WhiteSquare;
            renderer.color = DefaultTileColor;
            renderer.sortingOrder = TileSortingOrder;
            return renderer;
        }
    }
}
