using Moq;
using MinecraftProtoNet.Core.Abstractions;
using FluentAssertions;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Abstractions.Api;
using MinecraftProtoNet.Core.Dtos;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.State.Base;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Commands;

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
        var mockClient = new Mock<IMinecraftClient>();
        mockClient.Setup(x => x.State).Returns(new ClientState());
        var sink = new DefaultChatSink(mockClient.Object);
        var message = "Test Message";
        
        // Act
        await sink.EmitAsync(message, CancellationToken.None);
        
        // Assert
        mockClient.Verify(x => x.SendPacketAsync(
            It.Is<ChatPacket>(p => p.Message == message), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task WebcoreChatSink_EmitAsync_ShouldPostToApi()
    {
        // Arrange
        var mockApi = new Mock<IWebcoreChatApi>();
        var sink = new WebcoreChatSink(mockApi.Object);
        var message = "Test Message";
        
        // Act
        await sink.EmitAsync(message, CancellationToken.None);
        
        // Assert
        mockApi.Verify(x => x.PostRedirectedChatAsync(
            It.Is<ChatRedirectRequest>(r => r.Message == message), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task MinecraftClient_SendChatMessageAsync_ShouldUseWebcoreSink_WhenRedirectIsEnabled()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockApi = new Mock<IWebcoreChatApi>();
        var webcoreSinkImpl = new WebcoreChatSink(mockApi.Object);
        
        mockServiceProvider.Setup(x => x.GetService(typeof(WebcoreChatSink))).Returns(webcoreSinkImpl);
        mockServiceProvider.Setup(x => x.GetService(typeof(ILogger<CommandRegistry>))).Returns(new Mock<ILogger<CommandRegistry>>().Object);
        
        var state = new ClientState();
        state.BotSettings.RedirectChat = true;

        var client = new MinecraftClient(
            mockServiceProvider.Object,
            state,
            new Mock<IPacketSender>().Object,
            new Mock<IPacketService>().Object,
            new Mock<IPhysicsService>().Object,
            new Mock<CommandRegistry>().Object,
            new Mock<ILogger<MinecraftClient>>().Object
        );

        var message = "Redirected message";

        // Act
        await client.SendChatMessageAsync(message);

        // Assert
        mockApi.Verify(x => x.PostRedirectedChatAsync(
            It.Is<ChatRedirectRequest>(r => r.Message == message), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
