namespace OctopusDeployNuGetFeed.Model
{
    public interface IDownloadableNuGetPackage : INuGetPackage
    {
        long BlobSize { get; }
        byte[] GetBlob();
    }
}