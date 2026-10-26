using System;
using System.Collections.Generic;
using NUnit.Framework;
using ClientSamples.Flow;
using ClientSamples.Network;
using ClientSamples.RedDot;

namespace ClientSamples.Tests
{
    public class RedDotTreeTests
    {
        [Test]
        public void ChildOn_TurnsParentOn_AndOffAgain()
        {
            var tree = new RedDotTree();
            var menu = new List<bool>();
            tree.Subscribe("Menu", v => menu.Add(v));

            tree.Set("Menu/Research/Slot1", true);
            Assert.IsTrue(tree.Get("Menu"));
            Assert.IsTrue(tree.Get("Menu/Research"));

            tree.Set("Menu/Research/Slot1", false);
            Assert.IsFalse(tree.Get("Menu"));
            CollectionAssert.AreEqual(new[] { false, true, false }, menu);
        }

        [Test]
        public void SiblingStillOn_KeepsParentOn()
        {
            var tree = new RedDotTree();
            tree.Set("Menu/Mission/A", true);
            tree.Set("Menu/Mission/B", true);
            tree.Set("Menu/Mission/A", false);
            Assert.IsTrue(tree.Get("Menu/Mission"));
        }

        [Test]
        public void SameValue_DoesNotNotify()
        {
            var tree = new RedDotTree();
            int count = 0;
            tree.Subscribe("Guild", _ => count++);
            tree.Set("Guild/Join", true);
            tree.Set("Guild/Join", true);
            Assert.AreEqual(2, count);   // 구독 시 1회 + 변경 1회
        }
    }

    public class ResultPagerTests
    {
        [Test]
        public void PagesAreRoundedUp()
        {
            var pager = new ResultPager();
            pager.Start(25, 10);
            Assert.AreEqual(3, pager.TotalPage);
            Assert.AreEqual((20, 5), (pager.Next2().RangeOf(25)));
        }

        [Test]
        public void NextOnLast_FinishesAndStopsAuto()
        {
            var pager = new ResultPager();
            bool finished = false;
            pager.Finished += () => finished = true;
            pager.Start(10, 10);
            pager.SetAuto(true);
            pager.Next();
            Assert.IsTrue(finished);
            Assert.IsFalse(pager.IsAuto);
        }
    }

    internal static class PagerTestExt
    {
        public static ResultPager Next2(this ResultPager p) { p.Next(); p.Next(); return p; }
    }

    public class SequentialRequestQueueTests
    {
        [Test]
        public void RequestsRunOneAtATime_InOrder()
        {
            var queue = new SequentialRequestQueue();
            var log = new List<string>();
            var pending = new Queue<Action<bool>>();

            for (int i = 0; i < 3; i++)
            {
                int n = i;
                queue.Enqueue(done => { log.Add("send" + n); pending.Enqueue(done); });
            }

            CollectionAssert.AreEqual(new[] { "send0" }, log);   // 첫 응답 전에는 하나만 나간다
            pending.Dequeue()(true);
            pending.Dequeue()(true);
            CollectionAssert.AreEqual(new[] { "send0", "send1", "send2" }, log);
        }

        [Test]
        public void Failure_ClearsRemaining()
        {
            var queue = new SequentialRequestQueue();
            int sent = 0;
            Action<bool> first = null;
            queue.Enqueue(done => { sent++; first = done; });
            queue.Enqueue(done => { sent++; done(true); });
            first(false);
            Assert.AreEqual(1, sent);
            Assert.AreEqual(0, queue.Pending);
        }
    }
}
