using System;
using System.Collections.Generic;

namespace ClientSamples.Content.Gacha
{
    /// <summary>
    /// 가중치 테이블 추첨 + 확률 표시.
    /// - 실제 결과는 서버가 정하고, 클라이언트는 확률 정보 표시 · 오프라인 시뮬레이션 · 테스트에 쓴다.
    /// - 누적 가중치 + 이분 탐색으로 항목이 많아도 O(log n)에 뽑는다.
    /// - 표시 확률의 합이 100%가 되도록 반올림 오차를 마지막 항목에서 보정한다.
    /// </summary>
    public class WeightedTable<T>
    {
        private readonly List<T> items = new List<T>();
        private readonly List<long> cumulative = new List<long>();
        private readonly List<int> weights = new List<int>();
        public long TotalWeight { get; private set; }

        public void Add(T item, int weight)
        {
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            items.Add(item);
            weights.Add(weight);
            TotalWeight += weight;
            cumulative.Add(TotalWeight);
        }

        /// <summary>roll은 [0, TotalWeight) 범위. 테스트에서는 roll을 직접 넣어 결과를 고정할 수 있다.</summary>
        public T PickByRoll(long roll)
        {
            if (items.Count == 0) throw new InvalidOperationException("빈 테이블");
            if (roll < 0 || roll >= TotalWeight) throw new ArgumentOutOfRangeException(nameof(roll));

            int lo = 0, hi = cumulative.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (roll < cumulative[mid]) hi = mid;
                else lo = mid + 1;
            }
            return items[lo];
        }

        public T Pick(Random random) => PickByRoll((long)(random.NextDouble() * TotalWeight));

        /// <summary>확률 정보 화면용 (소수 둘째 자리 퍼센트, 합계 100.00 보정).</summary>
        public List<(T item, float percent)> GetDisplayRates()
        {
            var result = new List<(T, float)>();
            float sum = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                float p = (float)Math.Round(weights[i] * 100.0 / TotalWeight, 2);
                if (i == items.Count - 1) p = (float)Math.Round(100f - sum, 2);
                sum += p;
                result.Add((items[i], p));
            }
            return result;
        }
    }
}
