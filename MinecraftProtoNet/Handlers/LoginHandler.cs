using System.Security.Cryptography;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Auth.Authenticators;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using MinecraftProtoNet.Packets.Login.Clientbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Services;
using Spectre.Console;
using HelloPacket = MinecraftProtoNet.Packets.Login.Clientbound.HelloPacket;

namespace MinecraftProtoNet.Handlers;

[HandlesPacket(typeof(DisconnectPacket))]
[HandlesPacket(typeof(HelloPacket))]
[HandlesPacket(typeof(LoginSuccessPacket))]
[HandlesPacket(typeof(LoginCompressionPacket))]
public class LoginHandler : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(LoginHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case DisconnectPacket disconnectPacket:
                AnsiConsole.MarkupLine($"[red]Disconnected: {disconnectPacket.Reason}[/]");
                await client.DisconnectAsync();
                break;
            case HelloPacket helloPacket:
            {
                await HandleEncryptionRequestAsync(helloPacket, client);
                break;
            }
            case LoginSuccessPacket loginSuccess:
                await client.SendPacketAsync(new LoginAcknowledgedPacket());
                client.ProtocolState = ProtocolState.Configuration;
                AnsiConsole.MarkupLine(
                    $"[grey][[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.ProtocolState.ToString()}[/]");
                break;
            case LoginCompressionPacket compressionPacket:
            {
                await HandleCompressionAsync(compressionPacket, client);
                break;
            }
        }
    }

    private async Task HandleCompressionAsync(LoginCompressionPacket compressionPacket, IMinecraftClient client)
    {
        if (compressionPacket.Threshold < 0)
        {
            AnsiConsole.MarkupLine("[yellow]Not using compression due to negative threshold.[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[yellow]Enabling compression with threshold: {compressionPacket.Threshold}[/]");
        
        client.EnableCompression(compressionPacket.Threshold);
    }

    private async Task HandleEncryptionRequestAsync(HelloPacket helloPacket, IMinecraftClient client)
    {
        AnsiConsole.MarkupLine("[yellow]Received HelloPacket. Initiating encryption...[/]");

        // 1. Generate Shared Secret
        var sharedSecret = RandomNumberGenerator.GetBytes(16);

        if (helloPacket.ShouldAuthenticate)
        {
            AnsiConsole.MarkupLine("[yellow]Server requires authentication. Contacting Mojang session server...[/]");
            // 2. Authenticate with Mojang Session Server
            var authenticated = await MinecraftAuthenticator.AuthenticateWithMojangAsync(helloPacket.ServerId, sharedSecret,
                helloPacket.PublicKey, client.AuthResult);

            if (!authenticated)
            {
                AnsiConsole.MarkupLine("[red]Mojang authentication failed. Disconnecting.[/]");
                await client.DisconnectAsync();
                return;
            }

            AnsiConsole.MarkupLine("[green]Mojang authentication successful.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Server is in Offline Mode (authentication not required by server).[/]");
        }

        // 3. Encrypt Shared Secret and Verify Token using Server's Public Key
        byte[] encryptedSharedSecret;
        byte[] encryptedVerifyToken;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(helloPacket.PublicKey, out _);

            encryptedSharedSecret = rsa.Encrypt(sharedSecret, RSAEncryptionPadding.Pkcs1);
            encryptedVerifyToken = rsa.Encrypt(helloPacket.VerifyToken, RSAEncryptionPadding.Pkcs1);

            AnsiConsole.MarkupLine(
                $"[grey]Shared Secret ({sharedSecret.Length} bytes): {Convert.ToHexString(sharedSecret)}[/]");
            AnsiConsole.MarkupLine($"[grey]Encrypted Shared Secret ({encryptedSharedSecret.Length} bytes).[/]");
            AnsiConsole.MarkupLine($"[grey]Encrypted Verify Token ({encryptedVerifyToken.Length} bytes).[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Failed to perform RSA encryption.[/]");
            AnsiConsole.WriteException(ex);
            await client.DisconnectAsync();
            return;
        }

        // 4. Send KeyPacket
        var keyPacket = new KeyPacket
        {
            SharedSecret = encryptedSharedSecret,
            VerifyToken = encryptedVerifyToken
        };
        await client.SendPacketAsync(keyPacket);
        AnsiConsole.MarkupLine("[yellow]Sent KeyPacket. Enabling AES encryption...[/]");

        // 5. Enable Encryption Layer
        client.EnableEncryption(sharedSecret);

        AnsiConsole.MarkupLine("[green]Encryption enabled for subsequent packets.[/]");
    }
}
