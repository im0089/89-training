using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

// 建單前的請求結構層驗證：規則以清單宣告，依序 fail-fast。
// 新增驗證＝在 Rules 加一條規則，不必改動 OrderService。
public class NewOrderValidator
{
    private delegate string? Rule(Customer? customer, IReadOnlyList<NewOrderLine> lines);

    private static readonly Rule[] Rules =
    {
        (customer, _) => customer is null
            ? "找不到指定的客戶" : null,
        (_, lines) => lines is null || lines.Count == 0
            ? "訂單至少需要一項商品" : null,
        (_, lines) => lines.Any(l => l.Quantity <= 0)
            ? "商品數量必須大於 0" : null,
        (_, lines) => lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            ? "同一商品請勿重複加入，請調整數量即可" : null,
    };

    // 回傳第一個非 null 的錯誤訊息（fail-fast）；全通過回傳 null。
    public string? Validate(Customer? customer, IReadOnlyList<NewOrderLine> lines)
    {
        foreach (var rule in Rules)
        {
            var error = rule(customer, lines);
            if (error is not null)
                return error;
        }

        return null;
    }
}
