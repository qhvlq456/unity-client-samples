using System;
using System.Collections.Generic;
using NUnit.Framework;
using ClientSamples.Content.Enhance;
using ClientSamples.Content.Gacha;
using ClientSamples.Content.Quest;
using ClientSamples.Data;
using ClientSamples.InGame.Battle;

namespace ClientSamples.Tests
{
    public class EnhanceSystemTests
    {
        private class FakeInventory : EnhanceSystem.IInventory
        {
            public int Stones = 10;
            public int Count(int itemId) => Stones;
        }

        private class FakeServer : EnhanceSystem.IServer
        {
            public Action<EnhanceSystem.EnhanceResponse> Pending;
            public void RequestEnhance(int uid, int step, bool protect, Action<EnhanceSystem.EnhanceResponse> cb) => Pending = cb;
        }

        private static EnhanceSystem Create(out FakeServer server, out FakeInventory inv)
        {
            var rows = new List<EnhanceStepRow>();
            for (int i = 1; i <= 5; i++) rows.Add(new EnhanceStepRow { Id = i, CostItemId = 1, CostAmount = i, SuccessRate = 5000 });
            server = new FakeServer();
            inv = new FakeInventory();
            var sys = new EnhanceSystem(new TableRepository<EnhanceStepRow>(rows), inv, server, 5);
            sys.Bind(targetUid: 100, currentStep: 0);
            return sys;
        }

        [Test]
        public void Success_MovesSelectionToNextStep()
        {
            var sys = Create(out var server, out _);
            Assert.IsTrue(sys.Request(false));
            server.Pending(new EnhanceSystem.EnhanceResponse { Ok = true, Result = EnhanceResult.Success, NewStep = 1 });
            Assert.AreEqual(1, sys.CurrentStep);
            Assert.AreEqual(2, sys.SelectedStep);
        }

        [Test]
        public void UserChangedSelectionDuringRequest_IsKept()
        {
            var sys = Create(out var server, out _);
            sys.Request(false);
            sys.Select(4);   // 연출 중 다른 단계 확인
            server.Pending(new EnhanceSystem.EnhanceResponse { Ok = true, Result = EnhanceResult.Success, NewStep = 1 });
            Assert.AreEqual(4, sys.SelectedStep);
        }

        [Test]
        public void CannotRequestTwice_WhileRequesting()
        {
            var sys = Create(out _, out _);
            Assert.IsTrue(sys.Request(false));
            Assert.IsFalse(sys.Request(false));
        }

        [Test]
        public void ErrorResponse_ReleasesRequesting()
        {
            var sys = Create(out var server, out _);
            sys.Request(false);
            server.Pending(new EnhanceSystem.EnhanceResponse { Ok = false, ErrorCode = "E1" });
            Assert.IsFalse(sys.IsRequesting);
        }

        [Test]
        public void NotEnoughMaterial_Blocks()
        {
            var sys = Create(out _, out var inv);
            inv.Stones = 0;
            Assert.IsFalse(sys.CanRequest(out string reason));
            Assert.AreEqual("재료 부족", reason);
        }
    }

    public class QuestTrackerTests
    {
        [Test]
        public void KillQuest_CompletesAndClaimsOnce()
        {
            var t = new QuestTracker();
            t.Accept(new QuestDefinition { Id = 1, Kind = QuestKind.KillMonster, TargetId = 7, RequiredAmount = 3 });
            t.Report(QuestKind.KillMonster, 7, 2);
            t.Report(QuestKind.KillMonster, 8, 5);      // 다른 몬스터는 무시
            Assert.AreEqual(QuestState.InProgress, t.Get(1).State);
            t.Report(QuestKind.KillMonster, 7, 1);
            Assert.AreEqual(QuestState.Completed, t.Get(1).State);
            Assert.IsTrue(t.TryClaim(1));
            Assert.IsFalse(t.TryClaim(1));             // 중복 수령 불가
        }

        [Test]
        public void ReachQuest_ShowsZeroOrOne()
        {
            var t = new QuestTracker();
            t.Accept(new QuestDefinition { Id = 2, Kind = QuestKind.ReachLevel, RequiredAmount = 10 });
            Assert.AreEqual("0 / 1", t.Get(2).ProgressText);
            t.Report(QuestKind.ReachLevel, 0, 12);
            Assert.AreEqual("1 / 1", t.Get(2).ProgressText);
        }

        [Test]
        public void Notice_PrefersMainQuest()
        {
            var t = new QuestTracker();
            t.Accept(new QuestDefinition { Id = 1, IsMain = false, Kind = QuestKind.CollectItem, RequiredAmount = 2 }, savedAmount: 1);
            t.Accept(new QuestDefinition { Id = 2, IsMain = true, Kind = QuestKind.CollectItem, RequiredAmount = 10 });
            Assert.AreEqual(2, t.PickForNotice().Def.Id);
        }
    }

    public class WeightedTableTests
    {
        [Test]
        public void RollMapsToCumulativeRanges()
        {
            var table = new WeightedTable<string>();
            table.Add("N", 70);
            table.Add("R", 25);
            table.Add("SR", 5);
            Assert.AreEqual("N", table.PickByRoll(0));
            Assert.AreEqual("N", table.PickByRoll(69));
            Assert.AreEqual("R", table.PickByRoll(70));
            Assert.AreEqual("SR", table.PickByRoll(99));
        }

        [Test]
        public void DisplayRatesSumTo100()
        {
            var table = new WeightedTable<int>();
            table.Add(1, 1); table.Add(2, 1); table.Add(3, 1);
            float sum = 0;
            foreach (var r in table.GetDisplayRates()) sum += r.percent;
            Assert.AreEqual(100f, sum, 0.001f);
        }
    }

    public class AutoBattleTests
    {
        private static List<BattleUnit> Wave(int wave) => new List<BattleUnit>
        {
            new BattleUnit { Id = 1000 + wave, Team = 1, X = 5f, MaxHp = 3, Hp = 3, Attack = 1 }
        };

        [Test]
        public void HeroClearsWaves()
        {
            var hero = new BattleUnit { Id = 1, Team = 0, X = 0f, MaxHp = 100, Hp = 100, Attack = 1, AttackInterval = 0.5f };
            var battle = new AutoBattle(new[] { hero }, Wave);
            int cleared = 0;
            battle.WaveCleared += _ => cleared++;

            for (int i = 0; i < 200; i++) battle.Update(0.1f);

            Assert.GreaterOrEqual(cleared, 2);
            Assert.IsFalse(battle.IsDefeated);
        }

        [Test]
        public void SameInput_SameResult()
        {
            int Run()
            {
                var hero = new BattleUnit { Id = 1, Team = 0, X = 0f, MaxHp = 50, Hp = 50, Attack = 2 };
                var b = new AutoBattle(new[] { hero }, Wave);
                for (int i = 0; i < 37; i++) b.Update(0.037f);   // 프레임 간격이 달라도
                return hero.Hp * 1000 + b.Wave;
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
