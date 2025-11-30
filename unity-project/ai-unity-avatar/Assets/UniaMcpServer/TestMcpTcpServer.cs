using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using UnityEngine;

public class TestMcpTcpServer
{
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
                            }
                        }
                    }
                ),
                CallToolHandler = (req, ct) =>
                {
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
                        return new ValueTask<CallToolResult>(result);
                    }

                    var err = new CallToolResult
                    {
                        Content = new List<ContentBlock> { new TextContentBlock { Text = "Invalid call" } },
                        IsError = true
                    };
                    return new ValueTask<CallToolResult>(err);
                }
            }
        };

        // Streamable HTTP Transport を生成
        var transport = new StreamableHttpServerTransport();

        // MCP サーバを生成
        var server = McpServer.Create(transport, options);

        Debug.Log("MCP Streamable HTTP Server started");

        // RunAsync でサーバを非同期実行
        await server.RunAsync(cancellationToken);

        Debug.Log("MCP Streamable HTTP Server stopped");
    }
}