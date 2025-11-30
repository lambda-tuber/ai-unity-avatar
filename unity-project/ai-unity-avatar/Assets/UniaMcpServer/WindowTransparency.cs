using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class WindowConfig : MonoBehaviour
{
    // ---------------- WinAPI ----------------
    // [DllImport("user32.dll")] // GetActiveWindowは削除済み
    // private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
    // ★ 追加: ウィンドウタイトルを動的に設定するための WinAPI ★
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags
    );

    // ★ デスクトップの解像度を取得するための WinAPI ★
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0; // スクリーンの幅
    private const int SM_CYSCREEN = 1; // スクリーンの高さ

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;    // タスクバー非表示（必要なら）

    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;

    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // DWM 透過設定用
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private IntPtr windowHandle;

    void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        // -------------------------------------------------------------------
        // ★ 1. 【実行順序の最初】ユニークタイトルを生成し、OSに設定 ★
        // -------------------------------------------------------------------
        string originalTitle = Application.productName; 
        string uniqueTitle = originalTitle;

        string[] args = Environment.GetCommandLineArgs();
        
        // 引数をパースして --title フラグを探す
        for (int i = 0; i < args.Length; i++)
        {
            Debug.LogError($"実行時引数: {args[i]}");
            if (args[i].ToLower() == "--title")
            {
                if (i + 1 < args.Length)
                {
                    // --title の次の引数をそのまま uniqueTitle として使用
                    uniqueTitle = args[i + 1];
                }
                break; 
            }
        }

        // -------------------------------------------------------------------
        // ★ 2. 元のタイトルとUnityクラス名でハンドルを取得 ★
        // -------------------------------------------------------------------
        
        // FindWindowを試みるのは一度だけ、クラス名とタイトル名を使う。
        // GetActiveWindowは不安定なので使用しない。
        string unityWindowClass = "UnityWndClass"; 
        windowHandle = FindWindow(unityWindowClass, originalTitle);

        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogError($"ウィンドウのハンドル取得に失敗しました: {originalTitle}");
            return;
        }

        // -------------------------------------------------------------------
        // ★ 3. 【OS上のタイトルを変更】ユニークな名前に変更する ★
        // -------------------------------------------------------------------
        if (uniqueTitle != originalTitle)
        {
            // 取得したハンドルに対して、OS上のタイトルをユニークなものに変更
            SetWindowText(windowHandle, uniqueTitle); 
            Debug.Log($"Window title set to: {uniqueTitle}");
        }

        // -------------------------------------------------------------------
        // ★ 4. カメラとフレームレートの設定（ちらつき対策） ★
        // -------------------------------------------------------------------
        
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main Camera not found!");
            return;
        }

        // ---------- 4. Unity 側の背景透明 ----------
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

        // ---------- 5. 枠削除（ボーダーレス化） ----------
        int style = GetWindowLong(windowHandle, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME);
        SetWindowLong(windowHandle, GWL_STYLE, style);

        SetWindowPos(windowHandle, HWND_TOP, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

        // ---------- 6. WS_EX_LAYERED を付与 ----------
        int exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
        exStyle |= (int)(WS_EX_LAYERED);
        SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);

        // ---------- 7. DWM に「透明を使います」と宣言 ----------
        var margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(windowHandle, ref margins);

        // ----------------------------------------------------
        // ★ 8. サイズを固定し、デスクトップ中央に配置 ★
        // ----------------------------------------------------
        int targetWidth = 1080/3; 
        int targetHeight = 1920/3; 
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        int x = (screenWidth - targetWidth) / 2;
        int y = (screenHeight - targetHeight) / 2;
        
        // サイズ、位置、最前面化を一度に実行 (Updateでの繰り返し呼び出しは不要)
        SetWindowPos(windowHandle, HWND_TOPMOST, x, y, targetWidth, targetHeight,
             SWP_SHOWWINDOW); 

        Debug.Log("Transparent + ClickThrough + Borderless + TopMost window enabled.");

#endif
    }

    // -------------------------------------------------------------------
    // ★ Update() は安定性のため削除しました。最前面化はStart()で実行済みです。 ★
    // -------------------------------------------------------------------
    // void Update() { ... }
}