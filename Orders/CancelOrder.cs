namespace Valkyrie.Orders;

public class CancelOrder(
      long orderId,
      long securityId,
      string username
   )
   : OrderCore(orderId, securityId, username)
{

}