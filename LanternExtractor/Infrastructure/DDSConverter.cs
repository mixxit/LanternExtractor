using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Pfim;
namespace LanternExtractor.Infrastructure
{
    public class DDSConverter
    {
        public static Stream ConvertDds(Stream bytes)
        {
            try
            {
                using (var image = Pfim.Pfim.FromStream(bytes))
                {
                    PixelFormat format;

                    // Convert from Pfim's backend agnostic image format into GDI+'s image format
                    switch (image.Format)
                    {
                        case Pfim.ImageFormat.Rgba32:
                            format = PixelFormat.Format32bppArgb;
                            break;
                        default:
                            // see the sample for more details
                            throw new NotImplementedException();
                    }

                    // Pin pfim's data array so that it doesn't get reaped by GC, unnecessary
                    // in this snippet but useful technique if the data was going to be used in
                    // control like a picture box
                    var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                    try
                    {
                        var data = Marshal.UnsafeAddrOfPinnedArrayElement(image.Data, 0);
                        var bitmap = new Bitmap(image.Width, image.Height, image.Stride, format, data);
                        var converter = new ImageConverter();
                        return new MemoryStream((byte[])converter.ConvertTo(bitmap, typeof(byte[])));
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            } catch (ArgumentException e)
            {
                // Not targa
                return bytes;
            }

        }
    }
}
