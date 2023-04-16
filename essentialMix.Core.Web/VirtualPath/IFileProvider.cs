namespace essentialMix.Core.Web.VirtualPath;

public interface IFileProvider : Microsoft.Extensions.FileProviders.IFileProvider
{
	string Alias { get; }
	string Root { get; }
}