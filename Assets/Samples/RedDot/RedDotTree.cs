using System;
using System.Collections.Generic;

namespace ClientSamples.RedDot
{
    /// <summary>
    /// 경로 기반 레드닷 트리. 예: "Menu/Research/Slot3"
    /// - 조건은 데이터 쪽에서 한 번만 계산해 Set으로 넣는다 (화면마다 따로 계산하지 않음).
    /// - 부모는 자식 중 하나라도 켜져 있으면 켜진다. 변경된 경로의 조상만 다시 계산한다.
    /// - 화면은 Subscribe로 값만 읽는다.
    /// </summary>
    public class RedDotTree
    {
        private class Node
        {
            public string Path;
            public Node Parent;
            public readonly List<Node> Children = new List<Node>();
            public bool Self;
            public bool Value;
            public event Action<bool> Changed;
            public void Notify() => Changed?.Invoke(Value);
            public void Add(Action<bool> a) => Changed += a;
            public void Remove(Action<bool> a) => Changed -= a;
        }

        private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();

        public void Set(string path, bool on)
        {
            Node node = GetOrCreate(path);
            if (node.Self == on) return;
            node.Self = on;
            Recalculate(node);
        }

        public bool Get(string path) => nodes.TryGetValue(path, out Node n) && n.Value;

        public void Subscribe(string path, Action<bool> listener)
        {
            Node node = GetOrCreate(path);
            node.Add(listener);
            listener(node.Value);   // 구독 즉시 현재 값 전달
        }

        public void Unsubscribe(string path, Action<bool> listener)
        {
            if (nodes.TryGetValue(path, out Node node)) node.Remove(listener);
        }

        private void Recalculate(Node node)
        {
            while (node != null)
            {
                bool value = node.Self;
                foreach (Node child in node.Children)
                {
                    if (child.Value) { value = true; break; }
                }
                if (value == node.Value) return;   // 값이 안 바뀌면 위로 전파하지 않는다
                node.Value = value;
                node.Notify();
                node = node.Parent;
            }
        }

        private Node GetOrCreate(string path)
        {
            if (nodes.TryGetValue(path, out Node node)) return node;

            node = new Node { Path = path };
            nodes.Add(path, node);

            int slash = path.LastIndexOf('/');
            if (slash > 0)
            {
                Node parent = GetOrCreate(path.Substring(0, slash));
                node.Parent = parent;
                parent.Children.Add(node);
            }
            return node;
        }
    }
}
