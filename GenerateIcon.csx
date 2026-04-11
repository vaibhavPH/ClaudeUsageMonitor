using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

// Generate multiple sizes for .ico
var sizes = new[] { 16, 32, 48, 256 };
using var ms = new MemoryStream();
using var writer = new BinaryWriter(ms);

// ICO header
writer.Write((short)0);      // reserved
writer.Write((short)1);      // type: icon
writer.Write((short)sizes.Length); // count

var imageDataList = new List<byte[]>();

// Write directory entries (placeholder offsets)
int offset = 6 + sizes.Length * 16; // header + entries
foreach (var size in sizes)
{
    using var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    using var bgBrush = new SolidBrush(Color.FromArgb(88, 101, 242));
    g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

    var fontSize = size * 0.6f;
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    using var textBrush = new SolidBrush(Color.White);
    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    g.DrawString("C", font, textBrush, new RectangleF(0, 0, size, size), sf);

    using var pngMs = new MemoryStream();
    bmp.Save(pngMs, System.Drawing.Imaging.ImageFormat.Png);
    imageDataList.Add(pngMs.ToArray());

    var data = imageDataList[^1];
    writer.Write((byte)(size < 256 ? size : 0)); // width
    writer.Write((byte)(size < 256 ? size : 0)); // height
    writer.Write((byte)0);    // color palette
    writer.Write((byte)0);    // reserved
    writer.Write((short)1);   // color planes
    writer.Write((short)32);  // bits per pixel
    writer.Write(data.Length); // size
    writer.Write(offset);      // offset
    offset += data.Length;
}

foreach (var data in imageDataList)
    writer.Write(data);

File.WriteAllBytes("app.ico", ms.ToArray());
Console.WriteLine("Icon created: " + new FileInfo("app.ico").Length + " bytes");
