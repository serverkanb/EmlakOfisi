namespace EmlakOfisi.Tests;

// Görev 1 iskelet testi: test projesinin katman projelerine bağlandığını doğrular.
// Görünürlük testleri görev 7'de eklenecek.
public class ProjectReferenceTests
{
    [Fact]
    public void LayerAssemblies_AreReferenced()
    {
        var dal = typeof(EmlakOfisi.DataAccessLayer.AssemblyMarker).Assembly.GetName().Name;
        var bl = typeof(EmlakOfisi.BusinessLayer.AssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("EmlakOfisi.DataAccessLayer", dal);
        Assert.Equal("EmlakOfisi.BusinessLayer", bl);
    }
}
