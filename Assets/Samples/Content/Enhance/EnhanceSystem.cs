using System;
using System.Collections.Generic;
using ClientSamples.Data;

namespace ClientSamples.Content.Enhance
{
    /// <summary>강화 단계 테이블 한 행.</summary>
    public class EnhanceStepRow : ITableRow
    {
        public int Id { get; set; }               // 단계 (1, 2, 3 ...)
        public int CostItemId { get; set; }
        public int CostAmount { get; set; }
        public int SuccessRate { get; set; }      // 만분율 (10000 = 100%)
        public int DestroyRate { get; set; }      // 실패 시 파괴 확률 (만분율)
        public bool CanProtect { get; set; }      // 파괴 방지 아이템 사용 가능 여부
    }

    public enum EnhanceResult { Success, Fail, Destroyed }

    /// <summary>
    /// 강화 콘텐츠의 클라이언트 쪽 흐름.
    /// - 요청 전 검증(재료 · 최대 단계 · 진행 중)만 클라이언트가 하고, 결과는 서버 응답을 그대로 적용한다.
    /// - 연출 중에 유저가 다른 단계를 골랐다면 응답 후 자동 선택 변경을 하지 않는다 (선택 존중).
    /// - 상태는 이 객체만 바꾸고 화면은 Changed를 받아 표시한다.
    /// </summary>
    public class EnhanceSystem
    {
        public interface IInventory
        {
            int Count(int itemId);
        }

        public interface IServer
        {
            void RequestEnhance(int targetUid, int step, bool useProtect, Action<EnhanceResponse> onResponse);
        }

        public class EnhanceResponse
        {
            public bool Ok;
            public string ErrorCode;
            public EnhanceResult Result;
            public int NewStep;
        }

        private readonly TableRepository<EnhanceStepRow> table;
        private readonly IInventory inventory;
        private readonly IServer server;
        private readonly int maxStep;

        public int TargetUid { get; private set; }
        public int CurrentStep { get; private set; }
        public int SelectedStep { get; private set; }
        public bool IsRequesting { get; private set; }
        public bool AutoNextOnSuccess { get; set; } = true;

        public event Action Changed;
        public event Action<EnhanceResult> ResultArrived;

        public EnhanceSystem(TableRepository<EnhanceStepRow> table, IInventory inventory, IServer server, int maxStep)
        {
            this.table = table;
            this.inventory = inventory;
            this.server = server;
            this.maxStep = maxStep;
        }

        public void Bind(int targetUid, int currentStep)
        {
            TargetUid = targetUid;
            CurrentStep = currentStep;
            SelectedStep = Math.Min(currentStep + 1, maxStep);
            Changed?.Invoke();
        }

        public void Select(int step)
        {
            if (step < 1 || step > maxStep) return;
            SelectedStep = step;
            Changed?.Invoke();
        }

        /// <summary>강화 버튼 활성화 조건. 화면은 이 결과만 보고 버튼을 켜고 끈다.</summary>
        public bool CanRequest(out string reason)
        {
            reason = null;
            if (IsRequesting) { reason = "진행 중"; return false; }
            if (CurrentStep >= maxStep) { reason = "최대 단계"; return false; }
            if (SelectedStep != CurrentStep + 1) { reason = "다음 단계만 강화 가능"; return false; }

            EnhanceStepRow row = table.Get(SelectedStep);
            if (inventory.Count(row.CostItemId) < row.CostAmount) { reason = "재료 부족"; return false; }
            return true;
        }

        public bool Request(bool useProtect)
        {
            if (CanRequest(out _) == false) return false;

            EnhanceStepRow row = table.Get(SelectedStep);
            bool protect = useProtect && row.CanProtect;
            int requestedStep = SelectedStep;

            IsRequesting = true;
            Changed?.Invoke();
            server.RequestEnhance(TargetUid, requestedStep, protect, res => OnResponse(requestedStep, res));
            return true;
        }

        private void OnResponse(int requestedStep, EnhanceResponse res)
        {
            IsRequesting = false;

            if (res.Ok == false)
            {
                Changed?.Invoke();          // 실패 응답에서도 진행 중 상태는 반드시 해제
                return;
            }

            CurrentStep = res.NewStep;      // 결과는 서버 기준

            // 유저가 연출 중 선택을 바꾸지 않았을 때만 다음 단계로 자동 이동
            bool stillSelected = SelectedStep == requestedStep;
            if (res.Result == EnhanceResult.Success && AutoNextOnSuccess && stillSelected)
            {
                SelectedStep = Math.Min(CurrentStep + 1, maxStep);
            }
            else if (res.Result == EnhanceResult.Destroyed)
            {
                SelectedStep = Math.Min(CurrentStep + 1, maxStep);
            }

            ResultArrived?.Invoke(res.Result);
            Changed?.Invoke();
        }

        /// <summary>표시용 확률 정보 (만분율 → 퍼센트 문자열).</summary>
        public static string FormatRate(int rate) => (rate / 100f).ToString("0.##") + "%";
    }
}
