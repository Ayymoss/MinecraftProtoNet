using Humanizer;

namespace MinecraftProtoNet.Utilities;

public static class DataTypeHelper
{
    public static class VarInt
    {
        public static int Read(ReadOnlySpan<byte> buffer, out int bytesRead)
        {
            bytesRead = 0;
            var result = 0;
            byte read;

            do
            {
                read = buffer[bytesRead];
                var value = read & 127;
                result |= value << (7 * bytesRead);

                bytesRead++;
                if (bytesRead > 5) throw new ArithmeticException("VarInt too long");
            } while ((read & 0b10000000) != 0);

            return result;
        }

        public static void Write(Span<byte> buffer, int value, out int bytesWritten)
        {
            bytesWritten = 0;
            do
            {
                var temp = (byte)(value & 0x7F);
                value >>>= 7;
                if (value != 0)
                {
                    temp |= 0x80;
                }

                buffer[bytesWritten++] = temp;
            } while (value != 0);
        }
    }

    /// <summary>
    /// Used for internal packet namespaces. Will not work for other namespaces.
    /// </summary>
    /// <param name="fullname"></param>
    /// <param name="packetId"></param>
    /// <returns></returns>
    public static string NamespaceToPrettyString(this string fullname, int packetId)
    {
        var parts = fullname.Split('.');
        if (parts.Length < 5) return fullname;
        return $"[white][[[/][yellow]{parts[2]}[/][white] -> [/](0x{packetId:X2}) " +
               $"[cyan]{parts[4].Replace("Packet", string.Empty).Titleize()}[/][white]]][/]";
    }
}
