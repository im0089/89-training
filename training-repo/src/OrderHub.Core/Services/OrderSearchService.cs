using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class OrderSearchService : IOrderSearchService
{
    private readonly IOrderQueryTranslator _translator;
    private readonly IOrderRepository _orderRepository;

    public OrderSearchService(IOrderQueryTranslator translator, IOrderRepository orderRepository)
    {
        _translator = translator;
        _orderRepository = orderRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<Order>>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ServiceResult<IReadOnlyList<Order>>.Fail("請輸入查詢內容");

        var parsed = await _translator.TranslateAsync(query, cancellationToken);

        // 第二道防線：翻譯失敗、意圖不是查詢、或沒有任何有效條件，一律拒絕。
        // 就算翻譯器被騙，也不會變成「把整張表倒出來」。
        if (parsed is null || !parsed.HasAnyFilter)
            return ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢");

        if (parsed.DateFrom.HasValue && parsed.DateTo.HasValue && parsed.DateFrom > parsed.DateTo)
            return ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢");

        var orders = await _orderRepository.SearchAsync(parsed);
        return ServiceResult<IReadOnlyList<Order>>.Ok(orders);
    }
}
