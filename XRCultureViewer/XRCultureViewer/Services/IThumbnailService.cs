namespace XRCultureViewer.Services;

public interface IThumbnailService
{
    /// <summary>
    /// Enqueues a thumbnail generation job for the given model file name.
    /// </summary>
    void Enqueue(string modelFileName);
}