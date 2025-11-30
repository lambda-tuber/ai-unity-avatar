using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using UnityEngine;

public class TestMcpServer
{
    public static async Task RunServerAsync(CancellationToken cancellationToken = default)
    {
        Debug.Log("Starting MCP RunServerAsync ...");

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "UnityMcpServer",
                Version = "0.1.0"
            },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = (req, ct) => new ValueTask<ListToolsResult>(new ListToolsResult
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
                }),

                CallToolHandler = (req, ct) =>
                {
                    if (req.Params?.Name == "echo" &&
                        req.Params.Arguments.TryGetValue("message", out var msgElem))
                    {
                        string msg = msgElem.GetString() ?? "";

                        // TextContentBlock を作成
                        var textBlock = new TextContentBlock
                        {
                            Text = $"hello {msg}"
                        };

                        var result = new CallToolResult
                        {
                            Content = new List<ContentBlock> { textBlock },
                            IsError = false
                        };

                        return new ValueTask<CallToolResult>(result);
                    }

                    // エラー時も CallToolResult で返すのが推奨
                    var errorResult = new CallToolResult
                    {
                        Content = new List<ContentBlock>
                        {
                            new TextContentBlock { Text = "Invalid call" }
                        },
                        IsError = true
                    };
                    return new ValueTask<CallToolResult>(errorResult);
                }
            }
        };

        var transport = new StdioServerTransport(options);
        var server = McpServer.Create(transport, options);

        Debug.Log("MCP Server started");
        Console.WriteLine("MCP Server started");

        await server.RunAsync(cancellationToken);

        Console.WriteLine("MCP Server stopped");
        Debug.Log("MCP Server stopped");
    }
}
