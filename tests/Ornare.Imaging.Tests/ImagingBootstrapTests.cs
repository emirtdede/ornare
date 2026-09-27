using ImageMagick;
using Xunit;

namespace Ornare.Imaging.Tests;

public class ImagingBootstrapTests
{
    [Fact]
    public void MagickNetSupportedFormatsAreAvailable()
    {
        ImageSecurityPolicy.Initialize();
        // Prove that Magick.NET initialized and can query formats on this platform
        Assert.NotEmpty(MagickNET.SupportedFormats);
    }
}
