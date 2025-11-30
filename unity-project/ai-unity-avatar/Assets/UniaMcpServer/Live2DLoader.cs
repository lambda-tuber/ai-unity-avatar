using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Framework.Physics;
using Live2D.Cubism.Framework.LookAt;

using Live2D.Cubism.Framework.MouthMovement;
using Live2D.Cubism.Framework.HarmonicMotion;

public class Live2DLoader : MonoBehaviour
{
    private GameObject live2DInstance;
    private CubismModel cubismModel;

    async void Start()
    {
        string modelJsonPath = Path.Combine(Application.dataPath, "SampleLive2D_miku/runtime/miku.model3.json");
        //string modelJsonPath = Path.Combine(Application.dataPath, "SampleLive2D_Epsilon_free/runtime/Epsilon_free.model3.json");

        if (File.Exists(modelJsonPath))
        {
            await LoadLive2DAsync(modelJsonPath);
        }
        else
        {
            Debug.LogError($"[Live2D] Model not found: {modelJsonPath}");
        }
    }

    public async Task LoadLive2DAsync(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[Live2D] File not found: {path}");
            return;
        }

        try
        {
            if (live2DInstance != null)
            {
                Destroy(live2DInstance);
                live2DInstance = null;
                cubismModel = null;
            }

            Debug.Log("[Live2D] Loading model...");

            CubismModel3Json.LoadAssetAtPathHandler loader = (type, p) =>
            {
                if (type == typeof(byte[])) return File.ReadAllBytes(p);
                if (type == typeof(string)) return File.ReadAllText(p);

                if (type == typeof(Texture2D))
                {
                    var bytes = File.ReadAllBytes(p);
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(bytes);
                    return tex;
                }
                return null;
            };

            // Cubism5はboolが第2引数
            var model3Json = CubismModel3Json.LoadAtPath(path, loader);

            if (model3Json == null)
            {
                Debug.LogError("[Live2D] Failed to parse model3.json");
                return;
            }

            // モデル生成（Cubism 5はCubismModelを返す）
            cubismModel = model3Json.ToModel(true);

            if (cubismModel == null)
            {
                Debug.LogError("[Live2D] Failed to create model instance.");
                return;
            }

            // GameObject取得
            live2DInstance = cubismModel.gameObject;

            if (live2DInstance == null)
            {
                Debug.LogError("[Live2D] Failed to create model instance.");
                return;
            }

            live2DInstance.transform.SetParent(this.transform, false);
            //cubismModel = live2DInstance.GetComponent<CubismModel>();

            live2DInstance.name = "Live2D_Avatar";

            Debug.Log("[Live2D] Model loaded.");

            // コンポーネントのセットアップ（描画順序制御を追加）
            SetupComponents(live2DInstance);

            // 位置・スケール設定
            //live2DInstance.transform.localPosition = Vector3.zero;
            live2DInstance.transform.localPosition = new Vector3(0.5f, 0f, 0f); 
            live2DInstance.transform.localScale = Vector3.one * 1.2f;

            // カメラ設定
            SetupCamera();

            Debug.Log("[Live2D] Setup completed.");

            await Task.Yield();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Live2D] Error: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SetupComponents(GameObject model)
    {
        // ★ 描画順序制御コンポーネントを追加
        var renderController = model.AddComponent<CubismRenderController>();
        
        // レンダリング順序のソート方式を設定
        renderController.SortingMode = CubismSortingMode.BackToFrontOrder;

        var cubismModel = model.GetComponent<CubismModel>();
        var blink = model.AddComponent<CubismEyeBlinkController>();
        //blink.EyeOpening = 1.0f; 
        //blink.BlendMode = CubismParameterBlendMode.Multiply;
        //blink.BlendMode = CubismParameterBlendMode.Override;
        blink.BlendMode = CubismParameterBlendMode.Additive;
        //blink.BlendMode = CubismParameterBlendMode.Override; // Overrideに戻す
        blink.EyeOpening = 0.01f; // 開いている時の値

        var blinkInput = model.AddComponent<CubismAutoEyeBlinkInput>();
        blinkInput.Mean = 2.5f; // まばたき間隔を長めに設定
        blinkInput.MaximumDeviation = 2.0f; // ランダム性を追加
        blinkInput.Timescale = 10.0f;
        blinkInput.SetBlinkingSettings(0.01f, 0.00f, 0.01f);

        var harmonicMotion = model.AddComponent<CubismHarmonicMotionController>();
        harmonicMotion.BlendMode = CubismParameterBlendMode.Additive;
        //harmonic.ChannelTimescales = new float[] { 1.0f };
        //harmonic.Refresh();

       
        // マウス追従・視線追従の設定
        var lookController = model.AddComponent<CubismLookController>();
        lookController.BlendMode = CubismParameterBlendMode.Additive;
        lookController.Center = this.transform; // Transformを渡す

        //var physics = model.AddComponent<CubismPhysicsController>();

        Debug.Log("[Live2D] CubismRenderController added.");
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = 1.5f;
        cam.transform.position = new Vector3(0, 1.0f, -10f);
        cam.transform.LookAt(new Vector3(0, 1.0f, 0));
        
        // カメラのクリッピング範囲を適切に設定
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        
        Debug.Log("[Live2D] Camera setup completed.");
    }

    void OnDestroy()
    {
        if (live2DInstance)
            Destroy(live2DInstance);
    }

}