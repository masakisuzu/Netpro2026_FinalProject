using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Script.Network;

namespace Script.UI
{
    /// <summary>
    /// InGameシーンでの1メンバー分の表示（名前＋出した手＋勝敗結果）
    /// </summary>
    public class InGameMemberStateView : MonoBehaviour
    {
        [Header("テキスト表示")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("プレイヤーの状態アイコン")]
        [SerializeField] private Image defaultIcon; // 通常、考えてる時など
        [SerializeField] private Image retireIcon; // 負けたら常にコレ
        [SerializeField] private Image rockIcon;
        [SerializeField] private Image paperIcon;
        
        // 現在の状態を外から確認するためのプロパティ
        public IconType CurrentIcon { get; private set; }
        public bool IsRetired => CurrentIcon == IconType.Retire;

        /// <summary>
        /// 初期化時のユーザー情報の適応
        /// </summary>
        public void SetData(string playerName)
        {
            nameText.text = playerName;
        }
        
        /// <summary>
        /// ジャッジの結果表示の時に使う（ボタン押した瞬間には呼ばれない）
        /// </summary>
        public void SetIcon(IconType type)
        {
            CurrentIcon = type;
            
            // 選んだタイプだけ表示し、他は隠れる
            defaultIcon.gameObject.SetActive(type == IconType.Default);
            retireIcon.gameObject.SetActive(type == IconType.Retire);
            rockIcon.gameObject.SetActive(type == IconType.Rock);
            paperIcon.gameObject.SetActive(type == IconType.Paper);
        }
    }
}