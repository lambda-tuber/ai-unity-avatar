using UnityEngine;
using UniVRM10;
using System.Collections;

public class Vrm10AutoBlink : MonoBehaviour
{
    private Vrm10Runtime runtime;

    [Header("Blink Settings")]
    public float minInterval = 2f;     // 最短まばたき間隔
    public float maxInterval = 5f;     // 最長まばたき間隔
    public float blinkCloseTime = 0.05f; // 目を閉じるまでの時間
    public float blinkDuration = 0.08f;  // 閉じている時間
    public float blinkOpenTime = 0.05f;  // 開くまでの時間

    private float nextBlink = 0f;

    void Start()
    {
        runtime = GetComponent<Vrm10Instance>()?.Runtime;
        if (runtime == null)
        {
            Debug.LogError("Vrm10AutoBlink: Vrm10Runtime が見つかりません。VRM1.0モデルにアタッチしてください。");
            enabled = false;
            return;
        }

        SetNextBlinkTime();
    }

    void Update()
    {
        nextBlink -= Time.deltaTime;
        if (nextBlink <= 0)
        {
            StartCoroutine(Blink());
            SetNextBlinkTime();
        }
    }

    private IEnumerator Blink()
    {
        // -------------------------------
        // フェードで閉じる
        // -------------------------------
        float t = 0;
        while (t < blinkCloseTime)
        {
            float weight = Mathf.Lerp(0f, 1f, t / blinkCloseTime);
            runtime.Expression.SetWeight(ExpressionKey.Blink, weight);
            t += Time.deltaTime;
            yield return null;
        }
        runtime.Expression.SetWeight(ExpressionKey.Blink, 1f);

        yield return new WaitForSeconds(blinkDuration);

        // -------------------------------
        // フェードで開く
        // -------------------------------
        t = 0;
        while (t < blinkOpenTime)
        {
            float weight = Mathf.Lerp(1f, 0f, t / blinkOpenTime);
            runtime.Expression.SetWeight(ExpressionKey.Blink, weight);
            t += Time.deltaTime;
            yield return null;
        }
        runtime.Expression.SetWeight(ExpressionKey.Blink, 0f);
    }

    private void SetNextBlinkTime()
    {
        nextBlink = Random.Range(minInterval, maxInterval);
    }
}
