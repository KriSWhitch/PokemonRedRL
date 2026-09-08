using System.Net.Sockets;
using System.Text;

namespace PokemonRedRL.ControlPanel.Services;

/// <summary>
/// Probes a specific TCP port for a live mGBA bridge instance using the same
/// "ping\n" -&gt; "pong" handshake as <c>PokemonRedRL.Core.Helpers.NetworkConfigFactory</c>.
/// Unlike that factory, this probes one *known* port at a time (the port pre-assigned to a
/// manifest slot) rather than scanning a range — manual-attach mode requires the user to make
/// each mGBA instance listen on its slot's assigned port, so there is no ambiguity to resolve here.
/// </summary>
public interface IEmulatorPingProbe
{
    Task<bool> IsReadyAsync(int port, TimeSpan timeout);
}

public class EmulatorPingProbe : IEmulatorPingProbe
{
    public async Task<bool> IsReadyAsync(int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeout);

            var connectTask = client.ConnectAsync("127.0.0.1", port, cts.Token).AsTask();
            await connectTask;
            if (!client.Connected) return false;

            var stream = client.GetStream();
            var pingBytes = Encoding.ASCII.GetBytes("ping\n");
            await stream.WriteAsync(pingBytes, cts.Token);

            var buffer = new byte[256];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cts.Token);
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
            return response == "pong";
        }
        catch
        {
            return false;
        }
    }
}
