namespace Plugin.Maui.UITestHelpers.Core
{
	public interface ICommandExecutionGroup
	{
		bool IsCommandSupported(string commandName);
		CommandResponse Execute(string commandName, IDictionary<string, object> parameters);
	}
}