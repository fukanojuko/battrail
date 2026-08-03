using UnityEngine;
using UnityEngine.UI;

namespace Battrail
{
    /// ブースト中だけ集中線を出す。マテリアルの _Alpha を出し入れして濃度を制御する。
    /// 分割画面なので Canvas は Screen Space - Camera で親カメラのビューポートにだけ描画する
    /// （Overlay だとポストプロセス後の描画になり、HDR 発光色に Bloom が乗らない）。
    [RequireComponent(typeof(Canvas))]
    public class BoostConcentrationLine : MonoBehaviour
    {
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] int playerIndex = 0;
        [SerializeField] RawImage line;

        [Header("Fade")]
        [Tooltip("ブースト中の濃さ（マテリアルの Alpha）")]
        [SerializeField] float maxAlpha = 0.35f;
        [SerializeField] float fadeInDuration = 0.12f;
        [SerializeField] float fadeOutDuration = 0.3f;

        Racer _racer;
        Material _material;
        float _alpha;

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = GetComponentInParent<Camera>();

            // マテリアルは複製して使う。共有のままだと 2 画面が同じ濃度で動き、
            // さらにプロジェクトの .mat 自体が Play 中に書き換わってしまう。
            _material = new Material(line.material);
            line.material = _material;

            ApplyAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private void Update()
        {
            if (_racer == null)
                _racer = Racer.Find(playerIndex);
            if (_racer == null)
                return;

            float target = _racer.IsBoosting ? maxAlpha : 0f;
            float duration = _racer.IsBoosting ? fadeInDuration : fadeOutDuration;
            float speed = maxAlpha / Mathf.Max(0.0001f, duration);

            ApplyAlpha(Mathf.MoveTowards(_alpha, target, speed * Time.deltaTime));
        }

        void ApplyAlpha(float alpha)
        {
            _alpha = alpha;
            _material.SetFloat(AlphaId, alpha);
            // 消えている間は描画に出さない。
            line.enabled = alpha > 0f;
        }
    }
}
