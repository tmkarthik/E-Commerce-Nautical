namespace E_Commerce.ViewModels
{
    public class Order
    {
        public int OrderNumber { get; set; }
        public string OrderDate { get; set; }
        public string DeliveryExpected { get; set; }
        public bool ContainsGift { get; set; }
        public List<OrderItem> OrderItems { get; set; }
    }
}
