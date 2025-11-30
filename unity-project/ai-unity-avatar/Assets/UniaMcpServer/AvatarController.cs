using UnityEngine;
using Cysharp.Threading.Tasks;
using UniVRM10;
using System; // BitConverterなどに必要

/// <summary>
/// アバターの表情や動作、音声を制御するマネージャークラス
/// </summary>
// RequireComponent(typeof(AudioSource)) は AvatarController自体のGameObjectにAudioSourceをアタッチするので、
// VRMインスタンスのAudioSourceを使う場合はこれは不要、または別途管理が必要
// 今回はVRMインスタンスのAudioSourceを使うため、一旦この属性は削除
// [RequireComponent(typeof(AudioSource))] 
public class AvatarController : MonoBehaviour
{
    private static AvatarController _instance;

    // VRMインスタンスへの参照
    private Vrm10Runtime vrmRuntime;
    
    // --- 変更: 音声再生用コンポーネントは、VRMインスタンスから取得したものを使う ---
    private AudioSource _audioSource; // これが実際に音を鳴らすAudioSource
    //private VRM10LipSyncFromAudioSource _vrmLipSync; // リップシンク設定用コンポーネント
    // private VRM10AudioSource _vrmAudioSource; // このコンポーネントは直接使わないため不要（あるいは参照だけ持ってもよい）

    // 表情キー
    private ExpressionKey smileKey = ExpressionKey.CreateFromPreset(ExpressionPreset.happy);

    [Header("Smile Settings")]
    public float smileFadeIn = 0.25f;
    public float smileDuration = 0.7f;
    public float smileFadeOut = 0.25f;
    public float smileMaxWeight = 0.7f;

    private ExpressionKey mouthBlendKey;

    public static AvatarController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AvatarController>();
                if (_instance == null)
                {
                    var go = new GameObject("AvatarController");
                    _instance = go.AddComponent<AvatarController>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // AwakeではAudioSourceをアタッチしない
        // AudioSourceはVRMインスタンスにアタッチされているものを使う
    }

    /// <summary>
    /// VRMインスタンスを登録し、そのインスタンスに紐づくAudioSourceとLipSyncコンポーネントをセットアップします。
    /// </summary>
    public void SetVrmInstance(Vrm10Instance instance)
    {
        if (instance == null)
        {
            Debug.LogError("VRM instance is null!");
            return;
        }

        vrmRuntime = instance.Runtime;
        Debug.Log("VRM instance registered to AvatarController");

        // --- 変更点ここから ---
        // 1. VRMインスタンスに標準のAudioSourceがアタッチされているか確認し、なければ追加
        _audioSource = instance.GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = instance.gameObject.AddComponent<AudioSource>();
            Debug.Log("Added AudioSource to VRM instance.");
        }
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        // 2. VRMインスタンスにVRM10LipSyncFromAudioSourceがアタッチされているか確認し、なければ追加
        // _vrmLipSync = instance.GetComponent<VRM10LipSyncFromAudioSource>();
        // if (_vrmLipSync == null)
        // {
        //     _vrmLipSync = instance.gameObject.AddComponent<VRM10LipSyncFromAudioSource>();
        //     Debug.Log("Added VRM10LipSyncFromAudioSource to VRM instance.");
        // }

        // 3. VRM10LipSyncFromAudioSourceに、今見つけたAudioSourceとVRM10Controllerをセット
        // _vrmLipSync.Target = instance.GetComponent<VRM10Controller>(); // VRM10Controllerは通常VRMインスタンスのルートにある
        // _vrmLipSync.AudioSource = _audioSource; // 今見つけた（または追加した）AudioSourceをセット

        Debug.Log("VRM instance AudioSource and LipSync setup complete.");
        // --- 変更点ここまで ---

