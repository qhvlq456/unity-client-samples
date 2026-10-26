using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClientSamples.UI
{
    /// <summary>
    /// 보이는 개수 + 여유분만 생성해서 재사용하는 세로 스크롤 리스트.
    /// 데이터 1만 개도 셀은 화면에 보이는 만큼만 존재한다.
    /// - Layout Group / Content Size Fitter를 쓰지 않고 위치를 직접 계산해 리빌드 비용을 줄인다.
    /// - 셀은 Bind(index)만 받고 상태는 데이터 쪽이 가진다 (재사용해도 상태가 섞이지 않음).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class RecycleScrollView : MonoBehaviour
    {
        [SerializeField] private RectTransform cellPrefab;
        [SerializeField] private float cellHeight = 120f;
        [SerializeField] private float spacing = 8f;
        [SerializeField] private int bufferCount = 2;

        private ScrollRect scrollRect;
        private RectTransform content;
        private readonly LinkedList<RecycleCell> activeCells = new LinkedList<RecycleCell>();
        private readonly Stack<RecycleCell> pool = new Stack<RecycleCell>();

        private int itemCount;
        private Action<RecycleCell, int> onBind;
        private int firstIndex = -1;

        private float Stride => cellHeight + spacing;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            content = scrollRect.content;
            scrollRect.onValueChanged.AddListener(_ => Refresh());
        }

        /// <summary>데이터 개수와 바인딩 함수를 넘기면 리스트를 다시 구성한다.</summary>
        public void Setup(int count, Action<RecycleCell, int> bind, bool keepPosition = false)
        {
            itemCount = count;
            onBind = bind;

            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0, count * Stride - spacing));
            if (keepPosition == false)
            {
                content.anchoredPosition = Vector2.zero;
            }

            // 기존 셀은 전부 회수 후 다시 배치 — 이전 데이터 잔상 방지
            while (activeCells.Count > 0)
            {
                Release(activeCells.First.Value);
                activeCells.RemoveFirst();
            }
            firstIndex = -1;
            Refresh();
        }

        /// <summary>특정 아이템이 보이도록 스크롤 이동.</summary>
        public void ScrollTo(int index)
        {
            float y = Mathf.Clamp(index * Stride, 0, Mathf.Max(0, content.sizeDelta.y - ViewportHeight));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            Refresh();
        }

        private float ViewportHeight => scrollRect.viewport != null ? scrollRect.viewport.rect.height : ((RectTransform)transform).rect.height;

        private void Refresh()
        {
            if (onBind == null) return;

            float top = content.anchoredPosition.y;
            int newFirst = Mathf.Clamp(Mathf.FloorToInt(top / Stride) - bufferCount, 0, Mathf.Max(0, itemCount - 1));
            int visible = Mathf.CeilToInt(ViewportHeight / Stride) + bufferCount * 2;
            int newLast = Mathf.Min(itemCount - 1, newFirst + visible - 1);

            if (newFirst == firstIndex && activeCells.Count == newLast - newFirst + 1) return;

            // 범위 밖으로 나간 셀 회수
            for (var node = activeCells.First; node != null;)
            {
                var next = node.Next;
                if (node.Value.Index < newFirst || node.Value.Index > newLast)
                {
                    Release(node.Value);
                    activeCells.Remove(node);
                }
                node = next;
            }

            // 비어 있는 인덱스만 새로 바인딩
            var used = new HashSet<int>();
            foreach (var cell in activeCells) used.Add(cell.Index);

            for (int i = newFirst; i <= newLast; i++)
            {
                if (used.Contains(i)) continue;
                RecycleCell cell = Acquire();
                cell.Index = i;
                cell.Rect.anchoredPosition = new Vector2(0, -i * Stride);
                onBind(cell, i);
                activeCells.AddLast(cell);
            }
            firstIndex = newFirst;
        }

        private RecycleCell Acquire()
        {
            RecycleCell cell = pool.Count > 0 ? pool.Pop() : CreateCell();
            cell.gameObject.SetActive(true);
            return cell;
        }

        private void Release(RecycleCell cell)
        {
            cell.Clear();                    // 반납 전 상태 초기화
            cell.gameObject.SetActive(false);
            pool.Push(cell);
        }

        private RecycleCell CreateCell()
        {
            RectTransform rt = Instantiate(cellPrefab, content, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, cellHeight);
            RecycleCell cell = rt.GetComponent<RecycleCell>();
            if (cell == null) cell = rt.gameObject.AddComponent<RecycleCell>();
            return cell;
        }
    }

    /// <summary>재사용 셀 기반 클래스. 실제 셀은 이것을 상속해 Clear/Bind를 구현한다.</summary>
    public class RecycleCell : MonoBehaviour
    {
        public int Index { get; set; } = -1;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>풀로 돌아갈 때 호출. 이벤트 구독 · 선택 표시 · 연출을 반드시 지운다.</summary>
        public virtual void Clear() { Index = -1; }
    }
}
