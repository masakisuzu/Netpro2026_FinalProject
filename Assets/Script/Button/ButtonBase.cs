using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.Button
{
    /// <summary>
    /// 共通のボタン処理
    /// アタッチされた画像オブジェクトからイベント受信してくる（Interfaceによって）
    /// 一応 virtual にしているので固有処理させたいならこれを基底クラスとして継承すれば行ける
    /// </summary>
    public class ButtonBase : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("ホバーターゲット")]
        [SerializeField] private RectTransform target;
        [SerializeField] private float hoverScale = 1.1f;
        private Vector3 _defaultScale;
        
        // R3のSubjectで外側に通知する
        private readonly Subject<Unit> _onClick = new Subject<Unit>();
    
        // 外部から購読できるようにする。直接参照して発火されるのを防ぐため
        public Observable<Unit> OnClickAsObservable => _onClick;

        // 継承先で上書きできるようにvirtual
        // 上書きされなくても呼ばれる点に注意
        
        private void Awake()
        {
            if (target == null)
                target = transform as RectTransform;

            // 今の大きさが通常の大きさとなる
            _defaultScale = target.localScale;
        }
        
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("You clicked ");
            _onClick.OnNext(Unit.Default); // 発火！購読元に処理が行く
            target.localScale = _defaultScale;
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("You hovered ");
            target.localScale = _defaultScale * hoverScale;
        }
        
        public virtual void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log("You cancel ");
            target.localScale = _defaultScale;
        }

        private void OnDestroy()
        {
            _onClick.Dispose();
        }
        
        /// <summary>
        /// オブジェクトが無効（gameObject.SetActive(false)）になった時も戻しておく
        /// </summary>
        private void OnDisable()
        {
            if (target != null)
                target.localScale = _defaultScale;
        }
    }
}