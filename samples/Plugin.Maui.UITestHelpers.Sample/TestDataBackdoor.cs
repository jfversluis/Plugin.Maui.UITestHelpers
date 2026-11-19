#if UITEST_BACKDOOR
using Plugin.Maui.UITestHelpers.Backdoor;

namespace Plugin.Maui.UITestHelpers.Sample;

public record OrderLine(string Product, int Quantity, decimal Price);

public record Order(string Customer, List<OrderLine> Lines, DateTime OrderedAt, string? Note = null, DayOfWeek? DeliveryDay = null);

// A service from the app's dependency injection container with backdoor methods that take and
// return objects. Registered in MauiProgram.cs with AddSingleton and backdoor.AddMethods<TestDataBackdoor>().
public class TestDataBackdoor
{
	readonly List<Order> _orders = new();

	[BackdoorMethod]
	public int SeedOrders(Order[] orders)
	{
		_orders.AddRange(orders);
		return _orders.Count;
	}

	[BackdoorMethod]
	public object GetOrderSummary(string customer)
	{
		var orders = _orders.Where(o => o.Customer == customer).ToList();
		return new
		{
			Customer = customer,
			Orders = orders.Count,
			Items = orders.Sum(o => o.Lines.Sum(l => l.Quantity)),
			Total = orders.Sum(o => o.Lines.Sum(l => l.Quantity * l.Price)),
			Latest = orders.Count == 0 ? (DateTime?)null : orders.Max(o => o.OrderedAt),
			Notes = orders.Where(o => o.Note is not null).Select(o => o.Note).ToList(),
			DeliveryDays = orders.Where(o => o.DeliveryDay is not null).Select(o => o.DeliveryDay!.Value).ToList(),
		};
	}

	[BackdoorMethod]
	public void ClearOrders() => _orders.Clear();
}
#endif
