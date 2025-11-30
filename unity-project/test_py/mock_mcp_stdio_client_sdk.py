import asyncio
import sys
import os
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from mcp.types import ContentBlock
import time

#
# global setting.
#
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(line_buffering=False, write_through=True)

# Target bridge script
BRIDGE_SCRIPT = "C:\\work\\lambda-tuber\\ai-unity-avatar\\unity-project\\test_py\\ai_unia_mcp_server.py"

async def main():
    # 1. Check bridge script exists
    if not os.path.exists(BRIDGE_SCRIPT):
        print(f"Error: {BRIDGE_SCRIPT} not found.")
        return

    print(f"Starting connection to Unity via bridge script: {BRIDGE_SCRIPT}")
    print("Unity startup and TCP connection may take several seconds.")

    # 2. Configure server parameters
    server_params = StdioServerParameters(
        command=sys.executable,
        args=[BRIDGE_SCRIPT],
        env={"PYTHONUTF8": "1"}
    )

    try:
        # 3. Use stdio_client context manager
        async with stdio_client(server_params) as (read_stream, write_stream):
            
            # 4. Create ClientSession
            async with ClientSession(read_stream, write_stream) as session:
                print("Initializing session...")
                
                await session.initialize()
                print("Initialization completed.")

                # ---------------------------------------------------------
                # Get Prompt list
                # ---------------------------------------------------------
                print("\n--- Prompt List ---")
                prompts_result = await session.list_prompts()
                
                for prompt in prompts_result.prompts:
                    print(f" - {prompt.name}: {prompt.description}")


                # ---------------------------------------------------------
                # Get Prompt
                # ---------------------------------------------------------
                print("\n--- Prompt Get ---")
                #help(session)
                #dir(session)
                try:
                    result = await session.get_prompt("prompt_ai_aska")
                    for msg in result.messages:  # ← 最新 SDK では .messages が正しい
                        # msg.content は ContentBlock 単体
                        content = msg.content
                        if hasattr(content, "text"):
                            print(f" > {content.text}")
                        else:
                            print(f" > {content}")

                    print("Execution succeeded:")

                except Exception as e:
                    print(f"Prompt get execution error: {e}")

                # ---------------------------------------------------------
                # Get tool list
                # ---------------------------------------------------------
                print("\n--- Tool List ---")
                tools_result = await session.list_tools()
                
                for tool in tools_result.tools:
                    print(f" - {tool.name}: {tool.description}")

                # ---------------------------------------------------------
                # Execute tool
                # ---------------------------------------------------------
                print("\n--- Executing 'echo' Tool ---")
                
                try:
                    result = await session.call_tool(
                        "echo", 
                        arguments={"message": "Hello via MCP SDK!"}
                    )

                    for content in result.content:
                        if isinstance(content, ContentBlock) and hasattr(content, "text"):
                            print(f" > {content.text}")
                        else:
                            print(f" > {content}")

                    print("Execution succeeded:")


                    # print("Execution smile start:")
                    # result = await session.call_tool(
                    #     "ai-unia-smile", 
                    #     arguments={}
                    # )

                    for content in result.content:
                        if isinstance(content, ContentBlock) and hasattr(content, "text"):
                            print(f" > {content.text}")
                        else:
                            print(f" > {content}")

                    print("Execution smile succeeded:")

                    print("Execution speak start:")
                    result = await session.call_tool(
                        "ai-unia-speak", 
                        arguments={"text" : "あ、あの…こんにちは、みくるです…♪ えっと…未来のこと、ちょっとだけ知ってるかもしれない…けど、それは…禁則事項です…"}
                    )

                    for content in result.content:
                        if isinstance(content, ContentBlock) and hasattr(content, "text"):
                            print(f" > {content.text}")
                        else:
                            print(f" > {content}")

                    print("Execution speak succeeded:")

                    time.sleep(15)

                except Exception as e:
                    print(f"Tool execution error: {e}")

    except Exception as e:
        print(f"\nAn error occurred: {e}")
        print("Unity may not be running or the bridge script may have an issue.")

if __name__ == "__main__":
    asyncio.run(main())
