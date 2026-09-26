namespace Ornare.PersistenceRunner;

using System;
using System.IO;
using ImageMagick;
using Ornare.Imaging;
using Ornare.Windows;

public static class Program
{
    public static void Main()
    {
        var pipeline = new ImagePipeline();
        var customizer = new FolderCustomizer();
        string folder = @"C:\Users\emir\Desktop\Ornare\OrnareUninstallPersistenceTest";
        string tempIco = Path.Combine(Path.GetTempPath(), "persistence_test.ico");
        using (var img = new MagickImage(new MagickColor(46, 204, 113), 256, 256))
        {
            byte[] bytes = img.ToByteArray(MagickFormat.Png);
            pipeline.ProcessImageBytes(bytes, tempIco);
        }
        var result = customizer.ApplyIcon(folder, tempIco);
        Console.WriteLine("ApplyIcon IsSuccess: " + result.IsSuccess);
    }
}
