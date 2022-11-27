using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Functions;

public class DeliveryOrderProcessor
{
    private readonly CosmosClient _cosmosClient;
    
    public DeliveryOrderProcessor(CosmosClient cosmosClient)
    {
        _cosmosClient = cosmosClient;
    }
     
    [FunctionName("DeliveryOrderProcessor")]
    public async Task RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, ILogger log)
    {
        Order order = await GetOrder(req);
        Container container = _cosmosClient.GetContainer("eshop-db", "orders");
        await container.CreateItemAsync(new
        {
            id = order.Id.ToString(),
            order
        });
    }

    private async Task<Order> GetOrder(HttpRequest req)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        Order order = JsonConvert.DeserializeObject<Order>(requestBody);
        return order;
    }
}
