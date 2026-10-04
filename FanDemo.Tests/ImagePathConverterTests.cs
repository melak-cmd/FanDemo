using FanDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace FanDemo.Tests;

[TestClass]
public class ImagePathConverterTests
{
    [STATestMethod]
    public void Convert_RemoteImageUrl_ReturnsBitmapImageUsingRemoteUri()
    {
        var converter = new ImagePathConverter();

        var result = converter.Convert("https://www.bing.com/th?id=OHR.Test_en-US.jpg", typeof(BitmapImage), null!, CultureInfo.InvariantCulture);

        Assert.IsInstanceOfType(result, typeof(BitmapImage));
        Assert.AreEqual("https://www.bing.com/th?id=OHR.Test_en-US.jpg", ((BitmapImage)result).UriSource.AbsoluteUri);
    }

    [TestMethod]
    public void Convert_BundledImageName_ResolvesToImagesDirectory()
    {
        var converter = new ImagePathConverter();

        var result = converter.Convert("Aquarium.jpg", typeof(string), null!, CultureInfo.InvariantCulture);

        StringAssert.Contains((string)result, "Images");
        StringAssert.EndsWith((string)result, "Aquarium.jpg");
    }
}
