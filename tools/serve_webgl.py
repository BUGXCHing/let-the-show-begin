#!/usr/bin/env python3
"""Serve Unity's Brotli-compressed WebGL build on loopback for local testing."""

from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path: str) -> str:
        if path.endswith(".wasm.br"):
            return "application/wasm"
        if path.endswith(".js.br"):
            return "application/javascript"
        if path.endswith(".data.br"):
            return "application/octet-stream"
        if path.endswith(".br"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self) -> None:
        if urlsplit(self.path).path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
            self.send_header("Vary", "Accept-Encoding")
        super().end_headers()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path,
                        default=Path(__file__).resolve().parents[1] / "Build/WebGL")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not (directory / "index.html").is_file():
        parser.error(f"No WebGL index.html found in {directory}")

    handler = partial(UnityWebGLHandler, directory=str(directory))
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    print(f"Serving {directory} at http://127.0.0.1:{args.port}/ (loopback only)")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Stopping local WebGL preview.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
