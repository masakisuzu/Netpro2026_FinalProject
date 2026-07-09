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

        [Header("出した手のアイコン（選んだ手だけ表示予定）")]
        [SerializeField] private Image rockIcon;
        [SerializeField] private Image scissorsIcon;
        [SerializeField] private Image paperIcon;

        [Header("敗北者アイコン")]
        [SerializeField] private Image retireIcon;

        /// <summary>
        /// 初期化時のユーザー情報の適応
        /// </summary>
        public void SetData(string playerName)
        {
            nameText.text = playerName;
        }
        
        /// <summary>
        /// ジャッジの結果表示に使う
        /// Noneを引数にすれば表示を控えることもできる
        /// </summary>
        public void SetHand(HandType hand)
        {
            // 選んだ手だけ表示、他は隠す
            rockIcon.gameObject.SetActive(hand == HandType.Rock);
            scissorsIcon.gameObject.SetActive(hand == HandType.Scissors);
            paperIcon.gameObject.SetActive(hand == HandType.Paper);
        }
    }
}