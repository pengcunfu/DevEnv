using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class ImageConversionProgressTests
{
    [Fact]
    public void ProgressPercentage_EmptyBatch_IsZero()
    {
        var progress = new ImageConversionProgress { TotalFiles = 0, ProcessedFiles = 0 };

        Assert.Equal(0d, progress.ProgressPercentage);
    }

    [Fact]
    public void ProgressPercentage_HalfDone_IsFifty()
    {
        var progress = new ImageConversionProgress { TotalFiles = 4, ProcessedFiles = 2 };

        Assert.Equal(50d, progress.ProgressPercentage);
    }

    [Fact]
    public void ProgressPercentage_AllDone_IsHundred()
    {
        var progress = new ImageConversionProgress { TotalFiles = 3, ProcessedFiles = 3 };

        Assert.Equal(100d, progress.ProgressPercentage);
    }
}