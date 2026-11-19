using OrderEngine.Contract;
using OrderEngine.Service;

namespace OrderEngine.Composition;

/// <summary>
/// The composition root: the one place where the object graph is built.
/// Keep this signature. Change the body as much as you like.
/// </summary>
/// 

// this will be the final file to be updated. 
public static class Bootstrap
{
    public static IOrderService Build(ExternalSystems externals) => new OrderService(externals);
}
