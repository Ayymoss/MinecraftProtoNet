using Moq;
using MinecraftProtoNet.Core.Abstractions;
using FluentAssertions;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Tests.Core;

public class ChatSinkTests
{
    [Fact]
    public async Task IChatSink_EmitAsync_ShouldBeCalled()
    {
        // Arrange
        var mockSink = new Mock<IChatSink>();
        var message = "Hello, Minecraft!";
        
        // Act
        await mockSink.Object.EmitAsync(message, CancellationToken.None);
        
        // Assert
        mockSink.Verify(x => x.EmitAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DefaultChatSink_EmitAsync_ShouldSendChatPacket()
    {
        // Arrange
        var mockSender = new Mock<IPacketSender>();
        var sink = new DefaultChatSink(mockSender.Object);
        var message = "Test Message";
        
        // Act
        await sink.EmitAsync(message, CancellationToken.None);
        
        // Assert
        mockSender.Verify(x => x.SendPacketAsync(
            It.Is<ChatPacket>(p => p.Message == message), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
