using UnityEngine;

namespace Battrail
{
    /// トレイルの見た目だけを担当（現状は仮の LineRenderer、後で VFX Graph に差し替える）。
    /// CombatManager からは色とワールド座標列を渡すだけで、描画方法の詳細には関知しない。
    public sealed class TrailVisual : MonoBehaviour
    {
        LineRenderer _line;

        public void Initialize(Color color, float width)
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.widthMultiplier = width;
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
            _line.useWorldSpace = true;
            _line.positionCount = 0;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            _line.material = material;
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        /// pointCount + 1（先端分）で位置列を書き込む。呼び出し側が SetPoint を pointCount+1 回呼ぶ前提。
        public void BeginUpdate(int pointCount)
        {
            _line.positionCount = pointCount + 1;
        }

        public void SetPoint(int index, Vector3 position)
        {
            _line.SetPosition(index, position);
        }
    }
}
