namespace Plugin.Maui.UITestHelpers.Core
{
	public interface ICommandExecution
	{
		CommandResponse Execute(string commandName, IDictionary<string, object> parameters);
	}
}