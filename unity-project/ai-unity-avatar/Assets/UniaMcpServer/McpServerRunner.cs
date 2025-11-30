using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;

public class McpServerRunner : MonoBehaviour
{
    // Win32 APIから AllocConsole 関数をインポート
    [DllImport("kernel32.dll")]
    public static extern bool AllocConsole();

    // Stdioのストリームが有効化されたか確認
    private bool stdioAllocated = false;
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    // コンソールウィンドウのハンドルを取得する関数
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    // ウィンドウの状態を変更する関数 (最小化、最大化など)
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // 最小化の定数
    private const int SW_SHOWMINIMIZED = 2;

    private CancellationTokenSource _cts;

    void Awake()
    {
        _cts = new CancellationTokenSource();

        // Windows環境でのみ実行
        // if (Application.platform == RuntimePlatform.WindowsPlayer || 
        //     Application.platform == RuntimePlatform.WindowsEditor)
        // {
        //     if (AllocConsole())
        //     {
        //         // ストリームが有効化されたことを確認
        //         stdioAllocated = true;
        //         ConfigureStdioStreams();
        //         Debug.Log("Console allocated and Stdio enabled.");
        //     }
        // }

    }

    // ... Awake() メソッド内で AllocConsole() 成功後にこれを呼び出す ...
    private void ConfigureStdioStreams()
    {
        // 1. Stdioのハンドルを取得
        IntPtr hConsoleOutput = GetStdHandle(STD_OUTPUT_HANDLE);
        IntPtr hConsoleInput = GetStdHandle(STD_INPUT_HANDLE);

        // 2. 新しいFileStreamを生成し、Consoleクラスに接続
        //    これにより、Console.In/Out が新しいコンソールを参照する
        
        // Output/Error の設定
        var fsOut = new FileStream(new SafeFileHandle(hConsoleOutput, false), FileAccess.Write);
        var swOut = new StreamWriter(fsOut, System.Text.Encoding.UTF8) { AutoFlush = true };
        Console.SetOut(swOut);
        
        // Input の設定
        var fsIn = new FileStream(new SafeFileHandle(hConsoleInput, false), FileAccess.Read);
        var srIn = new StreamReader(fsIn, System.Text.Encoding.UTF8);
        Console.SetIn(srIn);
        
        // これで MCP Server started は新しいコンソールに出力されます。
        // Console.WriteLine("✅ Stdio Streams successfully configured for MCP.");

        // 2. 作成されたコンソールウィンドウを最小化する
        IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero)
        {
            // ウィンドウハンドルを取得し、最小化コマンドを実行
            ShowWindow(consoleHandle, SW_SHOWMINIMIZED);
            Debug.Log("Stdio Console minimized successfully.");
        }

    }

    void Start()
    {
        // サブスレッドで MCP サーバを非同期起動
        Task.Run(async () =>
        {
            try
            {
                Debug.Log("Starting MCP Server in background thread...");
                await TestMcpStreamServer.RunServerAsync(_cts.Token);
                //await TestMcpStreamServer.RunServerAsync(_cts.Token);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"MCP Server error: {ex}");
            }
        });
    }

    void OnDestroy()
    {
        // GameObject が破棄されるときにキャンセル
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}


