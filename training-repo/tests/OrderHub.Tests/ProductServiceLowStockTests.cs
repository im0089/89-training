using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        // 庫存剛好 = 門檻（10）的那筆必須被排除，證明過濾是 < 而非 <=。
        TestSetup.AddProduct(db, stock: 12);
        TestSetup.AddProduct(db, stock: 3);
        TestSetup.AddProduct(db, stock: 10);
        TestSetup.AddProduct(db, stock: 20);
        TestSetup.AddProduct(db, stock: 8);

        var result = await service.GetLowStockAsync(10);

        // 只回傳庫存 3 與 8，且依庫存升冪排序。
        Assert.Equal(new[] { 3, 8 }, result.Select(r => r.Product.StockQuantity).ToArray());
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        var active = TestSetup.AddProduct(db, stock: 2, isActive: true);
        TestSetup.AddProduct(db, stock: 1, isActive: false);

        var result = await service.GetLowStockAsync(10);

        // 只含上架商品，停售商品即使庫存更低也排除。
        Assert.Single(result);
        Assert.Equal(active.Id, result[0].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOld()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var orderService = TestSetup.CreateOrderService(db);

        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3);

        // 近期未取消訂單：計入。
        await orderService.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        // 近期已取消訂單：排除。
        var cancelled = (await orderService.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) })).Value!;
        cancelled.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();

        // 超過 30 天前的未取消訂單：排除。
        var old = (await orderService.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) })).Value!;
        old.CreatedAt = DateTime.UtcNow.AddDays(-40);
        await db.SaveChangesAsync();

        var result = await service.GetLowStockAsync(10);

        var row = Assert.Single(result);
        Assert.Equal(product.Id, row.Product.Id);
        // 只有近期未取消那筆的數量（1）計入。
        Assert.Equal(1, row.SoldLast30Days);
    }
}
