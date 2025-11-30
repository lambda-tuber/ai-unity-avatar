import asyncio
import sys
import os
import subprocess
from contextlib import asynccontextmanager

#
# global setting.
#
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(line_buffering=False, write_through=True)

# --- Settings ---
WORK_DIR = "C:\\work\\lambda-tuber\\ai-unity-avatar\\unity-project\\build"
if len(sys.argv) >= 2:
    WORK_DIR = sys.argv[1]

UNITY_EXE_PATH = os.path.join(WORK_DIR, "ai-unity-avatar.exe")
LOG_FILE = os.path.join(WORK_DIR, "ai-unity-avatar.log")

# 3. TCP configuration
TCP_HOST = "127.0.0.1"
TCP_PORT = 8080
STARTUP_WAIT_SEC = 10  # Wait time for Unity startup
# ---

def log(msg):
    """
    Output debug log to stderr.
    Important: stdout must not be used for debug messages,
    because stdout is used for the MCP protocol.
    """
    sys.stderr.write(f"[Bridge] {msg}\n")
    sys.stderr.flush()

# ------------------------------------------------------------------------------
# 1. Process management: Launch Unity
# ------------------------------------------------------------------------------
@asynccontextmanager
async def unity_process_context(exe_path: str, args: list):
    process: subprocess.Popen | None = None
    try:
        log(f"Launching Unity: {os.path.basename(exe_path)}")

        # Launch Unity as an independent process
        cmd = [exe_path] + args
        process = subprocess.Popen(cmd)

        log(f"Waiting {STARTUP_WAIT_SEC} seconds for TCP server to be ready...")
        await asyncio.sleep(STARTUP_WAIT_SEC)

        yield process

    finally:
        if process:
            log(f"Terminating Unity process (PID: {process.pid})")
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
            log("Process terminated")

# ------------------------------------------------------------------------------
# 2. Bridge: Stdin <-> TCP <-> Stdout
# ------------------------------------------------------------------------------
async def pipe_stdin_to_tcp(writer: asyncio.StreamWriter):
    """
    Read from stdin (MCP client request) and forward to TCP (Unity)
    """
    loop = asyncio.get_running_loop()
    try:
        while True:
            # Use executor for cross-platform blocking readline
            line = await loop.run_in_executor(None, sys.stdin.readline)

            if not line:  # EOF detected
                break

            writer.write(line.encode('utf-8'))
            await writer.drain()

    except Exception as e:
        log(f"Stdin->TCP Error: {e}")
    finally:
        log("Stdin closed -> closing TCP writer")
        if not writer.is_closing():
            writer.close()

async def pipe_tcp_to_stdout(reader: asyncio.StreamReader):
    """
    Read from TCP (Unity response) and write to stdout (MCP client)
    """
    try:
        while True:
            line = await reader.readline()
            if not line:  # TCP closed
                break

            sys.stdout.write(line.decode('utf-8'))
            sys.stdout.flush()

    except Exception as e:
        log(f"TCP->Stdout Error: {e}")
    finally:
        log("TCP connection closed")

# ------------------------------------------------------------------------------
# Main
# ------------------------------------------------------------------------------
async def main():
    # Unity launch arguments
    args = ["-logFile", LOG_FILE]

    try:
        # 1. Start Unity
        async with unity_process_context(UNITY_EXE_PATH, args):

            # 2. Connect to Unity TCP server
            log(f"Attempting TCP connection: {TCP_HOST}:{TCP_PORT}")
            try:
                reader, writer = await asyncio.open_connection(TCP_HOST, TCP_PORT)
                log("TCP connection established. Starting bridge.")

                # 3. Start bidirectional piping
                task_in = asyncio.create_task(pipe_stdin_to_tcp(writer))
                task_out = asyncio.create_task(pipe_tcp_to_stdout(reader))

                done, pending = await asyncio.wait(
                    [task_in, task_out],
                    return_when=asyncio.FIRST_COMPLETED
                )

                for t in pending:
                    t.cancel()

            except ConnectionRefusedError:
                log("Connection refused: Unity may not be running or port is closed.")
            except Exception as e:
                log(f"Bridge error: {e}")

    except Exception as e:
        log(f"Fatal error: {e}")

if __name__ == "__main__":
    if sys.platform == 'win32':
         asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
