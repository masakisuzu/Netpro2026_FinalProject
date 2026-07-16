using Script.Utility;
using TMPro;
using UnityEngine;

namespace Script.UI
{
    /// <summary>
    /// InGameシーンでの1メンバー分の表示（名前＋出した手＋勝敗結果）
    /// </summary>
    public class InGameMemberStateView : MonoBehaviour
    {
        [Header("テキスト表示")]
        [SerializeField] private TextMeshProUGUI nameText;
        
        [Header("名前の色")]
        [SerializeField] private Color opColor = Color.black;
        [SerializeField] private Color myColor = Color.yellow;

        [Header("プレイヤーの状態アイコン")]
        [SerializeField] private GameObject defaultIcon; // 通常、考えてる時など
        [SerializeField] private GameObject retireIcon; // 負けたら常にコレ
        [SerializeField] private GameObject rockIcon;
        [SerializeField] private GameObject paperIcon;
        
        // 現在の状態を外から確認するためのプロパティ
        public IconType CurrentIcon { get; private set; }

        /// <summary>
        /// 初期化時のユーザー情報の適応。自分の画面に全員分を作るから何度も呼ばれる
        /// </summary>
        public void SetData(string playerName, bool isLocalPlayer)
        {
            nameText.text = playerName;
            nameText.color = isLocalPlayer ? myColor : opColor;
        }
        
        /// <summary>
        /// ジャッジの結果表示の時に使う（ボタン押した瞬間には呼ばれない）
        /// </summary>
        public void SetIcon(IconType type)
        {
            CurrentIcon = type;
            
            // 選んだタイプだけ表示し、他は隠れる
            defaultIcon.SetActive(type == IconType.Default);
            retireIcon.SetActive(type == IconType.Retire);
            rockIcon.SetActive(type == IconType.Rock);
            paperIcon.SetActive(type == IconType.Paper);
        }
    }
}