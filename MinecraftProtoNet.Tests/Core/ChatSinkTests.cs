using Moq;
using MinecraftProtoNet.Core.Abstractions;
using FluentAssertions;

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
}
