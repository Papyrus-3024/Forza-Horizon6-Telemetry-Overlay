// Tiny static server + capture listing API for the FH6 top-down replay app.
// No external dependencies: plain Node http + fs.

import { createServer } from 'node:http';
import { readFile, readdir, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PORT = process.env.PORT || 3000;
const PUBLIC_DIR = join(__dirname, 'public');
const CAPTURES_DIR = join(__dirname, 'captures');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.jsonl': 'application/x-ndjson; charset=utf-8',
  '.avif': 'image/avif',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

function send(res, status, body, headers = {}) {
  res.writeHead(status, { 'Cache-Control': 'no-cache', ...headers });
  res.end(body);
}

// Resolve a request path safely under a base dir, blocking traversal.
function safeJoin(base, reqPath) {
  const decoded = decodeURIComponent(reqPath);
  const target = normalize(join(base, decoded));
  const root = resolve(base);
  if (target !== root && !target.startsWith(root + sep)) return null;
  return target;
}

async function serveStatic(res, filePath) {
  try {
    const info = await stat(filePath);
    if (info.isDirectory()) filePath = join(filePath, 'index.html');
    const data = await readFile(filePath);
    const mime = MIME[extname(filePath).toLowerCase()] || 'application/octet-stream';
    // Big map images can be cached aggressively.
    const cache = mime === 'image/avif' ? 'public, max-age=86400' : 'no-cache';
    send(res, 200, data, { 'Content-Type': mime, 'Cache-Control': cache });
  } catch {
    send(res, 404, 'Not found', { 'Content-Type': 'text/plain' });
  }
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  let pathname = url.pathname;

  // API: list available captures.
  if (pathname === '/api/captures') {
    try {
      const entries = await readdir(CAPTURES_DIR);
      const list = entries.filter((n) => n.toLowerCase().endsWith('.jsonl')).sort();
      send(res, 200, JSON.stringify(list), { 'Content-Type': MIME['.json'] });
    } catch {
      send(res, 200, '[]', { 'Content-Type': MIME['.json'] });
    }
    return;
  }

  // API: fetch a single capture by name.
  if (pathname.startsWith('/api/capture/')) {
    const name = pathname.slice('/api/capture/'.length);
    const filePath = safeJoin(CAPTURES_DIR, name);
    if (!filePath || !filePath.toLowerCase().endsWith('.jsonl')) {
      send(res, 400, 'Bad capture name', { 'Content-Type': 'text/plain' });
      return;
    }
    await serveStatic(res, filePath);
    return;
  }

  // List which season maps are actually present (so the UI can note missing ones).
  if (pathname === '/api/maps') {
    const seasons = ['spring', 'summer', 'autumn', 'winter'];
    const present = {};
    for (const s of seasons) {
      try {
        await stat(join(PUBLIC_DIR, 'maps', `${s}.avif`));
        present[s] = true;
      } catch {
        present[s] = false;
      }
    }
    send(res, 200, JSON.stringify(present), { 'Content-Type': MIME['.json'] });
    return;
  }

  // Static files.
  if (pathname === '/') pathname = '/index.html';
  const filePath = safeJoin(PUBLIC_DIR, pathname);
  if (!filePath) {
    send(res, 403, 'Forbidden', { 'Content-Type': 'text/plain' });
    return;
  }
  await serveStatic(res, filePath);
});

server.listen(PORT, () => {
  console.log(`FH6 web replay running at http://localhost:${PORT}`);
});
