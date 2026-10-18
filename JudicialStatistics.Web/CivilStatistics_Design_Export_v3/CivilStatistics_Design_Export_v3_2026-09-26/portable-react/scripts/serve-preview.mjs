import http from "node:http";
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { resolve, relative, extname, isAbsolute, sep } from "node:path";
import { fileURLToPath } from "node:url";

// Local preview of the included static build; Node's standard library only.
const directory = fileURLToPath(new URL("../dist/", import.meta.url));
const port = Number(process.argv[2] ?? 4173);
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error("Invalid port");
try { await stat(resolve(directory, "index.html")); }
catch { throw new Error("dist/index.html is missing. Run npm run build first."); }
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".svg": "image/svg+xml", ".json": "application/json; charset=utf-8", ".png": "image/png", ".jpg": "image/jpeg", ".woff2": "font/woff2", ".ico": "image/x-icon" };
const server = http.createServer(async (req, res) => {
  if (req.method !== "GET" && req.method !== "HEAD") {
    res.writeHead(405, { Allow: "GET, HEAD" }); res.end(); return;
  }
  let pathname;
  try { pathname = decodeURIComponent(new URL(req.url ?? "/", "http://localhost").pathname); }
  catch { res.writeHead(400); res.end("Bad request"); return; }
  const path = resolve(directory, `.${pathname.endsWith("/") ? `${pathname}index.html` : pathname}`);
  const rel = relative(directory, path);
  if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel) || pathname.includes("\0")) {
    res.writeHead(403); res.end("Forbidden"); return;
  }
  try {
    const info = await stat(path);
    if (!info.isFile()) throw new Error("Not a file");
    res.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream", "Content-Length": info.size, "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" });
    if (req.method === "HEAD") { res.end(); return; }
    const stream = createReadStream(path);
    stream.on("error", () => res.destroy());
    stream.pipe(res);
  } catch { res.writeHead(404); res.end("Not found"); }
});
server.on("error", (error) => { console.error(error.message); process.exitCode = 1; });
server.listen(port, "127.0.0.1", () => console.log(`Civil statistics preview: http://127.0.0.1:${port}`));
