using System;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using MassTransit;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Messages;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IBus _bus;
    private readonly CatalogSettings _catalogSettings;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IBus bus,
        IOptions<CatalogSettings> catalogSettings)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _bus = bus;
        _catalogSettings = catalogSettings.Value;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);
        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();
        
        await SendOrderToOrderItemsReserver(order);
        await SendOrderToDeliveryOrderProcessor(order);
    }

    private async Task SendOrderToDeliveryOrderProcessor(Order order)
    {
        var client = new HttpClient();
        var orderJson = JsonConvert.SerializeObject(order);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_catalogSettings.DeliveryOrderProcessorUrl),
            Content = new StringContent(orderJson, Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        await client.SendAsync(request);
    }
    
    private async Task SendOrderToOrderItemsReserver(Order order)
    {
        var endpoint = await _bus.GetSendEndpoint(new Uri($"queue:{_catalogSettings.OrderReserverQueueName}"));
        await endpoint.Send(new OrderAccepted { Order = order });
    }
}
