using System;
using System.Collections.Generic;

namespace ClientSamples.Data
{
    /// <summary>
    /// 기획 테이블(JSON · CSV 등에서 읽은 행 목록)을 ID로 조회하는 저장소.
    /// - 로드 시 한 번만 인덱싱하고, 런타임에는 읽기 전용으로 쓴다.
    /// - 중복 ID는 로드 시점에 바로 잡아낸다 (런타임에 엉뚱한 행을 쓰는 버그 방지).
    /// </summary>
    public interface ITableRow
    {
        int Id { get; }
    }

    public class TableRepository<T> where T : ITableRow
    {
        private readonly Dictionary<int, T> rows = new Dictionary<int, T>();
        public IReadOnlyCollection<T> All => rows.Values;

        public TableRepository(IEnumerable<T> source)
        {
            foreach (T row in source)
            {
                if (rows.ContainsKey(row.Id))
                {
                    throw new InvalidOperationException($"{typeof(T).Name} 테이블에 중복 ID {row.Id}");
                }
                rows.Add(row.Id, row);
            }
        }

        public T Get(int id)
        {
            if (rows.TryGetValue(id, out T row)) return row;
            throw new KeyNotFoundException($"{typeof(T).Name} 테이블에 ID {id} 없음");
        }

        public bool TryGet(int id, out T row) => rows.TryGetValue(id, out row);
    }
}
