using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Functions
{
    public class OrderItemsReserver
    {
        [FunctionName(nameof(OrderItemsReserver))]
        public async Task Run(
            [ServiceBusTrigger("sbq-orders", Connection = "ServiceBusConnection")] string message,
            [Blob("orders", Connection = "AzureWebJobsStorage")] BlobContainerClient container,
            ILogger log)
        {
            Order order = GetOrder(message);
            
            string name = $"{order.Id}.json";
            BlobClient blob = container.GetBlobClient(name);

            var blobOrder = new
            {
                id = order.Id,
                ShippingAddress = order.ShipToAddress,
                Items = order.OrderItems.Select(x => new {x.ItemOrdered.ProductName, x.UnitPrice, x.Units}),
                Price = order.Total(),
                BuyerId = order.BuyerId
            };

            await UploadBlob(blobOrder, blob);
        }

        private Order GetOrder(string message)
        {
            dynamic obj = JsonConvert.DeserializeObject(message);
            var orderStr = JsonConvert.SerializeObject(obj!.message.order);
            Order order = JsonConvert.DeserializeObject<Order>(orderStr);
            return order;
        }

        private async Task UploadBlob<T>(T obj, BlobClient blob)
        {
            var json = JsonConvert.SerializeObject(obj);
            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blob.UploadAsync(ms);
        }
    }
}
