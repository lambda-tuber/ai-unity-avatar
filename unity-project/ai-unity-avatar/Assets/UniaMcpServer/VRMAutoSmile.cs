using UnityEngine;
using UniVRM10; // PresetName.Happy がこの名前空間に含まれています
using System.Collections;


public class VRMAutoSmile : MonoBehaviour
{
    private Vrm10Runtime runtime;

    // VRM1.0 の「Happy（笑顔）」プリセット
    private ExpressionKey smileKey = ExpressionKey.CreateFromPreset(UniVRM10.ExpressionPreset.happy);

    [Header("Smile Settings")]
    public float minSmileInterval = 5f;
    public float maxSmileInterval = 12f;
    public float smileFadeIn = 0.25f;
    public float smileDuration = 0.7f;
    public float smileFadeOut = 0.25f;
    public float smileMaxWeight = 0.7f;

    private float timer = 0f;
    private float nextSmileTime = 0f;

    void Start()
    {
        var inst = GetComponent<Vrm10Instance>();
        if (inst == null)
        {
            Debug.LogError("VRMAutoSmile: Vrm10Instance が見つかりません。");
            enabled = false;
            return;
        }

        runtime = inst.Runtime;

        SetNextSmileTime();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= nextSmileTime)
        {
            StartCoroutine(DoSmile());
            SetNextSmileTime();
        }
    }

    private IEnumerator DoSmile()
    {
        float t = 0;

        // --- Fade In ---
        while (t < smileFadeIn)
        {
            float w = Mathf.Lerp(0, smileMaxWeight, t / smileFadeIn);
            runtime.Expression.SetWeight(smileKey, w);
            t += Time.deltaTime;
            yield return null;
        }
        runtime.Expression.SetWeight(smileKey, smileMaxWeight);

        yield return new WaitForSeconds(smileDuration);

        // --- Fade Out ---
        t = 0;
        while (t < smileFadeOut)
        {
            float w = Mathf.Lerp(smileMaxWeight, 0, t / smileFadeOut);
            runtime.Expression.SetWeight(smileKey, w);
            t += Time.deltaTime;
            yield return null;
        }

        runtime.Expression.SetWeight(smileKey, 0);
    }

    private void SetNextSmileTime()
    {
        timer = 0;
        nextSmileTime = Random.Range(minSmileInterval, maxSmileInterval);
    }
}