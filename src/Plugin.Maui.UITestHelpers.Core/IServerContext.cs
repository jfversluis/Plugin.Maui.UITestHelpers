namespace Plugin.Maui.UITestHelpers.Core
{
	public interface IServerContext : IDisposable
	{
		IUIClientContext CreateUIClientContext(IConfig config);
	}
}
