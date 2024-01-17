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

        /// <summary>
        /// Retrieves information about the most recent order for a customer based on the provided request.
        /// </summary>
        /// <param name="request">An <see cref="OrderRequest"/> object containing the user's email and customer ID.</param>
        /// <returns>
        /// Returns an <see cref="IActionResult"/> representing the result of the request.
        /// If successful, returns a JSON representation of the most recent order details.
        /// If the request is invalid or encounters an error, returns an appropriate error response.
        /// </returns>
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
            catch (Exception)
            {
                // Log the exception or handle it as needed
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Checks if the provided email matches the given customer ID in the database.
        /// </summary>
        /// <param name="email">The email to be checked.</param>
        /// <param name="customerId">The customer ID to be checked against.</param>
        /// <returns>Returns true if the email matches the customer ID; otherwise, returns false.</returns>
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

        /// <summary>
        /// Retrieves the details of the most recent order for the specified customer from the database.
        /// </summary>
        /// <param name="customerId">The customer ID for whom to retrieve the most recent order.</param>
        /// <returns>
        /// Returns an <see cref="OrderDetails"/> object containing customer information, order details,
        /// and delivery information for the most recent order. If no order is found, an empty
        /// <see cref="OrderDetails"/> object is returned.
        /// </returns>
        /// 
        private OrderDetails GetMostRecentOrder(string customerId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=true;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                // SQL query to retrieve detailed information about a customer's latest order
                string query = @"
                        SELECT 
                            C.FIRSTNAME, C.LASTNAME, C.HOUSENO, C.STREET, C.TOWN, C.POSTCODE,
                            O.ORDERID, O.ORDERDATE, O.DELIVERYEXPECTED, O.CONTAINSGIFT,
                            I.PRODUCTID, P.PRODUCTNAME, I.QUANTITY, I.PRICE
                        FROM ORDERS O
                        JOIN CUSTOMERS C ON O.CUSTOMERID = C.CUSTOMERID
                        JOIN ORDERITEMS I ON O.ORDERID = I.ORDERID
                        JOIN PRODUCTS P ON I.PRODUCTID = P.PRODUCTID
                        WHERE O.CUSTOMERID = @CustomerId AND O.ORDERID = (SELECT TOP 1 ORDERID FROM ORDERS WHERE CUSTOMERID = @CustomerId ORDER BY ORDERDATE DESC)
                        ORDER BY O.ORDERDATE DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CustomerId", customerId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Create a new instance of the OrderDetails class
                            OrderDetails orderDetails = new OrderDetails
                            {
                                // Populate Customer information
                                Customer = new Customer
                                {
                                    // Retrieve and set the first name from the database
                                    FirstName = reader["FIRSTNAME"].ToString(),
                                    // Retrieve and set the last name from the database
                                    LastName = reader["LASTNAME"].ToString(),
                                },
                                // Populate Order information
                                Order = new Order
                                {
                                    // Retrieve and convert the order number from the database
                                    OrderNumber = Convert.ToInt32(reader["ORDERID"]),
                                    // Retrieve and convert the order date from the database and format it as "dd-MMM-yyyy"
                                    OrderDate = Convert.ToDateTime(reader["ORDERDATE"]).ToString("dd-MMM-yyyy"),
                                    // Build the delivery address using information from the database
                                    DeliveryAddress = $"{reader["HOUSENO"]} {reader["STREET"]}, {reader["TOWN"]}, {reader["POSTCODE"]}",
                                    // Retrieve and convert the CONTAINSGIFT field as a boolean from the database
                                    ContainsGift = Convert.ToBoolean(reader["CONTAINSGIFT"]),
                                    // Retrieve and convert the expected delivery date from the database and format it as "dd-MMM-yyyy"
                                    DeliveryExpected = Convert.ToDateTime(reader["DELIVERYEXPECTED"]).ToString("dd-MMM-yyyy"),
                                    // Initialize the OrderItems property as an empty list
                                    OrderItems = new List<OrderItem>()
                                }
                            };

                            // Loop through the result set from the SQLDataReader
                            do
                            {
                                // Create a new OrderItem and add it to the Order's list of OrderItems
                                orderDetails.Order.OrderItems.Add(new OrderItem
                                {
                                    // Determine the product name based on whether the order contains a gift
                                    Product = reader["CONTAINSGIFT"].ToString() == "True" ? "Gift" : reader["PRODUCTNAME"].ToString(),
                                    // Retrieve and convert the quantity from the database
                                    Quantity = Convert.ToInt32(reader["QUANTITY"]),
                                    // Retrieve and convert the price for each item from the database
                                    PriceEach = Convert.ToDecimal(reader["PRICE"])
                                });
                                // Continue the loop if there are more rows in the result set
                            } while (reader.Read());

                            return orderDetails;
                        }
                    }
                }
            }

            // If no order found, return empty order details
            return new OrderDetails
            {
                Customer = new Customer(),
                Order = new Order(),
            };
        }
    }
}
