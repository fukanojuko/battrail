using UnityEngine;

namespace Battrail.Core
{
    /// 起動時に描画バックエンドの能力をログに出す。
    /// VFX Graph（MasterTrail / BasicImpacts）はコンピュートシェーダー必須で、
    /// WebGL 2.0 では無音のまま一切描画されない。Web ビルドは WebGPU を優先し
    /// WebGL 2.0 にフォールバックする設定なので、フォールバックした環境では
    /// 「エフェクトだけ出ない」状態になる。原因が追えるようここで警告を出す。
    public static class GraphicsCapabilityLog
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Log()
        {
            if (SystemInfo.supportsComputeShaders)
                return;

            Debug.LogWarning(
                $"[Battrail] Compute shaders are unavailable on {SystemInfo.graphicsDeviceType} " +
                $"({SystemInfo.graphicsDeviceVersion}). VFX Graph effects will not render. " +
                "On the web build this means the browser fell back to WebGL 2.0 instead of WebGPU.");
        }
    }
}
