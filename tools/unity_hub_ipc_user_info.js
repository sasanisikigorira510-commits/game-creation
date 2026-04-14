const path = require("node:path");

const hubResourcesDir =
  process.env.UNITY_HUB_RESOURCES_DIR ||
  "/Users/andou/Desktop/Unity Hub.app/Contents/Resources";
const nodeIpcPath = path.join(hubResourcesDir, "app.asar", "node_modules", "node-ipc");
const ipc = require(nodeIpcPath);

const server = "/tmp/Unity-hubIPCService.sock";
const responseEvent = "userInfo:changed";
const requestEvent = "userInfo:get";

ipc.config.id = `codex-unity-user-info-${process.pid}-${Date.now()}`;
ipc.config.retry = 1500;
ipc.config.silent = true;

let finished = false;

function finish(code, message) {
  if (finished) {
    return;
  }

  finished = true;

  if (message) {
    const stream = code === 0 ? process.stdout : process.stderr;
    stream.write(message);
  }

  try {
    ipc.disconnect(server);
  } catch {
    // Ignore disconnect errors during shutdown.
  }

  setTimeout(() => process.exit(code), 100);
}

ipc.connectTo(server, server, () => {
  const client = ipc.of[server];

  client.on("connect", () => {
    client.emit(requestEvent, {});
  });

  client.on(responseEvent, (data) => {
    finish(0, `${JSON.stringify(data)}\n`);
  });

  client.on("error", (error) => {
    const message = error && error.message ? error.message : String(error);
    finish(1, `${message}\n`);
  });

  setTimeout(() => {
    finish(2, "Timed out waiting for Unity Hub user info.\n");
  }, 10000);
});
