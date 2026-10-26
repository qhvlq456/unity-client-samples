using System.Collections.Generic;
using UnityEngine;

namespace ClientSamples.UI
{
    /// <summary>
    /// 팝업 공용 스택.
    /// - ESC(PC) / 뒤로가기(모바일)는 가장 최근 팝업만 닫는다.
    /// - 딤드는 하나만 두고 최상단 팝업 바로 아래로 옮긴다 (반투명 겹침 · 오버드로우 방지).
    /// - 같은 팝업 중복 열기를 막는다.
    /// </summary>
    public class PopupStack : MonoBehaviour
    {
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private CanvasGroup dim;

        private readonly List<PopupBase> stack = new List<PopupBase>();

        public int Count => stack.Count;
        public PopupBase Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        public T Open<T>(T prefab) where T : PopupBase
        {
            // 이미 열린 같은 종류의 팝업이면 맨 위로 올리기만 한다
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i] is T opened && opened.AllowMultiple == false)
                {
                    stack.RemoveAt(i);
                    stack.Add(opened);
                    opened.transform.SetAsLastSibling();
                    RefreshDim();
                    return opened;
                }
            }

            T popup = Instantiate(prefab, popupRoot, false);
            popup.Owner = this;
            stack.Add(popup);
            popup.OnOpen();
            RefreshDim();
            return popup;
        }

        /// <summary>ESC · 뒤로가기 입력에서 호출. 처리했으면 true.</summary>
        public bool CloseTop()
        {
            PopupBase top = Top;
            if (top == null) return false;
            if (top.CanCloseByBack == false) return true;   // 닫을 수 없는 팝업은 입력만 소비
            Close(top);
            return true;
        }

        public void Close(PopupBase popup)
        {
            if (stack.Remove(popup) == false) return;
            popup.OnClose();
            Destroy(popup.gameObject);
            RefreshDim();
        }

        private void RefreshDim()
        {
            bool show = stack.Count > 0;
            dim.alpha = show ? 1f : 0f;
            dim.blocksRaycasts = show;        // 뒤 화면 터치 차단도 딤드 하나가 담당
            if (show)
            {
                dim.transform.SetSiblingIndex(Mathf.Max(0, Top.transform.GetSiblingIndex() - 1));
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CloseTop();
            }
        }
    }

    public abstract class PopupBase : MonoBehaviour
    {
        public PopupStack Owner { get; set; }
        public virtual bool CanCloseByBack => true;
        public virtual bool AllowMultiple => false;

        public virtual void OnOpen() { }
        public virtual void OnClose() { }
        protected void CloseSelf() => Owner.Close(this);
    }
}
