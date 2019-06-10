﻿using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using LanternExtractor.EQ.Wld;

namespace LanternExtractor.Infrastructure
{
    /// <summary>
    /// Class which writes images to disk based on the shader type
    /// </summary>
    public static class ImageWriter
    {
        /// <summary>
        /// Writes bitmap data from memory to disk based on shader type
        /// </summary>
        /// <param name="bytes">The decompressed bitmap bytes</param>
        /// <param name="filePath">The output file path</param>
        /// <param name="fileName">The output file name</param>
        /// <param name="type">The type of shader (affects the output process)</param>
        public static void WriteImage(Stream bytes, string filePath, string fileName, ShaderType type)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            // Create the directory if it doesn't already exist
            Directory.CreateDirectory(filePath);

            if (bytes == null)
            {
                return;
            }

            var image = new Bitmap(bytes);

            Bitmap cloneBitmap;

            if (type == ShaderType.TransparentMasked)
            {
                cloneBitmap = image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format8bppIndexed);
            }
            else
            {
                cloneBitmap = image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format32bppArgb);
            }
                
            // Handle special cases
            if (type == ShaderType.TransparentMasked)
            {
                // For masked diffuse textures, the first index in the palette is the mask index.
                // We simply set it to invisible
                var palette = cloneBitmap.Palette;
        
                for (int i = 0; i < palette.Entries.Length; ++i)
                {
                    palette.Entries[0] = Color.FromArgb(0, 0, 0, 0);              
                }

                cloneBitmap.Palette = palette;
            }

            cloneBitmap.Save(filePath + fileName, ImageFormat.Png);
        }
    }
}