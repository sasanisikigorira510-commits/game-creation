"""Single VPS, Caddy on the same host. Do not expose this port externally."""
bind = '127.0.0.1:8788'
workers = 1  # Rate limits are process-local; SQLite writes are serialized.
worker_class = 'gthread'
threads = 4
timeout = 45
graceful_timeout = 45
keepalive = 2
limit_request_line = 4094
limit_request_fields = 40
limit_request_field_size = 8190
forwarded_allow_ips = '127.0.0.1,::1'
secure_scheme_headers = {'X-FORWARDED-PROTO': 'https'}
accesslog = None  # Application logs route categories, never account IDs or queries.
errorlog = '-'
loglevel = 'info'
capture_output = True
umask = 0o077
