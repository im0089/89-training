using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

// NewOrderValidator 為純物件（無相依），可直接建構、免 DB 驗證每條規則。
public class NewOrderValidatorTests
{
    private static Customer AnyCustomer() => new() { Id = 1, Name = "測試客戶" };

    [Fact]
    public void Validate_ValidRequest_ReturnsNull()
    {
        var validator = new NewOrderValidator();

        var error = validator.Validate(AnyCustomer(), new[] { new NewOrderLine(10, 2) });

        Assert.Null(error);
    }

    [Fact]
    public void Validate_NullCustomer_ReturnsCustomerError()
    {
        var validator = new NewOrderValidator();

        var error = validator.Validate(null, new[] { new NewOrderLine(10, 1) });

        Assert.Equal("找不到指定的客戶", error);
    }

    [Fact]
    public void Validate_EmptyLines_ReturnsEmptyError()
    {
        var validator = new NewOrderValidator();

        var error = validator.Validate(AnyCustomer(), Array.Empty<NewOrderLine>());

        Assert.Equal("訂單至少需要一項商品", error);
    }

    [Fact]
    public void Validate_NonPositiveQuantity_ReturnsQuantityError()
    {
        var validator = new NewOrderValidator();

        var error = validator.Validate(AnyCustomer(), new[] { new NewOrderLine(10, 0) });

        Assert.Equal("商品數量必須大於 0", error);
    }

    [Fact]
    public void Validate_DuplicateProduct_ReturnsDuplicateError()
    {
        var validator = new NewOrderValidator();

        var error = validator.Validate(AnyCustomer(), new[]
        {
            new NewOrderLine(10, 1),
            new NewOrderLine(10, 2)
        });

        Assert.NotNull(error);
        Assert.Contains("重複", error);
    }

    [Fact]
    public void Validate_MultipleViolations_FailsFastOnCustomerFirst()
    {
        var validator = new NewOrderValidator();

        // 同時「客戶為 null」與「數量非法」：應短路只回傳客戶錯誤（驗證順序不變）。
        var error = validator.Validate(null, new[] { new NewOrderLine(10, 0) });

        Assert.Equal("找不到指定的客戶", error);
    }
}
