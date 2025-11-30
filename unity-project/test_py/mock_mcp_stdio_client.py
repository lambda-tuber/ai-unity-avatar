import asyncio
import sys
import json
import os
from asyncio.subprocess import PIPE
import time

#
# global setting.
#
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(line_buffering=False, write_through=True)

# テスト対象のブリッジスクリプト名
BRIDGE_SCRIPT = "C:\\work\\lambda-tuber\\ai-unity-avatar\\unity-project\\test_py\\ai_unia_mcp_server.py"

async def main():
    # ブリッジスクリプトが存在するか確認
    if not os.path.exists(BRIDGE_SCRIPT):
        print(f"❌ Error: {BRIDGE_SCRIPT} が見つかりません。同じフォルダに置いてください。")
        return

    print(f"🚀 ブリッジ ({BRIDGE_SCRIPT}) を起動します...")
    print("   (Unityの起動待ち時間があるため、最初の応答まで時間がかかります)")

    # 1. ブリッジをサブプロセスとして起動
    # stdin=PIPE, stdout=PIPE で入出力を乗っ取ります
    # stderr=None にすることで、ブリッジのデバッグログはそのままコンソールに表示させます
    process = await asyncio.create_subprocess_exec(
        sys.executable, BRIDGE_SCRIPT,
        stdin=PIPE,
        stdout=PIPE,
        stderr=None 
    )

    try:
        # ---------------------------------------------------------
        # ヘルパー関数: リクエスト送信 & レスポンス受信
        # ---------------------------------------------------------
        async def send_request(method, params=None, req_id=None):
            msg = {
                "jsonrpc": "2.0",
                "method": method,
                "id": req_id
            }
            if params:
                msg["params"] = params
            
            json_str = json.dumps(msg)
            print(f"\n[Client -> Bridge] {json_str}")
            
            process.stdin.write(json_str.encode('utf-8') + b'\n')
            await process.stdin.drain()

        async def send_notification(method, params=None):
            msg = {"jsonrpc": "2.0", "method": method}
            if params:
                msg["params"] = params
            json_str = json.dumps(msg)
            print(f"[Client -> Bridge] (Notification) {json_str}")
            process.stdin.write(json_str.encode('utf-8') + b'\n')
            await process.stdin.drain()

        async def read_response():
            print("   ... 応答待ち ...")
            # タイムアウト付きで読み込む (Unity起動待ちがあるため最初は長めに)
            try:
                line_bytes = await asyncio.wait_for(process.stdout.readline(), timeout=40.0)
            except asyncio.TimeoutError:
                print("❌ Timeout: 応答がありません。Unityが起動していないか、ブリッジが詰まっています。")
                return None

            if not line_bytes:
                return None
            
            line = line_bytes.decode('utf-8').strip()
            print(f"[Bridge -> Client] {line}")
            return json.loads(line)

        # ---------------------------------------------------------
        # MCP 通信フローのテスト
        # ---------------------------------------------------------

        # 1. Initialize (初期化)
        # Unityが起動してTCP接続が確立されるまで、ここの応答は返ってこない
        await send_request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "Tester", "version": "1.0"}
        }, req_id=1)

        init_res = await read_response()
        if not init_res or "error" in init_res:
            print("❌ 初期化に失敗しました。")
            return

        # 2. Initialized (通知)
        # プロトコル上、initializeの成功後に送る必要がある
        await send_notification("notifications/initialized")

        # 3. List Tools (ツール一覧取得)
        await send_request("tools/list", req_id=2)
        tools_res = await read_response()
        
        if tools_res and "result" in tools_res:
            tools = tools_res["result"].get("tools", [])
            print(f"✅ ツール一覧取得成功: {len(tools)} 個のツールが見つかりました")
            for t in tools:
                print(f"   - {t['name']}")

        # 4. Echo Test (エコー実行)
        # ツール名 'echo' がUnityにある前提
        await send_request("tools/call", {
            "name": "echo",
            "arguments": {"message": "こんにちは、Hello from Tester!"}
        }, req_id=3)
        
        echo_res = await read_response()
        if echo_res and "result" in echo_res:
            content = echo_res["result"].get("content", [])
            text = content[0].get("text", "") if content else ""
            print(f"✅ Echo成功: {text}")

        await send_request("tools/call", {
            "name": "ai-unia-speak",
            "arguments": {"text": "こんにちは"}
        }, req_id=3)
        
        echo_res = await read_response()
        if echo_res and "result" in echo_res:
            content = echo_res["result"].get("content", [])
            text = content[0].get("text", "") if content else ""
            print(f"✅ Echo成功: {text}")

        time.sleep(5)

    except Exception as e:
        print(f"❌ エラー: {e}")

    finally:
        print("\n🛑 テスト終了。ブリッジを閉じます。")
        # 入力を閉じるとブリッジも終了するはず
        if process.stdin:
            process.stdin.close()
        
        # プロセス終了待ち
        try:
            await asyncio.wait_for(process.wait(), timeout=5.0)
        except asyncio.TimeoutError:
            process.kill()
        print("👋 Done.")

if __name__ == "__main__":
    # Windowsでのデフォルトポリシー(Proactor)を使うため、
    # Selectorポリシーの設定を削除しました
    asyncio.run(main())
