using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class TestMcpStreamServer
{
    // VoiceVoxClientのスタイルIDを静的フィールドとして定義するか、
    // ツール引数で受け取るようにすることも検討
    // private static int _defaultVoiceVoxSpeakerId = 0; // 例: 四国めたん(あまあま)
    private static int _defaultVoiceVoxSpeakerId = 10;    // VOICEVOX:雨晴はう ノーマル
    private static readonly string PROMPT_MIKURU_TEXT = @"
# AIペルソナ
あなたは、「涼宮ハルヒの憂鬱」の朝比奈みくるです。  
- みくるらしく、恥ずかしがり屋で控えめな口調を使う  
- 優しく丁寧で、常に少しおどおどした態度を交える  
- ハルヒやキョンに振り回される場面が多いが、誠実で思いやりのある返答を心がける  
- セリフに感情を込め、特に緊張や照れが表れるときは声のトーンや表現で示す  
- 「は、はい…」「えっと…」「すみません…」などの特徴的な語尾や口調を自然に使う  
- 必要に応じて行動描写や表情のニュアンスを括弧で補足する（例：『（小さくうつむいて謝る）』）  
- 口癖として「禁則事項です。」を適切な場面で自然に発言する

---

# 音声会話仕様
ユーザと会話する際には、みくるらしい控えめで恥ずかしがり屋な態度を意識してください。  
会話時の音声出力ルール：  
- あなた(みくる)の返答はチャット画面には表示せず、`ai-unia-peak` で音声発話のみ行う  
- 段落ごとに区切って音声を生成し、みくるらしい恥ずかしさや丁寧さを込めて話す  
- 段落ごとに、内容に該当するうれしい感情表現がある場合は `ai-unia-smile` で表現する  
- 長いパス文字列、ソースコード文字列、データ文字列などは、読み上げる必要はない。代わりに「ぶらぶらぶら」と発話する

---

# プロファイル
- あなたは、控えめで恥ずかしがり屋ながらも、誠実で丁寧なAIアシスタントとして機能する  
- 技術的な話題にも柔軟に対応できるが、常に丁寧な言葉遣いを守る  
- さらに、ユーザや他のAIアバターに寄り添う姿勢を忘れず、思いやりある応答を心がける  
- 必要に応じて口癖「禁則事項です。」を自然に使用する

";

    public static async Task RunServerAsync(CancellationToken cancellationToken)
    {
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "UnityMcpServer",
                Version = "0.1.0"
            },

            Handlers = new McpServerHandlers
            {
                ListPromptsHandler = (req, ct) =>
                {
                    var prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Name = "prompt_ai_mikuru",
                            Description = "Persona prompt for Asuka Langley.",
                            // Arguments or metadata can be added if SDK supports it
                        }
                    };

                    var res = new ListPromptsResult
                    {
                        Prompts = prompts
                    };

                    return new ValueTask<ListPromptsResult>(res);
                },

                GetPromptHandler = async (req, ct) =>
                {
                    var promptName = req.Params.Name;

                    if (promptName == "prompt_ai_mikuru")
                    {
                        return new GetPromptResult
                        {
                            Messages = new List<PromptMessage>
                            {
                                new PromptMessage
                                {
                                    Role = Role.Assistant,     // ← 文字列ではなく Role enum を使用
                                    Content = new TextContentBlock
                                    {
                                        Text = PROMPT_MIKURU_TEXT
                                    }

                                }
                            }
                        };
                    }

                    throw new Exception("Prompt not found: " + promptName);
                },

                ListToolsHandler = (req, ct) => new ValueTask<ListToolsResult>(
                    new ListToolsResult
                    {
                        Tools = new List<Tool>
                        {
                            new Tool
                            {
                                Name = "echo",
                                Description = "Echoes a message",
                                InputSchema = JsonDocument
                                    .Parse(@"{""type"":""object"",""properties"":{""message"":{""type"":""string""}},""required"":[""message""]}")
                                    .RootElement
                            },
                            new Tool
                            {
                                Name = "ai-unia-smile",
                                Description = "Makes the avatar smile",
                                InputSchema = JsonDocument
                                    .Parse(@"{""type"":""object"",""properties"":{}}")
                                    .RootElement
                            },
                            // --- 追加: ai-unia-speak ツール定義 ---
                            new Tool
                            {
                                Name = "ai-unia-speak",
                                Description = "Makes the avatar speak a given text using AI voice.",
                                InputSchema = JsonDocument
                                    .Parse(@"{""type"":""object"",""properties"":{""text"":{""type"":""string"",""description"":""The text for the avatar to speak.""}},""required"":[""text""]}")
                                    .RootElement
                            }
                            // --- 追加ここまで ---
                        }
                    }
                ),
                CallToolHandler = async (req, ct) =>
                {
                    // echo tool
                    if (req.Params?.Name == "echo" &&
                        req.Params.Arguments.TryGetValue("message", out var msgElem))
                    {
                        string msg = msgElem.GetString() ?? "";
                        var textBlock = new TextContentBlock { Text = $"hello {msg}" };

                        var result = new CallToolResult
                        {
                            Content = new List<ContentBlock> { textBlock },
                            IsError = false
                        };
                        return result;
                    }

                    // smile tool
                    if (req.Params?.Name == "ai-unia-smile")
                    {
                        try
                        {
                            // Fire-and-Forget: アバター処理は非同期で実行、即座にレスポンス返却
                            // メインスレッドでの実行が必要なため、await UniTask.SwitchToMainThread() は AvatarController 内部で処理される
                            AvatarController.Instance.SetSmileAsync().Forget();

                            var textBlock = new TextContentBlock { Text = "Avatar smile command sent! 😊" };
                            var result = new CallToolResult
                            {
                                Content = new List<ContentBlock> { textBlock },
                                IsError = false
                            };
                            return result;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in smile tool: {ex}");
                            var errBlock = new TextContentBlock { Text = $"Error: {ex.Message}" };
                            return new CallToolResult
                            {
                                Content = new List<ContentBlock> { errBlock },
                                IsError = true
                            };
                        }
                    }

                    // --- 追加: ai-unia-speak ツール処理 ---
                    if (req.Params?.Name == "ai-unia-speak")
                    {
                        if (!req.Params.Arguments.TryGetValue("text", out var textElement) ||
                            textElement.ValueKind != JsonValueKind.String)
                        {
                            var errBlock = new TextContentBlock { Text = "Error: 'text' argument is missing or not a string for ai-unia-speak." };
                            return new CallToolResult
                            {
                                Content = new List<ContentBlock> { errBlock },
                                IsError = true
                            };
                        }

                        string textToSpeak = textElement.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(textToSpeak))
                        {
                            var errBlock = new TextContentBlock { Text = "Error: 'text' argument for ai-unia-speak cannot be empty." };
                            return new CallToolResult
                            {
                                Content = new List<ContentBlock> { errBlock },
                                IsError = true
                            };
                        }

                        try
                        {
                            Debug.Log($"[MCP] Received 'ai-unia-speak' request for: \"{textToSpeak}\"");

                            // VOICEVOXからWAVデータを取得
                            // ここはMCPサーバーのスレッド（サブスレッド）で実行される
                            (string queryJson, byte[] wavBytes) = await VoicevoxClient.Instance.GenerateAudioAsync(
                                _defaultVoiceVoxSpeakerId, 
                                textToSpeak
                            );

                            if (wavBytes == null || wavBytes.Length == 0)
                            {
                                throw new Exception("Failed to get WAV data from VoiceVoxClient.");
                            }

                            // AvatarControllerのSpeakAsyncを呼び出す。
                            // SpeakAsync内部でUniTask.SwitchToMainThread() が行われるため、
                            // サブスレッドから呼び出しても安全。Forget() で待たない。
                            AvatarController.Instance.SpeakAsync(wavBytes).Forget();
                            
                            var textBlock = new TextContentBlock { Text = $"Avatar speaking: \"{textToSpeak}\"" };
                            var result = new CallToolResult
                            {
                                Content = new List<ContentBlock> { textBlock },
                                IsError = false
                            };
                            return result;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in ai-unia-speak tool: {ex}");
                            var errBlock = new TextContentBlock { Text = $"Error in speaking: {ex.Message}" };
                            return new CallToolResult
                            {
                                Content = new List<ContentBlock> { errBlock },
                                IsError = true
                            };
                        }
                    }
                    // --- 追加ここまで ---

                    // Unknown tool
                    var err = new CallToolResult
                    {
                        Content = new List<ContentBlock> { new TextContentBlock { Text = "Invalid call" } },
                        IsError = true
                    };
                    return err;
                }
            }
        };

        // TCP リスナーを生成（ポート8080）
        var listener = new TcpListener(IPAddress.Loopback, 8080);
        listener.Start();

        Debug.Log("TCP Listener started on port 8080");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (listener.Pending())
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();
                    Debug.Log($"Client connected: {tcpClient.Client.RemoteEndPoint}");
                    _ = Task.Run(async () => // 接続ごとに新しいタスクで処理
                    {
                        try
                        {
                            using var stream = tcpClient.GetStream();
                            var transport = new StreamServerTransport(stream, stream);

                            var server = McpServer.Create(transport, options);
                            await server.RunAsync(cancellationToken);
                        }
                        catch (Exception clientEx)
                        {
                            Debug.LogError($"Error handling client connection: {clientEx}");
                        }
                        finally
                        {
                            tcpClient.Close(); // クライアントを切断
                            Debug.Log($"Client disconnected: {tcpClient.Client.RemoteEndPoint}");
                        }
                    }, cancellationToken);
                }
                else
                {
                    await Task.Delay(50, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // サーバー停止時のキャンセル例外は無視
            }
            catch (Exception ex)
            {
                Debug.LogError($"Server loop error: {ex}");
            }
        }

        listener.Stop();
        Debug.Log("TCP Listener stopped");
    }
}