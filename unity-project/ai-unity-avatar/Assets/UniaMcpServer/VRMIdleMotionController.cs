using UnityEngine;

public class VRMIdleMotionController : MonoBehaviour
{
    private Animator animator;
    private float time = 0f;

    // Idle motion parameters
    private const float breatheAmplitude = 0.001f;  // 呼吸の振幅
    private const float breatheSpeed = 0.2f;        // 呼吸の速さ
    private const float swayAmplitude = 1.0f;      // 腰の左右揺れ
    private const float swaySpeed = 0.2f;             // 腰の揺れスピード
    private const float chestTwistAmplitude = 3f;  // 胸のY軸回転
    private const float chestTwistSpeed = 0.1f;      // 胸の回転スピード

    // Bone references
    private Transform hipsTransform;
    private Transform chestTransform;
    private Transform leftShoulderTransform;
    private Transform rightShoulderTransform;
    
    // Original positions and rotations
    private Vector3 originalHipsPosition;
    private Vector3 originalChestPosition;
    private Vector3 originalLeftShoulderPosition;
    private Vector3 originalRightShoulderPosition;
    
    private Quaternion originalHipsRotation;
    private Quaternion originalChestRotation;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found!");
            return;
        }

        hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
        chestTransform = animator.GetBoneTransform(HumanBodyBones.Chest);

        if (hipsTransform != null) originalHipsPosition = hipsTransform.localPosition;
        if (chestTransform != null) originalChestRotation = chestTransform.localRotation;

        Debug.Log("VRM Idle Motion Controller initialized");
    }


    void LateUpdate()
    {
      //  time += Time.deltaTime;

        // Idle motion を適用
//        ApplyBreathingMotion();
  //      ApplyHipsSway();
    //    ApplyChestTwist();

        time += Time.deltaTime;

        if (hipsTransform == null || chestTransform == null) return;

        //
        // ==============================
        // 呼吸（上下 + 前後の動き）
        // ==============================
        //
        float breathe = Mathf.Sin(time * breatheSpeed * Mathf.PI * 2f) * breatheAmplitude;

        Vector3 hipsBreatheOffset = new Vector3(0,  breathe,  breathe);
        Vector3 chestBreatheOffset = new Vector3(0,  breathe * 0.5f,  breathe * 0.5f);

        hipsTransform.localPosition = originalHipsPosition + hipsBreatheOffset;
        chestTransform.localPosition = originalChestPosition + chestBreatheOffset;


        //
        // ==============================
        // 腰（Hips）の左右スウェイ：X軸回転
        // ==============================
        //
        float hipsSway = Mathf.Sin(time * swaySpeed * Mathf.PI * 2f) * swayAmplitude;

        hipsTransform.localRotation =
            originalHipsRotation * Quaternion.Euler(hipsSway, 0, 0);


        //
        // ==============================
        // 胸（Chest）のひねり：Y軸回転
        // ==============================
        //
        float chestTwist = Mathf.Sin(time * chestTwistSpeed * Mathf.PI * 2f) * chestTwistAmplitude;

        chestTransform.localRotation =
            originalChestRotation * Quaternion.Euler(0, chestTwist, 0);

    }

    private void ApplyBreathingMotion()
    {
        if (hipsTransform == null || chestTransform == null) return;

        // 呼吸による上下動
        float breatheOffset = Mathf.Sin(time * breatheSpeed * Mathf.PI) * breatheAmplitude;
        
        Vector3 hipsPos = originalHipsPosition;
        hipsPos.y += breatheOffset;
        hipsTransform.localPosition = hipsPos;

        Vector3 chestPos = originalChestPosition;
        chestPos.y += breatheOffset * 0.5f;
        chestTransform.localPosition = chestPos;
    }

    private void ApplyHipsSway()
    {
        if (hipsTransform == null) return;

        float swayOffset = Mathf.Sin(time * swaySpeed * Mathf.PI * 2f) * swayAmplitude;

        Vector3 hipsPos = originalHipsPosition;
        hipsPos.x += swayOffset; // X軸左右揺れ
        hipsTransform.localPosition = hipsPos;
    }

    private void ApplyChestTwist()
    {
        if (chestTransform == null) return;

        float twistAngle = Mathf.Sin(time * chestTwistSpeed * Mathf.PI * 2f) * chestTwistAmplitude;

        Quaternion twistRotation = originalChestRotation * Quaternion.Euler(0f, twistAngle, 0f); // Y軸回転
        chestTransform.localRotation = twistRotation;
    }


    // Idle motion を有効/無効にするメソッド
    public void SetIdleMotionEnabled(bool enabled)
    {
        enabled = enabled;
    }
}
