using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core;

public class Connection : IDisposable
{
    public const bool Debug = false;
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;

    public async Task ConnectAsync(string host, int port)
    {
        await _client.ConnectAsync(host, port);
        _stream = _client.GetStream();
    }

    public async Task<byte[]> ReadPacketBytesAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected.");

        var lengthBuffer = new byte[5];
        var totalLengthBytesRead = 0;
        int packetLength;

        do
        {
            var lengthBytesRead = await _stream.ReadAsync(lengthBuffer.AsMemory(totalLengthBytesRead, 1), cancellationToken);
            if (lengthBytesRead == 0) throw new EndOfStreamException("Connection closed while reading packet length.");

            totalLengthBytesRead++;
            if (!TryReadVarInt(lengthBuffer, out packetLength, out var varIntLengthRead)) continue;
            if (varIntLengthRead == totalLengthBytesRead) break;
        } while (true);

        var packetData = new byte[packetLength];
        var totalBytesRead = 0;

        while (totalBytesRead < packetLength)
        {
            var bytesToRead = Math.Min(packetLength - totalBytesRead, _client.ReceiveBufferSize);
            var bytesRead = await _stream.ReadAsync(packetData.AsMemory(totalBytesRead, bytesToRead), cancellationToken);

            if (bytesRead is 0)
            {
                throw new EndOfStreamException("Connection closed prematurely.");
            }

            totalBytesRead += bytesRead;
        }

        // Debug
        if (Debug)
        {
            AnsiConsole.MarkupLine($"[grey][[DEBUG]][/] [blue][[->CLIENT]][/] [white]BYTES:[/] {BitConverter.ToString(packetData)}");
            var bytesAsString = Regex.Replace(Encoding.UTF8.GetString(packetData), @"\p{C}+", " ");
            AnsiConsole.Markup("[grey][[DEBUG]][/] [blue][[->CLIENT]][/] [white]AS STRING:[/] ");
            AnsiConsole.WriteLine(bytesAsString);
        }
        // Debug

        return packetData;
    }

    private static bool TryReadVarInt(byte[] buffer, out int value, out int bytesRead)
    {
        try
        {
            // TODO: Remove this helper method for another solution
            value = DataTypeHelper.VarInt.Read(buffer, out bytesRead);
            return true;
        }
        catch (InvalidOperationException)
        {
            value = 0;
            bytesRead = 0;
            return false;
        }
    }

    public async Task SendPacketAsync(IOutgoingPacket packet, CancellationToken cancellationToken = default)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected.");

        var writer = new PacketBufferWriter();
        packet.Serialize(ref writer);
        var packetData = writer.WrittenSpan.ToArray();
        writer.Dispose();

        var lengthBytes = new byte[5]; //Maximum VarInt Length
        // TODO: Remove this helper method for another solution
        DataTypeHelper.VarInt.Write(lengthBytes, packetData.Length, out var varIntLength); // Get VarInt length

        // Debug
        if (Debug)
        {
            var fullPacket = new byte[varIntLength + packetData.Length];
            Array.Copy(lengthBytes, 0, fullPacket, 0, varIntLength);
            Array.Copy(packetData, 0, fullPacket, varIntLength, packetData.Length);
            AnsiConsole.MarkupLine(
                $"[grey][[DEBUG]][/] [green][[->SERVER]][/] {packet.GetType().FullName?.NamespaceToPrettyString()} [white]BYTES:[/] {BitConverter.ToString(fullPacket)}"); // Debugging
            var bytesAsString = Regex.Replace(Encoding.UTF8.GetString(fullPacket), @"\p{C}+", " ");
            AnsiConsole.Markup(
                $"[grey][[DEBUG]][/] [green][[->SERVER]][/] {packet.GetType().FullName?.NamespaceToPrettyString()} [white]AS STRING:[/] ");
            AnsiConsole.WriteLine(bytesAsString);
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey][[DEBUG]] {TimeProvider.System.GetUtcNow():HH:mm:ss.fff}[/] [green][[->SERVER]][/] {packet.GetType().FullName?.NamespaceToPrettyString()}");
        }
        // Debug

        await _stream.WriteAsync(lengthBytes.AsMemory(0, varIntLength), cancellationToken); // Write VarInt length
        await _stream.WriteAsync(packetData, cancellationToken); // Write packet data
        await _stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client.Dispose();
    }
}
