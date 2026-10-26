using System;
using System.Collections.Generic;

namespace ClientSamples.InGame.FSM
{
    /// <summary>
    /// 상태 하나 = Enter / Tick / Exit. 전이는 상태가 다음 상태 키를 돌려주는 방식으로만 일어난다.
    /// 캐릭터 · 몬스터 AI, 콘텐츠 진행(대기 → 연출 → 결과) 모두에 같은 틀을 쓴다.
    /// </summary>
    public interface IState<TKey> where TKey : struct
    {
        void Enter();
        /// <summary>계속 머물면 null, 전이하려면 다음 상태 키를 반환.</summary>
        TKey? Tick(float deltaTime);
        void Exit();
    }

    public class StateMachine<TKey> where TKey : struct, Enum
    {
        private readonly Dictionary<TKey, IState<TKey>> states = new Dictionary<TKey, IState<TKey>>();
        private IState<TKey> current;

        public TKey CurrentKey { get; private set; }
        public event Action<TKey, TKey> Changed;   // (이전, 다음)

        public StateMachine<TKey> Add(TKey key, IState<TKey> state)
        {
            states.Add(key, state);
            return this;
        }

        public void Start(TKey key)
        {
            CurrentKey = key;
            current = states[key];
            current.Enter();
        }

        public void Tick(float deltaTime)
        {
            if (current == null) return;
            TKey? next = current.Tick(deltaTime);
            if (next.HasValue && EqualityComparer<TKey>.Default.Equals(next.Value, CurrentKey) == false)
            {
                ChangeTo(next.Value);
            }
        }

        /// <summary>외부 이벤트(피격 · 사망 · 스턴)로 강제 전이할 때 사용.</summary>
        public void ChangeTo(TKey key)
        {
            TKey prev = CurrentKey;
            current?.Exit();
            CurrentKey = key;
            current = states[key];
            current.Enter();
            Changed?.Invoke(prev, key);
        }
    }
}
