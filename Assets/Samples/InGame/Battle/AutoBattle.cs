using System;
using System.Collections.Generic;
using ClientSamples.InGame.FSM;

namespace ClientSamples.InGame.Battle
{
    /// <summary>전투 유닛의 순수 데이터 + 로직. MonoBehaviour와 분리해 테스트 · 서버 검증 · 리플레이가 가능하다.</summary>
    public class BattleUnit
    {
        public int Id;
        public int Team;               // 0 = 아군, 1 = 적
        public float X;                // 1차원 레인 위치 (방치형 · 횡스크롤 전투)
        public int MaxHp;
        public int Hp;
        public int Attack;
        public float Range = 1.5f;
        public float MoveSpeed = 2f;
        public float AttackInterval = 1f;
        public float Cooldown;

        public bool IsDead => Hp <= 0;
    }

    public enum UnitState { Idle, Move, Attack, Dead }

    /// <summary>
    /// 웨이브 기반 자동 전투 루프 (방치형 RPG 형태).
    /// - 유닛 AI는 StateMachine(Idle → Move → Attack → Dead)으로 돌린다.
    /// - 고정 시간 간격(Step)으로 시뮬레이션해 프레임 레이트와 무관하게 같은 결과가 나온다.
    /// - 화면(View)은 이벤트(Damaged · Died · WaveCleared)만 받아 연출한다.
    /// </summary>
    public class AutoBattle
    {
        public const float Step = 0.1f;

        private readonly List<BattleUnit> units = new List<BattleUnit>();
        private readonly Dictionary<int, StateMachine<UnitState>> brains = new Dictionary<int, StateMachine<UnitState>>();
        private readonly Func<int, List<BattleUnit>> waveFactory;
        private float accumulator;

        public int Wave { get; private set; }
        public bool IsDefeated { get; private set; }

        public event Action<BattleUnit, BattleUnit, int> Damaged;   // 공격자, 피격자, 피해량
        public event Action<BattleUnit> Died;
        public event Action<int> WaveCleared;

        public AutoBattle(IEnumerable<BattleUnit> heroes, Func<int, List<BattleUnit>> waveFactory)
        {
            this.waveFactory = waveFactory;
            foreach (BattleUnit hero in heroes) AddUnit(hero);
            StartWave(1);
        }

        public IReadOnlyList<BattleUnit> Units => units;

        /// <summary>Update에서 deltaTime을 넣으면 내부적으로 고정 간격으로 나눠 진행.</summary>
        public void Update(float deltaTime)
        {
            if (IsDefeated) return;
            accumulator += deltaTime;
            while (accumulator >= Step)
            {
                accumulator -= Step;
                Simulate(Step);
            }
        }

        private void Simulate(float dt)
        {
            for (int i = 0; i < units.Count; i++)
            {
                brains[units[i].Id].Tick(dt);
            }

            bool enemiesAlive = units.Exists(u => u.Team == 1 && u.IsDead == false);
            bool heroesAlive = units.Exists(u => u.Team == 0 && u.IsDead == false);

            if (heroesAlive == false) { IsDefeated = true; return; }
            if (enemiesAlive == false)
            {
                WaveCleared?.Invoke(Wave);
                foreach (BattleUnit dead in units.FindAll(u => u.Team == 1)) brains.Remove(dead.Id);
                units.RemoveAll(u => u.Team == 1);
                StartWave(Wave + 1);
            }
        }

        private void StartWave(int wave)
        {
            Wave = wave;
            foreach (BattleUnit enemy in waveFactory(wave)) AddUnit(enemy);
        }

        private void AddUnit(BattleUnit unit)
        {
            units.Add(unit);
            var fsm = new StateMachine<UnitState>()
                .Add(UnitState.Idle, new IdleState(this, unit))
                .Add(UnitState.Move, new MoveState(this, unit))
                .Add(UnitState.Attack, new AttackState(this, unit))
                .Add(UnitState.Dead, new DeadState());
            brains[unit.Id] = fsm;
            fsm.Start(UnitState.Idle);
        }

        /// <summary>가장 가까운 살아 있는 적. 동률이면 ID가 작은 쪽 (결과 고정).</summary>
        internal BattleUnit FindTarget(BattleUnit self)
        {
            BattleUnit best = null;
            float bestDist = float.MaxValue;
            foreach (BattleUnit u in units)
            {
                if (u.Team == self.Team || u.IsDead) continue;
                float d = Math.Abs(u.X - self.X);
                if (d < bestDist || (d == bestDist && best != null && u.Id < best.Id))
                {
                    best = u;
                    bestDist = d;
                }
            }
            return best;
        }

        internal void ApplyDamage(BattleUnit attacker, BattleUnit target)
        {
            if (target.IsDead) return;
            int damage = Math.Max(1, attacker.Attack);
            target.Hp = Math.Max(0, target.Hp - damage);
            Damaged?.Invoke(attacker, target, damage);
            if (target.IsDead)
            {
                brains[target.Id].ChangeTo(UnitState.Dead);
                Died?.Invoke(target);
            }
        }

        // ── 상태 구현 ─────────────────────────────────────────

        private class IdleState : IState<UnitState>
        {
            private readonly AutoBattle battle; private readonly BattleUnit unit;
            public IdleState(AutoBattle b, BattleUnit u) { battle = b; unit = u; }
            public void Enter() { }
            public UnitState? Tick(float dt) => battle.FindTarget(unit) != null ? UnitState.Move : (UnitState?)null;
            public void Exit() { }
        }

        private class MoveState : IState<UnitState>
        {
            private readonly AutoBattle battle; private readonly BattleUnit unit;
            public MoveState(AutoBattle b, BattleUnit u) { battle = b; unit = u; }
            public void Enter() { }
            public UnitState? Tick(float dt)
            {
                BattleUnit target = battle.FindTarget(unit);
                if (target == null) return UnitState.Idle;
                float dist = target.X - unit.X;
                if (Math.Abs(dist) <= unit.Range) return UnitState.Attack;
                unit.X += Math.Sign(dist) * unit.MoveSpeed * dt;
                return null;
            }
            public void Exit() { }
        }

        private class AttackState : IState<UnitState>
        {
            private readonly AutoBattle battle; private readonly BattleUnit unit;
            public AttackState(AutoBattle b, BattleUnit u) { battle = b; unit = u; }
            public void Enter() { }
            public UnitState? Tick(float dt)
            {
                BattleUnit target = battle.FindTarget(unit);
                if (target == null) return UnitState.Idle;
                if (Math.Abs(target.X - unit.X) > unit.Range) return UnitState.Move;

                unit.Cooldown -= dt;
                if (unit.Cooldown <= 0f)
                {
                    unit.Cooldown = unit.AttackInterval;
                    battle.ApplyDamage(unit, target);
                }
                return null;
            }
            public void Exit() { }
        }

        private class DeadState : IState<UnitState>
        {
            public void Enter() { }
            public UnitState? Tick(float dt) => null;
            public void Exit() { }
        }
    }
}
