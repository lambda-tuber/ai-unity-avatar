import asyncio
import json
import sys

class UnityMcpClient:
    def __init__(self, host="127.0.0.1", port=8080):
        self.host = host
        self.port = port
        self.reader = None
        self.writer = None
        self._msg_id = 0
        self._connected = False

    async def __aenter__(self):
        await self.connect()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.close()

    async def connect(self):
        """Unityサーバーに接続し、初期化ハンドシェイクを行う"""
        print(f"🔌 Connecting to {self.host}:{self.port}...")
        try:
            self.reader, self.writer = await asyncio.open_connection(self.host, self.port)
        except ConnectionRefusedError:
            print(f"❌ Error: Could not connect to {self.host}:{self.port}. Check if Unity is playing.")
            raise

        # 1. Initialize Request
        print("🤝 Initializing MCP session...")
        init_result = await self._send_request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "SimplePythonClient", "version": "1.0"}
        })
        
        # 2. Initialized Notification
        await self._send_notification("notifications/initialized")
        self._connected = True
        print("✅ Connected and Initialized!")
        return init_result

    async def list_tools(self):
        """ツール一覧を取得する"""
        result = await self._send_request("tools/list")
        return result.get("tools", [])

    async def call_tool(self, name, arguments=None):
        """ツールを実行する"""
        params = {"name": name, "arguments": arguments or {}}
        result = await self._send_request("tools/call", params)
        return result

    async def close(self):
        """接続を閉じる"""
        if self.writer:
            self.writer.close()
            await self.writer.wait_closed()
            print("🔌 Connection closed.")

    async def _send_request(self, method, params=None):
        """リクエストを送信し、対応するIDのレスポンスを待つ"""
        self._msg_id += 1
        current_id = self._msg_id
        
        msg = {
            "jsonrpc": "2.0",
            "method": method,
            "id": current_id
        }
        if params:
            msg["params"] = params

        await self._send_json(msg)

        # レスポンス待ちループ (通知やログを無視して、自分のIDの応答を探す)
        while True:
            response = await self._read_json()
            if not response:
                raise ConnectionError("Connection closed by server")
            
            # エラー判定
            if "error" in response and response.get("id") == current_id:
                raise Exception(f"RPC Error: {response['error']}")

            # 正常応答判定
            if "result" in response and response.get("id") == current_id:
                return response["result"]
            
            # それ以外のメッセージ（通知など）は一旦無視するかログに出す
            # print(f"[Log] Ignore message: {response}")

    async def _send_notification(self, method, params=None):
        """通知（レスポンス不要）を送信する"""
        msg = {"jsonrpc": "2.0", "method": method}
        if params:
            msg["params"] = params
        await self._send_json(msg)

    async def _send_json(self, data):
        json_str = json.dumps(data)
        self.writer.write(json_str.encode("utf-8") + b"\n")
        await self.writer.drain()

    async def _read_json(self):
        line = await self.reader.readline()
        if not line:
            return None
        return json.loads(line.decode("utf-8"))

# =================================================================
# 実行部分
# =================================================================
async def main():
    host = "127.0.0.1"
    port = 8080

    try:
        async with UnityMcpClient(host, port) as client:
            
            # 1. ツール一覧を表示
            print("\n--- 🛠 Listing Tools ---")
            tools = await client.list_tools()
            for t in tools:
                print(f" - {t['name']}: {t.get('description', '')}")

            # 2. echo ツールを実行
            print("\n--- 📨 Calling 'echo' ---")
            response = await client.call_tool("echo", {"message": "Hello from Simple Client!"})
            
            # 結果の解析
            content = response.get("content", [])
            for item in content:
                if item.get("type") == "text":
                    print(f"Server Response: {item.get('text')}")
                else:
                    print(f"Unknown content: {item}")

    except Exception as e:
        print(f"\n❌ Error occurred: {e}")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass