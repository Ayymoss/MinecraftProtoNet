namespace MinecraftProtoNet.Core.Enums;

/// <summary>
/// Actions for ServerboundPlayerCommandPacket (0x29).
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundPlayerCommandPacket.java:57-64
/// IMPORTANT: In 1.21.x, PRESS_SHIFT_KEY and RELEASE_SHIFT_KEY were REMOVED from this enum.
/// Sneaking is now communicated solely via PlayerInputPacket (0x2A) Shift flag.
/// The enum values shifted accordingly - STOP_SLEEPING is now 0, START_SPRINTING is 1, etc.
/// </summary>
public enum PlayerAction
{
    StopSleeping = 0,           // STOP_SLEEPING
    StartSprint = 1,            // START_SPRINTING
    StopSprint = 2,             // STOP_SPRINTING
    StartJumpWithHorse = 3,     // START_RIDING_JUMP
    StopJumpWithHorse = 4,      // STOP_RIDING_JUMP
    OpenVehicleInventory = 5,   // OPEN_INVENTORY
    StartFlyingWithElytra = 6,  // START_FALL_FLYING
}
