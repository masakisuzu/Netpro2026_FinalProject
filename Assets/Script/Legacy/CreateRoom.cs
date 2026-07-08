using Script.UI.Button;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.Legacy
{
    public class CreateRoom : ButtonBase
    {
        public override void OnPointerClick(PointerEventData eventData)
        {
            // 基底クラスの処理（R3の発火など）
            base.OnPointerClick(eventData);
        
            // ここに個別の処理を書く
            Debug.Log("CreateRoom: ルーム作成の通信を開始！");
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            // 共通のホバー処理
            base.OnPointerEnter(eventData);
        
            // 個別のホバー演出（例：ボタンを少し大きくする等）
            Debug.Log("CreateRoom: ホバー時にボタンを強調する演出");
        }
    }
}
