using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 정비창 그리드 위에 이동 경로 하나를 얇은 선분들로 이어 그린다(Docs/기획/20번 §3.3, 설계 25번
    /// §5.2). FormationGridView의 경로선 컨테이너(slotContent와 같은 영역을 덮는 자식) 아래 놓여 anchoredPosition
    /// 좌표계가 슬롯 위치와 일치한다. 세그먼트는 get-or-create로 재사용한다. 이동 중인 유닛 아이콘은
    /// 이 클래스가 아니라 FormationGridView가 별도 컨테이너로 관리한다 - 선은 항상 슬롯 위·오버레이 아래,
    /// 이동 아이콘은 항상 그 위에 그려야 해서(실전 확인된 렌더 순서 요구사항) 종류별 컨테이너의 형제 순서로
    /// 고정하기 때문이다(같은 오브젝트 안에 있으면 분리할 수 없다, Docs/Refactor/2026-10-26_공통.md A-1).
    /// </summary>
    public class FormationPathLineView : MonoBehaviour
    {
        [SerializeField] private Image segmentPrefab;
        [SerializeField] private float thickness = 6f;

        private readonly List<Image> segments = new();

        public void SetPath(IReadOnlyList<Vector2> waypointAnchoredPositions)
        {
            var neededSegments = waypointAnchoredPositions == null ? 0 : Mathf.Max(0, waypointAnchoredPositions.Count - 1);
            EnsureSegmentCount(neededSegments);

            for (var i = 0; i < neededSegments; i++)
            {
                PlaceSegment(segments[i], waypointAnchoredPositions[i], waypointAnchoredPositions[i + 1]);
            }
        }

        public void Clear() => SetPath(Array.Empty<Vector2>());

        private void EnsureSegmentCount(int count)
        {
            while (segments.Count < count)
            {
                segments.Add(segmentPrefab != null ? Instantiate(segmentPrefab, transform) : CreateFallbackSegment());
            }

            for (var i = 0; i < segments.Count; i++)
            {
                segments[i].gameObject.SetActive(i < count);
            }
        }

        private void PlaceSegment(Image segment, Vector2 from, Vector2 to)
        {
            var rect = (RectTransform)segment.transform;
            var delta = to - from;
            var distance = delta.magnitude;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(distance, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Image CreateFallbackSegment()
        {
            var go = new GameObject("PathSegment", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 1f); // 불투명(사용자 확정)
            image.raycastTarget = false;
            return image;
        }
    }
}
