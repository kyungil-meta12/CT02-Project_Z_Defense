using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// UI 버튼 클릭, 선택, 포인터 진입 사운드를 ProjectAudioManager로 재생한다.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIButtonAudioFeedback : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, ISelectHandler
    {
        [Header("버튼 사운드")]
        [SerializeField] private AudioCueSO clickCue;
        [SerializeField] private AudioCueSO hoverCue;
        [SerializeField] private AudioCueSO selectCue;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;

        private Selectable selectable;

        // 필요한 컴포넌트 참조를 초기화한다
        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        // 포인터 클릭 시 클릭 사운드를 재생한다
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanPlay())
            {
                return;
            }

            ProjectAudioManager.PlayCue(clickCue, volumeScale);
        }

        // 포인터 진입 시 호버 사운드를 재생한다
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanPlay())
            {
                return;
            }

            ProjectAudioManager.PlayCue(hoverCue, volumeScale);
        }

        // UI 선택 시 선택 사운드를 재생한다
        public void OnSelect(BaseEventData eventData)
        {
            if (!CanPlay())
            {
                return;
            }

            ProjectAudioManager.PlayCue(selectCue, volumeScale);
        }

        // 버튼이 상호작용 가능한지 확인한다
        private bool CanPlay()
        {
            return selectable == null || selectable.IsInteractable();
        }
    }
}
