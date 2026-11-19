using OrderEngine.Contract;

namespace OrderEngine.Composition;

/// <summary>
/// The composition root: the one place where the object graph is built.
/// Keep this signature. Change the body as much as you like.
/// </summary>
public static class Bootstrap
{
    public static IOrderService Build(ExternalSystems externals) => new OrderService(externals);
}
