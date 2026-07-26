"""本地起一个能正确伺服 Unity WebGL 包的静态服务器。

Unity 的 WebGL 输出是 .gz 压缩的（WebGL.data.gz / WebGL.wasm.gz / WebGL.framework.js.gz）。
普通静态服务器不会带 Content-Encoding: gzip 头，浏览器不会解压，Unity 加载器就会报
“Unable to parse WebGL.data.gz!”。直接双击 index.html 同样打不开（file:// 下有跨域限制）。
本脚本补上正确的响应头，方便本地验证。

用法：
    python tools/serve_webgl.py            # 默认 http://localhost:8080
    python tools/serve_webgl.py --port 9000
"""

from __future__ import annotations

import argparse
import functools
import http.server
import socketserver
import webbrowser
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
DEFAULT_ROOT = PROJECT / "Build" / "WebGL"

# 去掉 .gz 后的真实类型，交给浏览器按 Content-Encoding 解压后解析。
INNER_TYPE = {
    ".js": "application/javascript",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".symbols": "application/octet-stream",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self) -> None:
        # WebGL 需要这两个头才能启用 SharedArrayBuffer；顺带禁用缓存避免测到旧包。
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def guess_type(self, path):  # noqa: A003 - 覆盖标准库同名方法
        p = Path(path)
        if p.suffix == ".gz":
            return INNER_TYPE.get(p.with_suffix("").suffix, "application/octet-stream")
        return super().guess_type(path)

    def send_head(self):
        if self.translate_path(self.path).endswith(".gz"):
            self.send_header_gzip = True
        return super().send_head()

    def send_header(self, keyword: str, value: str) -> None:
        super().send_header(keyword, value)
        # Content-Type 之后紧跟着补 Content-Encoding，确保浏览器知道要先解压。
        if keyword == "Content-type" and self.translate_path(self.path).endswith(".gz"):
            super().send_header("Content-Encoding", "gzip")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--no-open", action="store_true")
    args = parser.parse_args()

    root: Path = args.root
    if not (root / "index.html").is_file():
        print(f"{root} 下没有 index.html，请先打 WebGL 包。")
        return 1

    handler = functools.partial(Handler, directory=str(root))
    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.TCPServer(("127.0.0.1", args.port), handler) as httpd:
        url = f"http://localhost:{args.port}/"
        print(f"伺服 {root}")
        print(f"打开 {url}  (Ctrl+C 停止)")
        if not args.no_open:
            webbrowser.open(url)
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n已停止。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
