"""Local validation server. Bind only to loopback; deploy behind a hardened HTTPS service."""
import argparse
import json
import os
import secrets
from pathlib import Path
from store import Store


def create_server(store, admin_token, port=8787):
    from socketserver import ThreadingMixIn
    from wsgiref.simple_server import WSGIServer, WSGIRequestHandler, make_server
    from application import Application
    from security import AdminAuth

    class Server(ThreadingMixIn, WSGIServer):
        daemon_threads = True

    class Handler(WSGIRequestHandler):
        def log_message(self, *args):
            pass

    return make_server('127.0.0.1', port, Application(store, AdminAuth(token=admin_token)),
                       server_class=Server, handler_class=Handler)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data-dir', default=str(Path(__file__).parent / '.data'))
    parser.add_argument('--port', type=int, default=8787)
    parser.add_argument('--backup', help='Write a consistent database backup and exit')
    args = parser.parse_args()
    directory = Path(args.data_dir)
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    os.chmod(directory, 0o700)
    token_file = directory / 'admin-token'
    if not token_file.exists():
        with os.fdopen(os.open(token_file, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as f:
            f.write(secrets.token_urlsafe(48))
    verifier = None
    if os.getenv('WITCH_APPLE_BUNDLE_ID'):
        from apple_verifier import build_verifier
        verifier = build_verifier()
    store = Store(directory / 'players.sqlite', json.loads(Path(__file__).with_name('catalog.json').read_text()), verifier)
    if args.backup:
        store.backup(args.backup)
        print('Consistent backup written.')
        return
    print(f'Local admin: http://127.0.0.1:{args.port} ; admin credential file: {token_file}', flush=True)
    create_server(store, token_file.read_text().strip(), args.port).serve_forever()


if __name__ == '__main__': main()
