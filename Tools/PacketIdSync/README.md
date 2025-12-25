# PacketIdSync Tool

A utility for automatically synchronizing C# packet IDs with the Minecraft Java reference protocol.

## Purpose

When Minecraft updates, packet IDs shift based on their registration order in `GameProtocols.java`. This tool parses the Java reference file and updates the `[Packet(0xNN, ...)]` attributes in your C# packet files.

## Usage

### Prerequisites

1. Copy `GameProtocols.java` from the Minecraft decompiled source to the `Protocol/` folder
2. Build the tool (it's part of the solution)

### Running the Tool

From the solution root:

```powershell
# Preview changes (dry run)
dotnet run --project Tools/PacketIdSync -- Protocol/GameProtocols.java MinecraftProtoNet/Packets/Play --dry-run

# Apply changes
dotnet run --project Tools/PacketIdSync -- Protocol/GameProtocols.java MinecraftProtoNet/Packets/Play
```

Or using the built executable:

```powershell
.\Tools\PacketIdSync\bin\Debug\net10.0\PacketIdSync.exe "Protocol\GameProtocols.java" "MinecraftProtoNet\Packets\Play"
```

### Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `protocol-file` | Path to `GameProtocols.java` | `Protocol/GameProtocols.java` |
| `packets-dir` | Path to C# packets directory | `MinecraftProtoNet/Packets/Play` |
| `--dry-run` | Preview changes without writing | - |

## How It Works

1. **Parses** `GameProtocols.java` to extract packet registrations
2. **Counts** registration order to determine packet IDs
3. **Maps** Java packet names to C# names (handles naming conventions)
4. **Updates** the `[Packet(...)]` attributes in C# files

## Name Mapping

The tool handles naming differences between Java and C#:

| Java Pattern | C# Pattern |
|--------------|------------|
| `ClientboundLoginPacket` | `LoginPacket` |
| `ServerboundChatPacket` | `ChatPacket` |
| `MoveEntityPacket.Pos` | `MoveEntityPositionPacket` |
| `MoveEntityPacket.PosRot` | `MoveEntityPositionRotationPacket` |
| `BlockChangedAckPacket` | `BlockChangedAcknowledgementPacket` |

## After Running

Verify the changes by building the project:

```powershell
dotnet build
```

Then test the client connection to a Minecraft server.
