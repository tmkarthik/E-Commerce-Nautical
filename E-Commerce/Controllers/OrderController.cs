using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace E_Commerce.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public OrderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        // Replace with your actual connection string

        [HttpPost]
        public IActionResult GetRecentOrder([FromBody] OrderRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.User) || string.IsNullOrEmpty(request.CustomerId))
                    return BadRequest("Invalid request. Please provide user and customerId.");

                // Check if email and customerId match
                if (!IsEmailMatchingCustomerId(request.User, request.CustomerId))
                    return BadRequest("Invalid request. Email and customerId do not match.");

                // Retrieve the most recent order
                OrderDetails orderDetails = GetMostRecentOrder(request.CustomerId);

                // Return the result as JSON
                return Ok(orderDetails);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                return StatusCode(500, "Internal Server Error");
            }
        }

        private bool IsEmailMatchingCustomerId(string email, string customerId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=true;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT 1 FROM CUSTOMERS WHERE EMAIL = @Email AND CUSTOMERID = @CustomerId";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    command.Parameters.AddWithValue("@CustomerId", customerId);
                    return command.ExecuteScalar() != null;
                }
            }
        }

        private OrderDetails GetMostRecentOrder(string customerId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=true;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
        SELECT TOP 1
            C.FIRSTNAME, C.LASTNAME, C.HOUSENO, C.STREET, C.TOWN, C.POSTCODE,  -- Add these lines
            O.ORDERID, O.ORDERDATE, O.DELIVERYEXPECTED, O.CONTAINSGIFT,
            I.PRODUCTID, P.PRODUCTNAME, I.QUANTITY, I.PRICE
        FROM ORDERS O
        JOIN CUSTOMERS C ON O.CUSTOMERID = C.CUSTOMERID
        JOIN ORDERITEMS I ON O.ORDERID = I.ORDERID
        JOIN PRODUCTS P ON I.PRODUCTID = P.PRODUCTID
        WHERE O.CUSTOMERID = @CustomerId
        ORDER BY O.ORDERDATE DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CustomerId", customerId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new OrderDetails
                            {
                                Customer = new Customer
                                {
                                    FirstName = reader["FIRSTNAME"].ToString(),
                                    LastName = reader["LASTNAME"].ToString(),
                                },
                                Order = new Order
                                {
                                    OrderNumber = Convert.ToInt32(reader["ORDERID"]),
                                    OrderDate = Convert.ToDateTime(reader["ORDERDATE"]).ToString("dd-MMM-yyyy"),
                                    DeliveryExpected = Convert.ToDateTime(reader["DELIVERYEXPECTED"]).ToString("dd-MMM-yyyy"),
                                    ContainsGift = Convert.ToBoolean(reader["CONTAINSGIFT"]),
                                    OrderItems = new List<OrderItem>
                            {
                                new OrderItem
                                {
                                    Product = reader["CONTAINSGIFT"].ToString() == "True" ? "Gift" : reader["PRODUCTNAME"].ToString(),
                                    Quantity = Convert.ToInt32(reader["QUANTITY"]),
                                    PriceEach = Convert.ToDecimal(reader["PRICE"])
                                }
                            }
                                },
                                // Add the following line for the delivery address
                                DeliveryAddress = $"{reader["HOUSENO"]} {reader["STREET"]}, {reader["TOWN"]}, {reader["POSTCODE"]}"
                            };
                        }
                    }
                }
            }

            // If no order found, return empty order details
            return new OrderDetails
            {
                Customer = new Customer(),
                Order = new Order(),
                DeliveryAddress = string.Empty
            };
        }
    }
}
