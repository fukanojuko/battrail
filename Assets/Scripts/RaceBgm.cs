using UnityEngine;

namespace Battrail
{
    /// レース BGM。カウントダウン明け（RaceManager.RaceStarted）で再生し、決着でフェードアウトする。
    /// clip 未設定なら何も再生しないだけなので、音源が入る前からシーンに置いておける。
    [RequireComponent(typeof(RaceManager))]
    public class RaceBgm : MonoBehaviour
    {
        [Tooltip("レース中に流す曲。未設定なら何も再生しない")]
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 0.3f;
        [Tooltip("決着後にフェードアウトしきるまでの時間（秒）")]
        [SerializeField] float fadeOutSeconds = 1.5f;

        AudioSource _source;

        private void Awake()
        {
            var raceManager = GetComponent<RaceManager>();
            raceManager.RaceStarted += OnRaceStarted;
            raceManager.RaceFinished += OnRaceFinished;

            // BGM としての設定はコード側で確定させる（シーン側の付け替えで 3D 化・自動再生にならないように）。
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = volume;
        }

        void OnRaceStarted()
        {
            if (clip == null)
                return;

            _source.volume = volume;
            _source.Play();
        }

        // Awaitable は MonoBehaviour の破棄／シーン遷移で自動キャンセルされるため、
        // async void で起動しっぱなしにして問題ない（PostRaceController と同じパターン）。
        async void OnRaceFinished(Racer winner)
        {
            if (!_source.isPlaying)
                return;

            if (fadeOutSeconds <= 0f)
            {
                _source.Stop();
                return;
            }

            float from = _source.volume;
            for (float elapsed = 0f; elapsed < fadeOutSeconds; elapsed += Time.deltaTime)
            {
                _source.volume = Mathf.Lerp(from, 0f, elapsed / fadeOutSeconds);
                await Awaitable.NextFrameAsync();
            }

            _source.Stop();
        }
    }
}