        // 口パク用 BlendShape を設定
        mouthBlendKey = new ExpressionKey(ExpressionPreset.oh);
    }

    // =======================================================================
    // 追加機能: 音声発話関連 (変更なし)
    // =======================================================================

    /// <summary>
    /// WAVバイナリデータをAudioClipに変換する静的ユーティリティ関数。
    /// インスタンスの状態に依存しないため static メソッドとしています。
    /// (16bit PCM WAVのみ対応の簡易実装)
    /// </summary>
    /// <param name="wavFileBytes">WAVファイルの全バイトデータ</param>
    /// <returns>生成されたAudioClip。失敗時はnull</returns>
    public static AudioClip ToAudioClip(byte[] wavFileBytes)
    {
        // 以前のToAudioClipメソッドの内容をそのまま使用
        // ...
        if (wavFileBytes == null || wavFileBytes.Length < 44)
        {
            Debug.LogError("AvatarController: WAVデータが無効か短すぎます。");
            return null;
        }

        try
        {
            int channels = BitConverter.ToInt16(wavFileBytes, 22);
            int frequency = BitConverter.ToInt32(wavFileBytes, 24);
            int bitDepth = BitConverter.ToInt16(wavFileBytes, 34);

            if (bitDepth != 16)
            {
                Debug.LogError($"AvatarController: 16bit WAVのみ対応しています。入力は{bitDepth}bitでした。");
                return null;
            }

            int headerSize = 44; 
            int pcmDataSizeBytes = wavFileBytes.Length - headerSize;
            int bytesPerSample = bitDepth / 8; 
            int totalSampleCount = pcmDataSizeBytes / bytesPerSample;
            
            float[] floatData = new float[totalSampleCount];
            float max16BitValue = short.MaxValue; 

            int byteIndex = headerSize;
            for (int i = 0; i < totalSampleCount; i++)
            {
                short shortValue = BitConverter.ToInt16(wavFileBytes, byteIndex);
                floatData[i] = shortValue / max16BitValue;
                byteIndex += bytesPerSample;
            }

            int lengthSamples = totalSampleCount / channels;
            AudioClip audioClip = AudioClip.Create("GeneratedVoice", lengthSamples, channels, frequency, false);
            audioClip.SetData(floatData, 0);

            return audioClip;
        }
        catch (Exception e)
        {
            Debug.LogError($"AvatarController: WAV変換中にエラーが発生しました。\n{e}");
            return null;
        }
    }

    /// <summary>
    /// WAVデータを受け取り、メインスレッドで発話させる非同期メソッド。
    /// サブスレッドから呼び出しても安全です。(AvatarController.Instance.SpeakAsync(wavData).Forget() のように使用)
    /// </summary>
    /// <param name="wavData">WAVバイナリデータ</param>
    public async UniTask SpeakAsync(byte[] wavData)
    {
        if (_audioSource == null)
        {
            Debug.LogError("AvatarController: VRMインスタンスのAudioSourceが設定されていません。VRMインスタンスの登録を確認してください。");
            return;
        }

        AudioClip clip = ToAudioClip(wavData);
        if (clip == null)
        {
            Debug.LogError("AvatarController: AudioClipが不正。");
            return;
        }

        await UniTask.SwitchToMainThread();

        Debug.Log("🗣️ Avatar: Start speaking...");
        if (_audioSource.isPlaying) _audioSource.Stop();
        
        _audioSource.clip = clip;
        _audioSource.Play();

        await LipSyncAsync();
    }

    private async UniTask LipSyncAsync()
    {
        await UniTask.SwitchToMainThread();

        var clip = _audioSource.clip;
        if (clip == null) return;

        _audioSource.Play();

        float[] samples = new float[1024];
        while (_audioSource.isPlaying)
        {
            _audioSource.GetOutputData(samples, 0);
            float level = 0;
            foreach (var s in samples) level += Mathf.Abs(s);
            level /= samples.Length;

            // mouthBlendKey は口パク BlendShape
            vrmRuntime.Expression.SetWeight(mouthBlendKey, Mathf.Clamp01(level * 5f));

            await UniTask.Yield();
        }

        // 最後に口を閉じる
        vrmRuntime.Expression.SetWeight(mouthBlendKey, 0f);
    }

    // =======================================================================
    // 既存機能 (変更なし)
    // =======================================================================

    // SetSmileAsync, SetEmotionAsync は変更なし
    // ...
    /// <summary>
    /// 笑顔の表情を設定（非同期、スレッドセーフ）
    /// </summary>
    public async UniTask SetSmileAsync()
    {
        // メインスレッドに切り替え
        await UniTask.SwitchToMainThread();

        if (vrmRuntime == null)
        {
            Debug.LogWarning("VRM Runtime is not set. Cannot apply smile.");
            return;
        }

        Debug.Log("🙂 Avatar: Starting smile animation...");

        float t = 0;
        while (t < smileFadeIn)
        {
            float w = Mathf.Lerp(0, smileMaxWeight, t / smileFadeIn);
            vrmRuntime.Expression.SetWeight(smileKey, w);
            t += Time.deltaTime;
            await UniTask.Yield();
        }
        vrmRuntime.Expression.SetWeight(smileKey, smileMaxWeight);

        await UniTask.Delay(System.TimeSpan.FromSeconds(smileDuration));

        t = 0;
        while (t < smileFadeOut)
        {
            float w = Mathf.Lerp(smileMaxWeight, 0, t / smileFadeOut);
            vrmRuntime.Expression.SetWeight(smileKey, w);
            t += Time.deltaTime;
            await UniTask.Yield();
        }
        
        vrmRuntime.Expression.SetWeight(smileKey, 0);
        Debug.Log("😊 Avatar: Smile animation completed!");
    }

    /// <summary>
    /// 汎用的な表情設定メソッド（非同期、スレッドセーフ、将来の拡張用）
    /// </summary>
    public async UniTask SetEmotionAsync(string emotion)
    {
        await UniTask.SwitchToMainThread();
        Debug.Log($"😊 Avatar: Emotion '{emotion}' applied!");
    }
}