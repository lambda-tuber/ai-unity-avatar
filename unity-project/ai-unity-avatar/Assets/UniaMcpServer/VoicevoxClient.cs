using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// VOICEVOX Web API クライアント（Singleton）
/// UniVRM に渡すために、
///  - audio_query(JSON)
///  - synthesis(WAVバイト列)
/// を取得するだけのクラス。
/// </summary>
public sealed class VoicevoxClient
{
    private static readonly Lazy<VoicevoxClient> _instance =
        new Lazy<VoicevoxClient>(() => new VoicevoxClient());

    public static VoicevoxClient Instance => _instance.Value;

    private readonly HttpClient _http;

    private const string BASE_URL = "http://127.0.0.1:50021";

    private VoicevoxClient()
    {
        _http = new HttpClient();
    }

    /// <summary>
    /// VOICEVOXに問い合わせて、UniVRMに渡すための
    /// queryJson と wavBytes を生成する。
    /// </summary>
    public async Task<(string queryJson, byte[] wavBytes)> GenerateAudioAsync(
        int styleId,
        string text,
        float speedScale = 1.0f,
        float pitchScale = 0.0f,
        float intonationScale = 1.0f,
        float volumeScale = 1.0f
    )
    {
        text = RemoveBracketText(text);

        // --- 1) audio_query ---
        var queryResponse = await _http.PostAsync(
            $"{BASE_URL}/audio_query?text={Uri.EscapeDataString(text)}&speaker={styleId}",
            null
        );

        if (!queryResponse.IsSuccessStatusCode)
            throw new Exception("VOICEVOX audio_query エラー");

        string queryJson = await queryResponse.Content.ReadAsStringAsync();

        // JSON をパラメータ調整して再構築
        var queryNode = JsonUtility.FromJson<AudioQueryWrapper>($"{{\"q\":{queryJson}}}");
        var q = queryNode.q;
        q.speedScale = speedScale;
        q.pitchScale = pitchScale;
        q.intonationScale = intonationScale;
        q.volumeScale = volumeScale;

        string modifiedQueryJson = JsonUtility.ToJson(q);
        modifiedQueryJson = queryJson;

        // --- 2) synthesis ---
        var content = new StringContent(modifiedQueryJson, Encoding.UTF8, "application/json");

        var synthesisResponse = await _http.PostAsync(
            $"{BASE_URL}/synthesis?speaker={styleId}",
            content
        );

        if (!synthesisResponse.IsSuccessStatusCode)
            throw new Exception("VOICEVOX synthesis エラー");

        byte[] wavBytes = await synthesisResponse.Content.ReadAsByteArrayAsync();

        return (modifiedQueryJson, wavBytes);
    }


    // ------------- 補助関数 -------------

    /// <summary>（ ）や（）などの括弧内文を削除</summary>
    private string RemoveBracketText(string text)
    {
        return System.Text.RegularExpressions.Regex
            .Replace(text, "（.*?）|\\(.*?\\)", "")
            .Trim();
    }


    // ------------- JSON パース用 -------------

    [Serializable]
    private class AudioQuery
    {
        public float speedScale;
        public float pitchScale;
        public float intonationScale;
        public float volumeScale;
    }

    [Serializable]
    private class AudioQueryWrapper
    {
        public AudioQuery q;
    }
}
