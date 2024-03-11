namespace Plugin.Maui.UITestHelpers.Core
{
	public interface IUIElement : IUIElementQueryable
	{
		ICommandExecution Command { get; }
	}
}
