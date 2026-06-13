import dgram from 'node:dgram';
import { writeFileSync, appendFileSync } from 'node:fs';

const PORT = 20440;
const OUT = `capture-${Date.now()}.jsonl`;
const sock = dgram.createSocket('udp4');
const t0 = process.hrtime.bigint();

writeFileSync(OUT, ''); // truncate
sock.on('message', (buf) => {
  const tNs = Number(process.hrtime.bigint() - t0);
  // store raw bytes as base64 + monotonic offset in ms + length
  appendFileSync(OUT, JSON.stringify({
    t: tNs / 1e6,
    len: buf.length,
    b64: buf.toString('base64'),
  }) + '\n');
});
sock.on('listening', () => console.log(`listening :${PORT} -> ${OUT}`));
sock.bind(PORT);