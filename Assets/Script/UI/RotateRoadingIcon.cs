using UnityEngine;

namespace Script.UI
{
    public class RotateRoadingIcon : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed; // 1秒あたりの回転角度（度）
        private RectTransform rectTransform;

        void Start()
        {
            // ImageはUIなのでRectTransformを取得
            rectTransform = GetComponent<RectTransform>();
        }

        void Update()
        {
            // Z軸を中心に等速回転
            rectTransform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
        }
    }
}