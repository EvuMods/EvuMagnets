using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;

namespace EvuMagnets;

internal static class ItemIcons
{
    public static Sprite Load(string id)
    {
        var name = "EvuMagnets.icons." + id + ".png";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("Missing embedded icon " + name + ".");
            }

            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                var texture = DecodePng(memory.ToArray(), name);
                texture.filterMode = FilterMode.Bilinear;
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }
    }

    static Texture2D DecodePng(byte[] png, string name)
    {
        if (!TryDecodeRgba(png, out var width, out var height, out var rgba))
        {
            throw new InvalidOperationException("Could not read icon " + name + ".");
        }

        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            var source = y * width * 4;
            var dest = (height - 1 - y) * width;
            for (var x = 0; x < width; x++)
            {
                var i = source + (x * 4);
                pixels[dest + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    internal static bool TryDecodeRgba(byte[] png, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = Array.Empty<byte>();
        if (png == null || png.Length < 8 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71)
        {
            return false;
        }

        var inflated = new MemoryStream();
        var offset = 8;
        var bitDepth = 0;
        var colorType = 0;
        while (offset + 8 <= png.Length)
        {
            var length = ReadInt(png, offset);
            offset += 4;
            var type = ReadInt(png, offset);
            offset += 4;
            if (length < 0 || offset + length + 4 > png.Length)
            {
                return false;
            }

            if (type == 0x49484452)
            {
                width = ReadInt(png, offset);
                height = ReadInt(png, offset + 4);
                bitDepth = png[offset + 8];
                colorType = png[offset + 9];
                if (png[offset + 12] != 0)
                {
                    return false;
                }
            }
            else if (type == 0x49444154)
            {
                inflated.Write(png, offset, length);
            }
            else if (type == 0x49454E44)
            {
                break;
            }

            offset += length + 4;
        }

        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6)
        {
            return false;
        }

        var raw = Inflate(inflated.ToArray());
        var stride = width * 4;
        if (raw.Length < height * (stride + 1))
        {
            return false;
        }

        rgba = new byte[height * stride];
        var previous = new byte[stride];
        var current = new byte[stride];
        var rawOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = raw[rawOffset++];
            Buffer.BlockCopy(raw, rawOffset, current, 0, stride);
            rawOffset += stride;
            Unfilter(filter, current, previous, stride);
            Buffer.BlockCopy(current, 0, rgba, y * stride, stride);
            var swap = previous;
            previous = current;
            current = swap;
        }

        return true;
    }

    static void Unfilter(byte filter, byte[] current, byte[] previous, int stride)
    {
        const int bytesPerPixel = 4;
        for (var i = 0; i < stride; i++)
        {
            var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : (byte)0;
            var up = previous[i];
            var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : (byte)0;
            var value = filter switch
            {
                0 => current[i],
                1 => (byte)(current[i] + left),
                2 => (byte)(current[i] + up),
                3 => (byte)(current[i] + ((left + up) / 2)),
                4 => (byte)(current[i] + Paeth(left, up, upLeft)),
                _ => current[i],
            };
            current[i] = value;
        }
    }

    static byte Paeth(byte left, byte up, byte upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
        {
            return left;
        }

        return upDistance <= upLeftDistance ? up : upLeft;
    }

    static byte[] Inflate(byte[] zlib)
    {
        if (zlib.Length < 6)
        {
            return Array.Empty<byte>();
        }

        using (var input = new MemoryStream(zlib, 2, zlib.Length - 6))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }

    static int ReadInt(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }
}
