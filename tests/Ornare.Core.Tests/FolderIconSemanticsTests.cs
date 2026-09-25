using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class FolderIconSemanticsTests
{
    [Fact]
    public void SerializationRoundtripPreservesAllSemantics()
    {
        var original = new FolderIconSemantics
        {
            IconResource = @".ornare\folder.ico,0",
            IconFile = "custom.ico",
            IconIndex = 2,
            AttributesIntroduced = true,
            RawUnrelatedContent = "[ViewState]\nFolderType=Generic"
        };

        string json = original.ToJson();
        var deserialized = FolderIconSemantics.FromJson(json);

        Assert.Equal(original.IconResource, deserialized.IconResource);
        Assert.Equal(original.IconFile, deserialized.IconFile);
        Assert.Equal(original.IconIndex, deserialized.IconIndex);
        Assert.Equal(original.AttributesIntroduced, deserialized.AttributesIntroduced);
        Assert.Equal(original.RawUnrelatedContent, deserialized.RawUnrelatedContent);
        Assert.True(deserialized.HasCustomIcon);
    }

    [Fact]
    public void HasCustomIconWhenEmptyReturnsFalse()
    {
        var empty = new FolderIconSemantics();
        Assert.False(empty.HasCustomIcon);
    }
}
