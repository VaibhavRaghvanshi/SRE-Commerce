using System.Buffers.Binary;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Catalog.Domain;

public sealed record ProductImageInfo(string ContentType, int Width, int Height);
public static class ProductImageRules
{
    public const int MaximumBytes = 750000;
    public static ProductImageInfo Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 45 or > MaximumBytes) throw new DomainFault("InvalidProductImage");
        if (bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.Slice(12, 4).SequenceEqual("IHDR"u8) &&
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) == 13 && bytes.Slice(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
            return Dimensions("image/png", BinaryPrimitives.ReadInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadInt32BigEndian(bytes[20..]));
        if (bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9)
        {
            var cursor = 2;
            while (cursor + 3 < bytes.Length)
            {
                if (bytes[cursor++] != 0xff) break;
                while (cursor < bytes.Length && bytes[cursor] == 0xff) cursor++;
                if (cursor >= bytes.Length) break;
                var marker = bytes[cursor++];
                if (marker is 0xd9 or 0xda) break;
                if (marker is 0x01 or >= 0xd0 and <= 0xd8) continue;
                if (cursor + 2 > bytes.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[cursor..]);
                if (length < 2 || cursor + length > bytes.Length) break;
                if (marker is 0xc0 or 0xc1 or 0xc2)
                {
                    if (length < 8) break;
                    return Dimensions("image/jpeg", BinaryPrimitives.ReadUInt16BigEndian(bytes[(cursor + 5)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(cursor + 3)..]));
                }
                cursor += length;
            }
        }
        throw new DomainFault("UnsupportedProductImage");
    }
    private static ProductImageInfo Dimensions(string type, int width, int height)
    {
        if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 16777216)
            throw new DomainFault("ProductImageDimensionsTooLarge");
        return new(type, width, height);
    }
}
