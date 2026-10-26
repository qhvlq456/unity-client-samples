using System;
using System.Collections.Generic;

namespace ClientSamples.Content.Quest
{
    public enum QuestKind { KillMonster, CollectItem, ReachLevel, UpgradeBuilding }
    public enum QuestState { InProgress, Completed, Rewarded }

    public class QuestDefinition
    {
        public int Id;
        public bool IsMain;
        public QuestKind Kind;
        public int TargetId;          // 몬스터 ID · 아이템 ID 등 (0 = 전체)
        public int RequiredAmount;
    }

    public class QuestProgress
    {
        public QuestDefinition Def;
        public int Amount;
        public QuestState State;

        /// <summary>표시용 진행 문자열. 달성형(레벨 · 건물)은 0/1, 1/1로 보여준다.</summary>
        public string ProgressText =>
            Def.Kind == QuestKind.ReachLevel || Def.Kind == QuestKind.UpgradeBuilding
                ? (State == QuestState.InProgress ? "0 / 1" : "1 / 1")
                : $"{Math.Min(Amount, Def.RequiredAmount)} / {Def.RequiredAmount}";
    }

    /// <summary>
    /// 게임 이벤트를 받아 퀘스트 진행도를 올리는 트래커.
    /// - 콘텐츠 코드는 퀘스트를 몰라도 된다: "몬스터 처치", "아이템 획득" 같은 이벤트만 보낸다.
    /// - 같은 종류 퀘스트만 골라 갱신하도록 종류별 인덱스를 둔다 (이벤트마다 전체 순회하지 않음).
    /// - 완료 · 보상 수령 상태를 분리해 보상 중복 수령을 막는다.
    /// </summary>
    public class QuestTracker
    {
        private readonly Dictionary<int, QuestProgress> byId = new Dictionary<int, QuestProgress>();
        private readonly Dictionary<QuestKind, List<QuestProgress>> byKind = new Dictionary<QuestKind, List<QuestProgress>>();

        public event Action<QuestProgress> ProgressChanged;
        public event Action<QuestProgress> Completed;

        public void Accept(QuestDefinition def, int savedAmount = 0)
        {
            var p = new QuestProgress { Def = def, Amount = savedAmount, State = QuestState.InProgress };
            if (p.Amount >= def.RequiredAmount) p.State = QuestState.Completed;

            byId[def.Id] = p;
            if (byKind.TryGetValue(def.Kind, out var list) == false)
            {
                list = new List<QuestProgress>();
                byKind.Add(def.Kind, list);
            }
            list.Add(p);
        }

        /// <summary>게임 이벤트 진입점. 누적형은 amount만큼 더하고, 달성형은 값이 목표 이상이면 완료.</summary>
        public void Report(QuestKind kind, int targetId, int amount)
        {
            if (byKind.TryGetValue(kind, out var list) == false) return;

            foreach (QuestProgress p in list)
            {
                if (p.State != QuestState.InProgress) continue;
                if (p.Def.TargetId != 0 && p.Def.TargetId != targetId) continue;

                bool reachType = kind == QuestKind.ReachLevel || kind == QuestKind.UpgradeBuilding;
                p.Amount = reachType ? Math.Max(p.Amount, amount) : p.Amount + amount;
                ProgressChanged?.Invoke(p);

                if (p.Amount >= p.Def.RequiredAmount)
                {
                    p.State = QuestState.Completed;
                    Completed?.Invoke(p);
                }
            }
        }

        /// <summary>보상 수령. 완료 상태에서만 한 번 성공한다.</summary>
        public bool TryClaim(int questId)
        {
            if (byId.TryGetValue(questId, out var p) == false) return false;
            if (p.State != QuestState.Completed) return false;
            p.State = QuestState.Rewarded;
            ProgressChanged?.Invoke(p);
            return true;
        }

        /// <summary>알림창에 띄울 퀘스트: 메인 우선, 그다음 완료 임박 순.</summary>
        public QuestProgress PickForNotice()
        {
            QuestProgress best = null;
            foreach (QuestProgress p in byId.Values)
            {
                if (p.State == QuestState.Rewarded) continue;
                if (best == null || Score(p) > Score(best)) best = p;
            }
            return best;
        }

        private static float Score(QuestProgress p)
        {
            float ratio = p.Def.RequiredAmount <= 0 ? 1f : (float)p.Amount / p.Def.RequiredAmount;
            return (p.Def.IsMain ? 10f : 0f) + (p.State == QuestState.Completed ? 5f : 0f) + ratio;
        }

        public QuestProgress Get(int id) => byId.TryGetValue(id, out var p) ? p : null;
    }
}
