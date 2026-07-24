using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private const int DefaultThreshold = 10;

    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(LowStockQueryViewModel query)
    {
        var vm = new LowStockListViewModel { Threshold = query.Threshold ?? DefaultThreshold };

        // threshold <= 0 由 DataAnnotations 擋下：顯示表單錯誤而非 500。
        if (!ModelState.IsValid)
            return View(vm);

        var items = await _productService.GetLowStockAsync(vm.Threshold);

        vm.Products = items.Select(i => new LowStockRowViewModel
        {
            Sku = i.Product.Sku,
            Name = i.Product.Name,
            StockQuantity = i.Product.StockQuantity,
            SoldLast30Days = i.SoldLast30Days
        }).ToList();

        return View(vm);
    }
}

