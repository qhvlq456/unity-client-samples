using System;
using System.Collections.Generic;

namespace ClientSamples.Network
{
    /// <summary>
    /// 같은 대상에 대한 요청을 순서대로 하나씩 보내는 큐.
    /// 일괄 등록 · 일괄 구매처럼 요청이 연속으로 나가는 기능에서
    /// 서버의 동시 요청 오류와 응답 역전(이전 값으로 화면이 되돌아감)을 막는다.
    /// </summary>
    public class SequentialRequestQueue
    {
        public delegate void Request(Action<bool> onComplete);

        private readonly Queue<Request> queue = new Queue<Request>();
        private bool running;

        public int Pending => queue.Count + (running ? 1 : 0);
        public event Action Drained;

        public void Enqueue(Request request)
        {
            queue.Enqueue(request);
            if (running == false) RunNext();
        }

        public void Clear() => queue.Clear();

        private void RunNext()
        {
            if (queue.Count == 0)
            {
                running = false;
                Drained?.Invoke();
                return;
            }

            running = true;
            Request request = queue.Dequeue();
            bool done = false;
            request(success =>
            {
                if (done) return;      // 중복 콜백 방지
                done = true;
                if (success == false) queue.Clear();   // 실패하면 남은 요청은 보내지 않는다
                RunNext();
            });
        }
    }
}
