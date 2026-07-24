using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

public record LowStockProduct(Product Product, int SoldLast30Days);
