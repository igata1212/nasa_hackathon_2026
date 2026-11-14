"""Send a Python file to the running Blender (MCP for Blender addon, port 9876)."""
import socket, json, sys
code = open(sys.argv[1], encoding="utf-8").read()
s = socket.create_connection(("127.0.0.1", 9876), timeout=600)
s.sendall(json.dumps({"type": "execute_code", "params": {"code": code}}).encode())
buf = b""
while True:
    chunk = s.recv(65536)
    if not chunk: break
    buf += chunk
    try: res = json.loads(buf); break
    except json.JSONDecodeError: continue
if res.get("status") == "success":
    out = res["result"].get("result", "") if isinstance(res["result"], dict) else str(res["result"])
    print("OK\n" + out[-3000:])
else:
    print("ERROR\n" + str(res.get("message", res))[-3000:])
    sys.exit(1)
