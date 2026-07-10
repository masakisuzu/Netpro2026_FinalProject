using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    /// <summary>
    /// メンバー一覧の1行分（ReadyIcon + NAME）を管理・表示する
    /// </summary>
    public class MemberListItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image readyIcon; // 準備完了時に表示するアイコン画像

        public void SetData(string playerName, bool isReady)
        {
            nameText.text = playerName;
            readyIcon.gameObject.SetActive(isReady); // Ready状態に応じて表示/非表示を切り替え
        }
    }
}