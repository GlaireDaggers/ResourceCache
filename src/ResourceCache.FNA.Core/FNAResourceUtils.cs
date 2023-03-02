using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using QoiSharp;
using ResourceCache.Core;

namespace ResourceCache.FNA
{
    /// <summary>
    /// Helper class to install resource factories for FNA content types such as Effect, Texture, etc
    /// </summary>
    public static class FNAResourceUtils
    {
        /// <summary>
        /// Install FNA resource loaders into the resource cache for the given FNA game
        /// </summary>
        /// <param name="resourceCache">The resource cache to install loaders into</param>
        /// <param name="game">The game instance to load resources for</param>
        public static void InstallFNAResourceLoaders(this ResourceManager resourceCache, Game game)
        {
            // Effect loader
            resourceCache.RegisterFactory((stream) =>
            {
                byte[] effectCode;
                using (var memstream = new MemoryStream())
                {
                    stream.CopyTo(memstream);
                    effectCode = memstream.ToArray();
                }

                return new Effect(game.GraphicsDevice, effectCode);
            }, true);

            // Texture2D loader
            resourceCache.RegisterFactory((stream) =>
            {
                // workaround for cases where stream does not support seeking
                using (var memStream = new MemoryStream())
                {
                    stream.CopyTo(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);

                    byte[] header = new byte[4];

                    memStream.Read(header, 0, header.Length);
                    memStream.Seek(0, SeekOrigin.Begin);

                    // TODO: KTX files?

                    // DDS file?
                    if (header[0] == 'D' &&
                        header[1] == 'D' &&
                        header[2] == 'S' &&
                        header[3] == ' ')
                    {
                        return Texture2D.DDSFromStreamEXT(game.GraphicsDevice, memStream);
                    }
                    // QOI file?
                    else if (header[0] == 'q' &&
                        header[1] == 'o' &&
                        header[2] == 'i' &&
                        header[3] == 'f')
                    {
                        // decode QOI image and copy contents to new Texture2D
                        byte[] data = memStream.ToArray();
                        QoiImage imageData = QoiDecoder.Decode(data);
                        return FromQoi(game.GraphicsDevice, imageData);
                    }
                    // something else.
                    else
                    {
                        return Texture2D.FromStream(game.GraphicsDevice, memStream);
                    }
                }
            }, true);

            // TextureCube loader
            resourceCache.RegisterFactory((stream) =>
            {
                // workaround for cases where stream does not support seeking
                using (var memStream = new MemoryStream())
                {
                    stream.CopyTo(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    return TextureCube.DDSFromStreamEXT(game.GraphicsDevice, memStream);
                }
            }, true);

            // SoundEffect loader
            resourceCache.RegisterFactory((stream) =>
            {
                return SoundEffect.FromStream(stream);
            }, true);

            // TODO: Video + Song?
        }

        private static Texture2D FromQoi(GraphicsDevice gd, QoiImage image)
        {
            // TODO: originally this checked for srgb and set surface format to ColorSrgbExt, but that seems to crash in some cases?
            SurfaceFormat format = SurfaceFormat.Color;

            Texture2D tex = new Texture2D(gd, image.Width, image.Height, false, format);

            if (image.Channels == QoiSharp.Codec.Channels.Rgb)
            {
                // copy to temporary Colors array first, setting alpha to 255
                Color[] colors = new Color[tex.Width * tex.Height];

                for (int i = 0; i < colors.Length; i++)
                {
                    int idx = i * 4;
                    colors[i].R = image.Data[idx];
                    colors[i].G = image.Data[idx + 1];
                    colors[i].B = image.Data[idx + 2];
                    colors[i].A = 255;
                }

                tex.SetData(colors);
            }
            else
            {
                // just copy in as-is
                tex.SetData(image.Data, 0, image.Data.Length);
            }

            return tex;
        }
    }
}
