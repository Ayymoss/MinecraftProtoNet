using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Auth.Authenticators;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Configuration.Clientbound;
using MinecraftProtoNet.Packets.Login.Clientbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using MinecraftProtoNet.Services;
using HelloPacket = MinecraftProtoNet.Packets.Login.Clientbound.HelloPacket;

namespace MinecraftProtoNet.Handlers;

[HandlesPacket(typeof(DisconnectPacket))]
[HandlesPacket(typeof(HelloPacket))]
[HandlesPacket(typeof(LoginSuccessPacket))]
[HandlesPacket(typeof(LoginCompressionPacket))]
public class LoginHandler : IPacketHandler
{
    private readonly ILogger<LoginHandler> _logger = LoggingConfiguration.CreateLogger<LoginHandler>();

    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(LoginHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case DisconnectPacket disconnectPacket:
                _logger.LogWarning("Disconnected: {Reason}", disconnectPacket.Reason);
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
                _logger.LogDebug("Switching protocol state: {ProtocolState}", client.ProtocolState);
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
            _logger.LogInformation("Not using compression due to negative threshold");
            return;
        }
        
        _logger.LogInformation("Enabling compression with threshold: {Threshold}", compressionPacket.Threshold);
        
        client.EnableCompression(compressionPacket.Threshold);
    }

    private async Task HandleEncryptionRequestAsync(HelloPacket helloPacket, IMinecraftClient client)
    {
        _logger.LogInformation("Received HelloPacket. Initiating encryption...");

        // 1. Generate Shared Secret
        var sharedSecret = RandomNumberGenerator.GetBytes(16);

        if (helloPacket.ShouldAuthenticate)
        {
            _logger.LogInformation("Server requires authentication. Contacting Mojang session server...");
            // 2. Authenticate with Mojang Session Server
            var authenticated = await MinecraftAuthenticator.AuthenticateWithMojangAsync(helloPacket.ServerId, sharedSecret,
                helloPacket.PublicKey, client.AuthResult!);

            if (!authenticated)
            {
                _logger.LogError("Mojang authentication failed. Disconnecting");
                await client.DisconnectAsync();
                return;
            }

            _logger.LogInformation("Mojang authentication successful");
            
            // Small delay to ensure Mojang's session has propagated before server queries it
            await Task.Delay(100);
        }
        else
        {
            _logger.LogInformation("Server is in Offline Mode (authentication not required by server)");
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

            _logger.LogDebug("Shared Secret ({Length} bytes): {SharedSecret}",
                sharedSecret.Length, Convert.ToHexString(sharedSecret));
            _logger.LogDebug("Encrypted Shared Secret ({Length} bytes)", encryptedSharedSecret.Length);
            _logger.LogDebug("Encrypted Verify Token ({Length} bytes)", encryptedVerifyToken.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform RSA encryption");
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
        _logger.LogInformation("Sent KeyPacket. Enabling AES encryption...");

        // 5. Enable Encryption Layer
        client.EnableEncryption(sharedSecret);

        _logger.LogInformation("Encryption enabled for subsequent packets");
    }
}
