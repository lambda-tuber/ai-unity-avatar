using UnityEngine;
using System.Runtime.InteropServices;
using System;  

public class WindowDragHandler : MonoBehaviour
{
    [DllImport("user32.dll")] private static extern System.IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    const int WM_NCLBUTTONDOWN = 0x00A1;
    const int HTCAPTION = 2;

    private IntPtr windowHandle;
    private bool canDrag = false;

    void Start()
    {
        windowHandle = GetActiveWindow();
    }

    void Update()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        // マウスダウン時の透明判定
        if (mouse.leftButton.wasPressedThisFrame)
        {
            canDrag = IsMouseOverAvatarOnce();

            if (canDrag)
            {
                // Windows の標準ドラッグ処理を実行（最強）
                ReleaseCapture();
                SendMessage(windowHandle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }
    }

    private bool IsMouseOverAvatarOnce()
    {
        Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.value;

        int x = (int)pos.x;
        int y = (int)(Screen.height - pos.y);

        Texture2D t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        t.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        t.Apply();

        var c = t.GetPixel(0, 0);
        Destroy(t);

        return c.a > 0.5f;
    }
}
