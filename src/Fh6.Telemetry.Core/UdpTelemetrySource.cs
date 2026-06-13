using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Fh6.Telemetry.Core;

public sealed class UdpTelemetrySource : ITelemetrySource, IDisposable
{
    private readonly UdpClient _client;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public UdpTelemetrySource(int port) => _client = new UdpClient(port);

    public IEnumerable<CaptureFrame> Frames()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] data;
            try
            {
                data = _client.Receive(ref endpoint);
            }
            catch (SocketException)
            {
                yield break; // socket closed (e.g. Ctrl-C disposed the client)
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            yield return new CaptureFrame(_clock.Elapsed.TotalMilliseconds, data);
        }
    }

    public void Dispose() => _client.Dispose();
}
