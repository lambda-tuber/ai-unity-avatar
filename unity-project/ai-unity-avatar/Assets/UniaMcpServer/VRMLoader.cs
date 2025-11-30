using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UniGLTF;
using UniVRM10;

public class VRMLoader : MonoBehaviour
{
    private GameObject vrmInstance;

    async void Start()
    {
        // 開発環境に応じて、適切なパスを設定してください。
        string vrmPath = Path.Combine(Application.dataPath, "SampleVRM.vrm");

        if (File.Exists(vrmPath))
        {
            await LoadVRMAsync(vrmPath);
        }
        else
        {
            Debug.LogError($"VRM file not found at: {vrmPath}");
        }
    }
    
    public async Task LoadVRMAsync(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"VRM file not found: {path}");
            return;
        }

        try
        {
            // 既存のVRMを破棄 (VRM_Rootごと破棄)
            if (vrmInstance != null)
            {
                // vrmInstanceの親オブジェクト（VRM_Root）を破棄します。
                Destroy(vrmInstance.transform.root.gameObject);
                vrmInstance = null;
            }

            // VRMロード
            Vrm10Instance instance = await Vrm10.LoadPathAsync(path);
            vrmInstance = instance.gameObject;
            vrmInstance.name = "VRM_Avatar";
            AvatarController.Instance.SetVrmInstance(instance);

            // 親オブジェクトを作って回転を付与
            GameObject vrmWrapper = new GameObject("VRM_Root");
            
            // 正面向きに調整（Y軸180度回転）を先に適用
            //vrmWrapper.transform.rotation = Quaternion.Euler(0, 90, 90);
            vrmWrapper.transform.rotation = Quaternion.Euler(0, 180, 0);
            vrmWrapper.transform.SetParent(this.transform, false);

            // VRM本体をvrmWrapperの子にし、localTransformを初期化
            vrmInstance.transform.SetParent(vrmWrapper.transform, false);
            vrmInstance.transform.localPosition = Vector3.zero;
            vrmInstance.transform.localRotation = Quaternion.identity;

            Debug.Log($"VRM loaded successfully: {vrmInstance.name}");

            // Animatorコンポーネントを取得または追加
            Animator animator = vrmInstance.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("No Animator component found. Adding one...");
                animator = vrmInstance.AddComponent<Animator>();
            }

            // アニメーションコントローラーを読み込み・設定
            RuntimeAnimatorController idleController = Resources.Load<RuntimeAnimatorController>(
                "UnityChan/Animators/UnityChanActionCheck"
            );

            if (idleController != null)
            {
                animator.runtimeAnimatorController = idleController;
                Debug.Log("Idle animation controller loaded and assigned successfully!");
            }
            else
            {
                Debug.LogError("Failed to load Idle animation controller. Check the resource path.");
            }

            // VRMIdleMotionController をアタッチ
            //vrmInstance.AddComponent<VRMIdleMotionController>();
            //Debug.Log("VRMIdleMotionController attached successfully!");

            // 自動まばたき & 自動笑顔
            vrmInstance.AddComponent<Vrm10AutoBlink>();
            //vrmInstance.AddComponent<VRMAutoSmile>(); 
            Debug.Log("Vrm10AutoBlink attached successfully!");


            // ------------------------
            // 画面の中央にアバター全体を配置
            // ------------------------
            
            // 1. アバターのワールド座標でのバウンディングボックスを計算
            Bounds bounds = new Bounds(vrmInstance.transform.position, Vector3.zero);
            var renderers = vrmInstance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                // ワールド座標でのバウンディングボックスを統合
                bounds.Encapsulate(r.bounds);
            }

            Vector3 boundsCenter = bounds.center; // ワールド座標での中心
            float boundsHeight = bounds.size.y;   // アバターの高さ
            
            // 2. VRM_Root の新しいワールドポジションを計算
            // アバターの中心 (X, Y, Z) がワールド原点 (0, 0, 0) になるように、VRM_Root を移動させます。
            
            Vector3 newWorldPosition = vrmWrapper.transform.position; // 初期位置

            // X, Y, Z 全てにおいて、現在のアバターの中心を打ち消す量だけ移動
            newWorldPosition.x -= boundsCenter.x;
            newWorldPosition.y -= boundsCenter.y; 
            newWorldPosition.z -= boundsCenter.z;

            // 3. VRM_Root の新しいワールドポジションを適用
            vrmWrapper.transform.position = newWorldPosition;

            Debug.Log($"[VRM Debug] boundsCenter(World): {boundsCenter}");
            Debug.Log($"[VRM Debug] boundsSize: {bounds.size}");
            Debug.Log($"[VRM Debug] final VRM_Root position: {vrmWrapper.transform.position}");

            // ------------------------
            // カメラの位置を調整（胸のあたりを中心に、全身が収まるように）
            // ------------------------
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // カメラ位置を高くする分、距離も少し遠くする必要がある
                float verticalFOV = mainCamera.fieldOfView;
                
                // 胸のあたりの高さ（アバターの高さの約35%上）
                float chestHeight = boundsHeight * 0.15f;
                
                // カメラから見える範囲を考慮して距離を計算
                // カメラが上にある分、下の足まで見えるように距離を調整
                float distance = (boundsHeight * 0.65f) / Mathf.Tan(verticalFOV * 0.5f * Mathf.Deg2Rad);
                
                // カメラをアバターの正面に配置
                mainCamera.transform.position = new Vector3(0, chestHeight, -distance);
                
                // カメラをアバター全体に向ける（少し下向き）
                // アバターの中心（原点）を見るように回転
                mainCamera.transform.LookAt(Vector3.zero);
                
                Debug.Log($"[Camera Debug] Camera position: {mainCamera.transform.position}");
                Debug.Log($"[Camera Debug] Camera rotation: {mainCamera.transform.rotation.eulerAngles}");
                Debug.Log($"[Camera Debug] Distance: {distance}");
                Debug.Log($"[Camera Debug] Chest height offset: {chestHeight}");
            }
            else
            {
                Debug.LogWarning("Main Camera not found!");
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading VRM: {e.Message}\n{e.StackTrace}");
        }
    }

    void OnDestroy()
    {
        // VRM_Root があればそれを破棄します
        if (vrmInstance != null)
        {
            // vrmInstanceの親オブジェクト（VRM_Root）を破棄
            Destroy(vrmInstance.transform.root.gameObject);
        }
    }
}