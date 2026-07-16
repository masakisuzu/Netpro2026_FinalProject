using UnityEngine;

namespace Script.Utility
{
    /// <summary>
    /// メインカメラにアタッチする。
    /// 画面のアスペクト比が targetAspect (デフォルト16:9) と異なる場合に
    /// Camera.rect を調整してレターボックス(上下黒帯) / ピラーボックス(左右黒帯) を作る。
    ///
    /// 重要: Camera.rect の外側は「誰も描画しない」領域になるだけで、
    /// 自動的に黒くなるわけではない。前フレームの描画がそのまま残り続けるため、
    /// このスクリプトは実行時に「背景を毎フレーム黒でクリアするだけの専用カメラ」を
    /// 自動生成し、rect外側を確実に黒く保つ。手動でカメラをもう1台用意する必要はない。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AspectRatioFitter : MonoBehaviour
    {
        // 固定したい比率。1920 x 1080 なら 16f / 9f
        private const float targetAspect = 16f / 9f;

        // 余白を埋める色。基本は黒色
        private Color letterboxColor = Color.black;

        private Camera cam;
        private Camera backgroundCam;
        
        // 前Fの比率を保持することで途中でモニターを変えるなどに対応できる
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            CreateBackgroundCamera();
            ApplyAspect();
        }

        /// <summary>
        /// 画面全体を毎フレーム黒でクリアするだけのカメラを、メインカメラの子として自動生成する。
        /// Culling Maskは何も描画しないよう Nothing に設定し、
        /// Depthはメインカメラより必ず低くして「先に描画される(=下地になる)」ようにする。
        /// これによりrect外側に前フレームの絵が残る問題を防ぐ。
        /// </summary>
        private void CreateBackgroundCamera()
        {
            var go = new GameObject("LetterboxBackgroundCamera");
            go.transform.SetParent(transform, false);
            backgroundCam = go.AddComponent<Camera>();

            backgroundCam.clearFlags = CameraClearFlags.SolidColor;
            backgroundCam.backgroundColor = letterboxColor;
            backgroundCam.cullingMask = 0; // Nothing = 何も描画しない、クリアのみ行う
            backgroundCam.rect = new Rect(0f, 0f, 1f, 1f); // 常にフルスクリーン
            backgroundCam.depth = cam.depth - 1f; // 必ずメインカメラより先に描画
            backgroundCam.useOcclusionCulling = false;
            backgroundCam.allowHDR = false;
            backgroundCam.allowMSAA = false;
        }

        private void Update()
        {
            // 実機の回転・ウィンドウのリサイズに追従させたい場合は毎フレームチェック
            // (負荷が気になる場合はOnRectTransformDimensionsChange等に置き換えてもよい)
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                ApplyAspect();
        }

        private void ApplyAspect()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            float windowAspect = (float)Screen.width / Screen.height;
            float scaleHeight = windowAspect / targetAspect;

            Rect rect = cam.rect;

            if (scaleHeight < 1f)
            {
                // 画面が想定より縦長 → 上下に黒帯 (レターボックス)
                rect.width = 1f;
                rect.height = scaleHeight;
                rect.x = 0f;
                rect.y = (1f - scaleHeight) / 2f;
            }
            else
            {
                // 画面が想定より横長 → 左右に黒帯 (ピラーボックス)
                float scaleWidth = 1f / scaleHeight;
                rect.width = scaleWidth;
                rect.height = 1f;
                rect.x = (1f - scaleWidth) / 2f;
                rect.y = 0f;
            }

            cam.rect = rect;
        }
    }
}