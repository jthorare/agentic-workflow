#!/usr/bin/env python3
"""Fake GitHub App token endpoint for scripts/tests/agent-token.test.sh.

Binds 127.0.0.1 on a free port (written to <dir>/port), verifies the RS256 JWT
with the fixture public key through `openssl dgst -verify` (not the signer's
code path), snapshots every process argv while the request is in flight, and
answers by MODE: ok -> 201 with a canned token; err -> 401 with a body that
carries a token-shaped string the script must not echo; errmsg -> 401 whose
message itself holds token shapes and terminal control characters; slow ->
writes <dir>/inflight, then holds the request for $FAKE_HOLD seconds (default
10) before answering ok; it writes <dir>/replying just before the reply goes out
and <dir>/answered once the reply was attempted (whether or not the client was
still there).
Usage: fake-github.py <dir> <public.pem> <mode>
"""
import base64, json, os, socketserver, subprocess, sys, tempfile, time
from http.server import BaseHTTPRequestHandler, HTTPServer

DIR, PUB, MODE = sys.argv[1], sys.argv[2], sys.argv[3]
CANNED = "ghs_fixtureToken0123456789abcdefABCDEF"
LEAK = "ghs_leakyBodyToken9876543210zyxwvuZYXW"
# Token shapes with '-' and '.' and a fine-grained PAT, plus ESC and BEL.
MSG = ("Bad credentials ghs_abc-def.ghi github_pat_11AA22_bb-cc.dd "
       "\u001b]0;pwned\u0007\u001b[2Jdone")


def b64d(s):
    return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))


def verify(jwt):
    head, claims, sig = jwt.split(".")
    with tempfile.TemporaryDirectory() as t:
        data, sigf = os.path.join(t, "d"), os.path.join(t, "s")
        open(data, "wb").write(f"{head}.{claims}".encode())
        open(sigf, "wb").write(b64d(sig))
        r = subprocess.run(["openssl", "dgst", "-sha256", "-verify", PUB, "-signature", sigf, data],
                           capture_output=True, text=True)
    return json.loads(b64d(head)), json.loads(b64d(claims)), r.stdout.strip()


class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_POST(self):
        auth = self.headers.get("Authorization", "")
        body = self.rfile.read(int(self.headers.get("Content-Length", 0) or 0)).decode()
        jwt = auth[7:] if auth.startswith("Bearer ") else auth
        argv = subprocess.run(["ps", "-axww", "-o", "args="], capture_output=True, text=True).stdout
        # Look for this run's exact header and JWT only: other processes on the
        # machine may legitimately carry a "Bearer " of their own.
        rec = {"path": self.path, "accept": self.headers.get("Accept"), "body": body,
               "argv_has_bearer": bool(jwt) and ("Bearer " + jwt) in argv,
               "argv_has_jwt": bool(jwt) and jwt in argv,
               # the snapshot must at least contain this server's own command
               # line, or the two checks above prove nothing
               "argv_seen": "fake-github.py" in argv}
        try:
            rec["header"], rec["claims"], rec["verify"] = verify(jwt)
        except Exception as e:  # malformed JWT is a test failure, recorded
            rec["verify"] = f"error: {e}"
        with open(os.path.join(DIR, "requests.jsonl"), "a") as f:
            f.write(json.dumps(rec) + "\n")
        if MODE == "slow":
            open(os.path.join(DIR, "inflight"), "w").close()
            time.sleep(float(os.environ.get("FAKE_HOLD", "10")))
        if MODE in ("ok", "slow"):
            code, out = 201, {"token": CANNED, "expires_at": "2099-01-01T00:00:00Z"}
        elif MODE == "errmsg":
            code, out = 401, {"message": MSG}
        else:
            code, out = 401, {"message": "Bad credentials", "token": LEAK,
                              "documentation_url": "https://docs.github.com/rest"}
        payload = json.dumps(out).encode()
        if MODE == "slow":
            # before the first byte, so a client that exits after this file appears could have read the reply
            open(os.path.join(DIR, "replying"), "w").close()
        try:
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)
            self.wfile.flush()
        except OSError:  # the client was killed while we held the request
            pass
        finally:
            if MODE == "slow":
                open(os.path.join(DIR, "answered"), "w").close()


class LoopbackServer(HTTPServer):
    # HTTPServer.server_bind calls socket.getfqdn("127.0.0.1"), a reverse DNS lookup
    # that can stall for seconds on a CI runner; every start of this fake paid it.
    # The handler never uses server_name, so skip the lookup.
    def server_bind(self):
        socketserver.TCPServer.server_bind(self)
        self.server_name, self.server_port = "127.0.0.1", self.server_address[1]


srv = LoopbackServer(("127.0.0.1", 0), H)
with open(os.path.join(DIR, "port.tmp"), "w") as f:
    f.write(str(srv.server_address[1]))
os.rename(os.path.join(DIR, "port.tmp"), os.path.join(DIR, "port"))
srv.serve_forever()
