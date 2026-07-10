using Script.Button;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.Legacy
{
    public class BackTitle : ButtonBase
    {
        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);
            Debug.Log("EnterRoom: ルーム参加の通信を開始！");
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            Debug.Log("EnterRoom: ホバー時にボタンを強調する演出");
        }
    }
}