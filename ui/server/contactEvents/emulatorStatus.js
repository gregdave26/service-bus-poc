import net from "node:net";

export function getEmulatorStatus() {
  const host = process.env.ServiceBus__EmulatorHost ?? "127.0.0.1";
  const port = Number.parseInt(process.env.ServiceBus__EmulatorPort ?? "5672", 10);

  return new Promise((resolve) => {
    const socket = net.createConnection({ host, port });
    const finish = (running, error) => {
      socket.destroy();
      resolve({
        running,
        host,
        port,
        checkedAt: new Date().toISOString(),
        error: error ?? null,
      });
    };

    socket.setTimeout(1500, () => finish(false, "Connection timed out"));
    socket.once("connect", () => finish(true));
    socket.once("error", (error) => finish(false, error.code ?? error.message));
  });
}
