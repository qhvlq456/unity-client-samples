using System;
using System.Collections;
using UnityEngine;

namespace ClientSamples.Flow
{
    /// <summary>
    /// 연속 소환 · 합성처럼 결과를 N장 단위로 넘겨 보여주는 흐름의 상태 객체.
    /// 회차 · 자동 진행 · 마지막 여부는 이 객체만 바꾸고, 화면은 Changed를 받아 표시만 한다.
    /// 상태의 주인을 하나로 두면 버튼 표시와 실제 진행이 어긋나지 않는다.
    /// </summary>
    public class ResultPager
    {
        public int CurrentPage { get; private set; }
        public int TotalPage { get; private set; }
        public int PageSize { get; private set; }
        public bool IsAuto { get; private set; }
        public bool IsLast => CurrentPage >= TotalPage;

        public event Action Changed;
        public event Action Finished;

        public void Start(int resultCount, int pageSize = 10)
        {
            PageSize = Mathf.Max(1, pageSize);
            TotalPage = Mathf.Max(1, (resultCount + PageSize - 1) / PageSize);
            CurrentPage = 1;
            IsAuto = false;
            Changed?.Invoke();
        }

        public void Next()
        {
            if (IsLast)
            {
                IsAuto = false;
                Finished?.Invoke();
                return;
            }
            CurrentPage++;
            Changed?.Invoke();
        }

        public void SetAuto(bool on)
        {
            if (IsAuto == on) return;
            IsAuto = on;
            Changed?.Invoke();
        }

        /// <summary>자동 진행 루프. 연출 대기는 화면이 넘겨준다.</summary>
        public IEnumerator AutoRoutine(Func<IEnumerator> playPage, float interval = 0.5f)
        {
            while (IsAuto)
            {
                yield return playPage();
                yield return new WaitForSeconds(interval);
                if (IsAuto == false) yield break;   // 연출 중 유저가 자동을 끈 경우
                Next();
                if (IsLast && IsAuto == false) yield break;
            }
        }

        public (int start, int count) RangeOf(int totalResults)
        {
            int start = (CurrentPage - 1) * PageSize;
            return (start, Mathf.Min(PageSize, totalResults - start));
        }
    }
}
