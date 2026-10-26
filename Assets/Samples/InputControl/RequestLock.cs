using System;
using UnityEngine;

namespace ClientSamples.InputControl
{
    /// <summary>
    /// 요청부터 결과 연출 종료까지 화면 입력을 잠그는 도우미.
    /// - 연타로 같은 요청이 여러 번 나가는 것을 막는다.
    /// - 실패 응답 · 타임아웃에서도 반드시 해제한다 (잠긴 채로 남는 버그 방지).
    /// </summary>
    public class RequestLock
    {
        private readonly CanvasGroup target;
        private readonly float timeoutSeconds;
        private float lockedAt = -1f;

        public bool IsLocked => lockedAt >= 0f;

        public RequestLock(CanvasGroup target, float timeoutSeconds = 10f)
        {
            this.target = target;
            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>잠금에 성공하면 true. 이미 잠겨 있으면 false (요청을 보내지 않는다).</summary>
        public bool TryLock()
        {
            if (IsLocked) return false;
            lockedAt = Time.unscaledTime;
            target.interactable = false;
            return true;
        }

        public void Unlock()
        {
            lockedAt = -1f;
            target.interactable = true;
        }

        /// <summary>매 프레임 호출. 응답이 오지 않으면 타임아웃으로 해제.</summary>
        public void Tick(Action onTimeout = null)
        {
            if (IsLocked && Time.unscaledTime - lockedAt > timeoutSeconds)
            {
                Unlock();
                onTimeout?.Invoke();
            }
        }
    }

    // 사용 예시
    //
    // void OnClickUpgrade()
    // {
    //     if (requestLock.TryLock() == false) return;
    //     api.RequestUpgrade(OnUpgradeResponse);
    // }
    //
    // void OnUpgradeResponse(Response res)
    // {
    //     if (res.Success == false) { requestLock.Unlock(); ShowError(res); return; }
    //     resultView.Play(res, onFinished: requestLock.Unlock);   // 연출이 끝난 뒤 해제
    // }
}
