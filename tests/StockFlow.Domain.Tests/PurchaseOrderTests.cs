using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public class PurchaseOrderTests
{
    private static readonly Guid CreatorId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static readonly Guid DecisionUserId = Guid.NewGuid();

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private static PurchaseOrder CreateOrder()
    {
        return new PurchaseOrder(
            "  po-001  ",
            Guid.NewGuid(),
            "  sup-001  ",
            "  Nova Electronics  ",
            Guid.NewGuid(),
            "  bru-01  ",
            "  Brussels Warehouse  ",
            CreatedAt,
            CreatorId);
    }

    [Fact]
    public void Constructor_WithValidInput_CreatesNormalizedDraft()
    {
        var order = CreateOrder();

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.NotEqual(Guid.Empty, order.SupplierId);
        Assert.NotEqual(Guid.Empty, order.WarehouseId);

        Assert.Equal("PO-001", order.OrderNumber);
        Assert.Equal("SUP-001", order.SupplierCode);
        Assert.Equal("Nova Electronics", order.SupplierName);
        Assert.Equal("BRU-01", order.WarehouseCode);
        Assert.Equal("Brussels Warehouse", order.WarehouseName);

        Assert.Equal("EUR", order.Currency);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(CreatedAt, order.CreatedAt);
        Assert.Equal(CreatorId, order.CreatedByUserId);
        Assert.Null(order.SubmittedAt);
        Assert.Null(order.ApprovedAt);
        Assert.Null(order.RejectedAt);
        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Fact]
    public void AddLine_WithValidInput_AddsNormalizedLineAndCalculatesTotal()
    {
        var order = CreateOrder();
        var productId = Guid.NewGuid();

        var lineId = order.AddLine(
            productId,
            "  laptop-001  ",
            "  Business Laptop  ",
            3,
            19.95m);

        var line = Assert.Single(order.Lines);

        Assert.NotEqual(Guid.Empty, lineId);
        Assert.Equal(lineId, line.Id);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal("LAPTOP-001", line.ProductSku);
        Assert.Equal("Business Laptop", line.ProductName);
        Assert.Equal(3, line.OrderedQuantity);
        Assert.Equal(19.95m, line.UnitPrice);
        Assert.Equal(59.85m, line.LineTotal);
        Assert.Equal(59.85m, order.TotalAmount);
    }

    [Fact]
    public void Submit_WithoutLines_ThrowsAndPreservesDraft()
    {
        var order = CreateOrder();

        Assert.Throws<InvalidOperationException>(
            () => order.Submit(CreatedAt.AddMinutes(1)));

        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Null(order.SubmittedAt);
        Assert.Null(order.ApprovedAt);
        Assert.Null(order.RejectedAt);
        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithNonpositiveQuantity_PreservesExistingLines(
    int quantity)
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "EXISTING", "Existing product", 2, 10m);
        var original = Assert.Single(order.Lines);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => order.AddLine(
                Guid.NewGuid(), "NEW", "New product", quantity, 5m));

        Assert.Equal("orderedQuantity", exception.ParamName);
        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Theory]
    [InlineData("-0.01", typeof(ArgumentOutOfRangeException))]
    [InlineData("-10", typeof(ArgumentOutOfRangeException))]
    [InlineData("1.001", typeof(ArgumentException))]
    [InlineData("0.009", typeof(ArgumentException))]
    public void AddLine_WithInvalidPrice_PreservesExistingLines(
        string priceText,
        Type exceptionType)
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "EXISTING", "Existing product", 2, 10m);
        var original = Assert.Single(order.Lines);

        var price = decimal.Parse(
            priceText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws(
            exceptionType,
            () => order.AddLine(
                Guid.NewGuid(), "NEW", "New product", 1, price));

        Assert.Equal(
            "unitPrice",
            Assert.IsAssignableFrom<ArgumentException>(exception).ParamName);

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("19.95")]
    [InlineData("19.950")]
    public void AddLine_WithValidPrice_PreservesAgreedValue(string priceText)
    {
        var order = CreateOrder();

        var price = decimal.Parse(
            priceText,
            System.Globalization.CultureInfo.InvariantCulture);

        order.AddLine(Guid.NewGuid(), "PRODUCT", "Product", 3, price);

        var line = Assert.Single(order.Lines);

        Assert.Equal(price, line.UnitPrice);
        Assert.Equal(3m * price, order.TotalAmount);
    }

    [Fact]
    public void AddLine_WithEmptyProductId_LeavesOrderEmpty()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<ArgumentException>(
            () => order.AddLine(Guid.Empty, "PRODUCT", "Product", 1, 10m));

        Assert.Equal("productId", exception.ParamName);
        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void AddLine_WithMissingSku_LeavesOrderEmpty(string? sku)
    {
        var order = CreateOrder();

        var exception = Assert.Throws<ArgumentException>(
            () => order.AddLine(Guid.NewGuid(), sku!, "Product", 1, 10m));

        Assert.Equal("productSku", exception.ParamName);
        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void AddLine_WithMissingName_LeavesOrderEmpty(string? name)
    {
        var order = CreateOrder();

        var exception = Assert.Throws<ArgumentException>(
            () => order.AddLine(Guid.NewGuid(), "PRODUCT", name!, 1, 10m));

        Assert.Equal("productName", exception.ParamName);
        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Fact]
    public void AddLine_WithDuplicateProduct_PreservesOriginalLine()
    {
        var order = CreateOrder();
        var productId = Guid.NewGuid();

        order.AddLine(productId, "ORIGINAL", "Original name", 2, 10m);
        var original = Assert.Single(order.Lines);

        Assert.Throws<InvalidOperationException>(
            () => order.AddLine(
                productId, "DIFFERENT", "Different name", 5, 20m));

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void AddLine_WhenLineTotalOverflows_LeavesOrderEmpty()
    {
        var order = CreateOrder();

        Assert.Throws<OverflowException>(
            () => order.AddLine(
                Guid.NewGuid(), "PRODUCT", "Product", 2, decimal.MaxValue));

        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Fact]
    public void AddLine_WhenOrderTotalOverflows_PreservesExistingLines()
    {
        var order = CreateOrder();

        order.AddLine(
            Guid.NewGuid(), "FIRST", "First product", 1, decimal.MaxValue);

        var original = Assert.Single(order.Lines);

        Assert.Throws<OverflowException>(
            () => order.AddLine(
                Guid.NewGuid(), "SECOND", "Second product", 1, 1m));

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(decimal.MaxValue, order.TotalAmount);
    }

    [Fact]
    public void UpdateLine_WithValidTerms_PreservesIdentityAndSnapshots()
    {
        var order = CreateOrder();
        var productId = Guid.NewGuid();

        var lineId = order.AddLine(
            productId, "SKU-001", "Original product", 2, 10m);

        order.AddLine(Guid.NewGuid(), "OTHER", "Other product", 1, 5m);

        var original = order.Lines[0];
        var other = order.Lines[1];

        order.UpdateLine(lineId, 3, 12.50m);

        var updated = order.Lines[0];

        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(lineId, updated.Id);
        Assert.Equal(productId, updated.ProductId);
        Assert.Equal("SKU-001", updated.ProductSku);
        Assert.Equal("Original product", updated.ProductName);
        Assert.Equal(3, updated.OrderedQuantity);
        Assert.Equal(12.50m, updated.UnitPrice);
        Assert.Equal(37.50m, updated.LineTotal);
        Assert.Equal(42.50m, order.TotalAmount);
        Assert.Same(other, order.Lines[1]);

        // Previously obtained line objects remain immutable.
        Assert.Equal(2, original.OrderedQuantity);
        Assert.Equal(10m, original.UnitPrice);
    }

    public static IEnumerable<object[]> InvalidLineUpdates()
    {
        yield return [0, 10m, typeof(ArgumentOutOfRangeException)];
        yield return [-1, 10m, typeof(ArgumentOutOfRangeException)];
        yield return [1, -0.01m, typeof(ArgumentOutOfRangeException)];
        yield return [1, 1.001m, typeof(ArgumentException)];
        yield return [2, decimal.MaxValue, typeof(OverflowException)];
    }

    [Theory]
    [MemberData(nameof(InvalidLineUpdates))]
    public void UpdateLine_WithInvalidTerms_PreservesOriginalLine(
        int quantity,
        decimal price,
        Type exceptionType)
    {
        var order = CreateOrder();
        var lineId = order.AddLine(
            Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);

        var original = Assert.Single(order.Lines);

        Assert.Throws(
            exceptionType,
            () => order.UpdateLine(lineId, quantity, price));

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateLine_WithInvalidIdentifier_PreservesOrder(bool emptyId)
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);
        var original = Assert.Single(order.Lines);

        var lineId = emptyId ? Guid.Empty : Guid.NewGuid();

        if (emptyId)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => order.UpdateLine(lineId, 3, 20m));

            Assert.Equal("lineId", exception.ParamName);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(
                () => order.UpdateLine(lineId, 3, 20m));
        }

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void UpdateLine_WhenOrderTotalOverflows_PreservesAllLines()
    {
        var order = CreateOrder();

        order.AddLine(
            Guid.NewGuid(), "FIRST", "First product", 1, decimal.MaxValue);

        var secondId = order.AddLine(
            Guid.NewGuid(), "SECOND", "Second product", 1, 0m);

        var first = order.Lines[0];
        var second = order.Lines[1];

        Assert.Throws<OverflowException>(
            () => order.UpdateLine(secondId, 1, 1m));

        Assert.Equal(2, order.Lines.Count);
        Assert.Same(first, order.Lines[0]);
        Assert.Same(second, order.Lines[1]);
        Assert.Equal(0m, second.UnitPrice);
        Assert.Equal(decimal.MaxValue, order.TotalAmount);
    }

    [Fact]
    public void RemoveLine_WithExistingIdentifier_RemovesOnlySelectedLine()
    {
        var order = CreateOrder();

        var firstId = order.AddLine(
            Guid.NewGuid(), "FIRST", "First product", 2, 10m);

        order.AddLine(
            Guid.NewGuid(), "SECOND", "Second product", 3, 5m);

        var second = order.Lines[1];

        order.RemoveLine(firstId);

        Assert.Same(second, Assert.Single(order.Lines));
        Assert.Equal(15m, order.TotalAmount);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void RemoveLine_WithLastLine_AllowsEmptyDraft()
    {
        var order = CreateOrder();

        var lineId = order.AddLine(
            Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);

        order.RemoveLine(lineId);

        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemoveLine_WithInvalidIdentifier_PreservesOrder(bool emptyId)
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);
        var original = Assert.Single(order.Lines);

        var lineId = emptyId ? Guid.Empty : Guid.NewGuid();

        if (emptyId)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => order.RemoveLine(lineId));

            Assert.Equal("lineId", exception.ParamName);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(
                () => order.RemoveLine(lineId));
        }

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void Lines_RejectsExternalCollectionModification()
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);
        var original = Assert.Single(order.Lines);

        var collection =
            Assert.IsAssignableFrom<IList<PurchaseOrderLine>>(order.Lines);

        Assert.True(collection.IsReadOnly);

        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Throws<NotSupportedException>(() => collection.Add(original));
        Assert.Throws<NotSupportedException>(() => collection.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() =>
        {
            collection[0] = original;
        });

        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void Lines_PreviouslyObtainedView_ReflectsAggregateChanges()
    {
        var order = CreateOrder();
        var view = order.Lines;

        var lineId = order.AddLine(
            Guid.NewGuid(), "PRODUCT", "Product", 1, 10m);

        Assert.Equal(lineId, Assert.Single(view).Id);

        order.UpdateLine(lineId, 2, 15m);

        Assert.Equal(30m, Assert.Single(view).LineTotal);

        order.RemoveLine(lineId);

        Assert.Empty(view);
    }

    private static PurchaseOrder CreateOrderInState(
    PurchaseOrderStatus status)
    {
        var order = CreateOrder();

        order.AddLine(
            Guid.NewGuid(), "PRODUCT", "Product", 2, 10m);

        if (status != PurchaseOrderStatus.Draft)
        {
            order.Submit(CreatedAt.AddMinutes(1));
        }

        if (status == PurchaseOrderStatus.Approved)
        {
            order.Approve(CreatedAt.AddMinutes(2), DecisionUserId);
        }
        else if (status == PurchaseOrderStatus.Rejected)
        {
            order.Reject(CreatedAt.AddMinutes(2), DecisionUserId);
        }

        return order;
    }

    private static void AssertRejectedOperationPreservesState(
        PurchaseOrder order,
        Action operation,
        Type exceptionType)
    {
        var status = order.Status;
        var submittedAt = order.SubmittedAt;
        var approvedAt = order.ApprovedAt;
        var rejectedAt = order.RejectedAt;
        var lines = order.Lines.ToArray();
        var total = order.TotalAmount;

        Assert.Throws(exceptionType, operation);

        Assert.Equal(status, order.Status);
        Assert.Equal(submittedAt, order.SubmittedAt);
        Assert.Equal(approvedAt, order.ApprovedAt);
        Assert.Equal(rejectedAt, order.RejectedAt);
        Assert.Equal(total, order.TotalAmount);
        Assert.Equal(lines.Length, order.Lines.Count);

        for (var index = 0; index < lines.Length; index++)
        {
            Assert.Same(lines[index], order.Lines[index]);
        }
    }

    [Fact]
    public void Submit_WithValidOrder_RecordsSubmissionAndPreservesLines()
    {
        var order = CreateOrderInState(PurchaseOrderStatus.Draft);
        var original = Assert.Single(order.Lines);
        var timestamp = CreatedAt.AddMinutes(1);

        order.Submit(timestamp);

        Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);
        Assert.Equal(timestamp, order.SubmittedAt);
        Assert.Null(order.ApprovedAt);
        Assert.Null(order.RejectedAt);
        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void Approve_WithPendingOrder_RecordsApprovalAndPreservesHistory()
    {
        var order = CreateOrderInState(PurchaseOrderStatus.PendingApproval);
        var original = Assert.Single(order.Lines);
        var timestamp = CreatedAt.AddMinutes(2);

        order.Approve(timestamp, DecisionUserId);

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        Assert.Equal(CreatedAt.AddMinutes(1), order.SubmittedAt);
        Assert.Equal(timestamp, order.ApprovedAt);
        Assert.Equal(DecisionUserId, order.ApprovedByUserId);
        Assert.Null(order.RejectedByUserId);
        Assert.Null(order.RejectedAt);
        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Fact]
    public void Reject_WithPendingOrder_RecordsRejectionAndPreservesHistory()
    {
        var order = CreateOrderInState(PurchaseOrderStatus.PendingApproval);
        var original = Assert.Single(order.Lines);
        var timestamp = CreatedAt.AddMinutes(2);

        order.Reject(timestamp, DecisionUserId);

        Assert.Equal(PurchaseOrderStatus.Rejected, order.Status);
        Assert.Equal(CreatedAt.AddMinutes(1), order.SubmittedAt);
        Assert.Null(order.ApprovedAt);
        Assert.Equal(timestamp, order.RejectedAt);
        Assert.Equal(DecisionUserId, order.RejectedByUserId);
        Assert.Null(order.ApprovedByUserId);
        Assert.Same(original, Assert.Single(order.Lines));
        Assert.Equal(20m, order.TotalAmount);
    }

    [Theory]
    [InlineData(true, "approvedByUserId")]
    [InlineData(false, "rejectedByUserId")]
    public void Decision_WithEmptyActorId_LeavesOrderPending(
        bool approve,
        string expectedParameter)
    {
        var order = CreateOrderInState(PurchaseOrderStatus.PendingApproval);
        var timestamp = CreatedAt.AddMinutes(2);

        Action decide = approve
            ? () => order.Approve(timestamp, Guid.Empty)
            : () => order.Reject(timestamp, Guid.Empty);

        var error = Assert.Throws<ArgumentException>(decide);

        Assert.Equal(expectedParameter, error.ParamName);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);
        Assert.Null(order.ApprovedAt);
        Assert.Null(order.ApprovedByUserId);
        Assert.Null(order.RejectedAt);
        Assert.Null(order.RejectedByUserId);
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("approve")]
    [InlineData("reject")]
    public void Transition_WithTimestampEqualToPreviousEvent_IsAllowed(
        string operation)
    {
        var order = CreateOrderInState(PurchaseOrderStatus.Draft);

        order.Submit(CreatedAt);

        if (operation == "submit")
        {
            Assert.Equal(CreatedAt, order.SubmittedAt);
            Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);
        }
        else if (operation == "approve")
        {
            order.Approve(CreatedAt, DecisionUserId);

            Assert.Equal(CreatedAt, order.ApprovedAt);
            Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        }
        else
        {
            order.Reject(CreatedAt, DecisionUserId);

            Assert.Equal(CreatedAt, order.RejectedAt);
            Assert.Equal(PurchaseOrderStatus.Rejected, order.Status);
        }
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft, "approve")]
    [InlineData(PurchaseOrderStatus.Draft, "reject")]
    [InlineData(PurchaseOrderStatus.PendingApproval, "submit")]
    [InlineData(PurchaseOrderStatus.Approved, "submit")]
    [InlineData(PurchaseOrderStatus.Approved, "approve")]
    [InlineData(PurchaseOrderStatus.Approved, "reject")]
    [InlineData(PurchaseOrderStatus.Rejected, "submit")]
    [InlineData(PurchaseOrderStatus.Rejected, "approve")]
    [InlineData(PurchaseOrderStatus.Rejected, "reject")]
    public void Transition_FromInvalidStatus_PreservesOrder(
        PurchaseOrderStatus status,
        string operation)
    {
        var order = CreateOrderInState(status);

        Action action = operation switch
        {
            "submit" => () => order.Submit(CreatedAt.AddMinutes(3)),
            "approve" => () => order.Approve(CreatedAt.AddMinutes(3), DecisionUserId),
            "reject" => () => order.Reject(CreatedAt.AddMinutes(3), DecisionUserId),
            _ => throw new ArgumentException("Unknown operation.")
        };

        AssertRejectedOperationPreservesState(
            order, action, typeof(InvalidOperationException));
    }

    public static IEnumerable<object[]> InvalidTransitionTimestamps()
    {
        foreach (var operation in new[] { "submit", "approve", "reject" })
        {
            yield return
            [
                operation,
            default(DateTimeOffset),
            typeof(ArgumentOutOfRangeException)
            ];

            yield return
            [
                operation,
            CreatedAt.AddMinutes(3).ToOffset(TimeSpan.FromHours(1)),
            typeof(ArgumentException)
            ];

            var earlierTimestamp = operation == "submit"
                ? CreatedAt.AddTicks(-1)
                : CreatedAt.AddSeconds(30);

            yield return
            [
                operation,
            earlierTimestamp,
            typeof(ArgumentOutOfRangeException)
            ];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTransitionTimestamps))]
    public void Transition_WithInvalidTimestamp_PreservesOrder(
        string operation,
        DateTimeOffset timestamp,
        Type exceptionType)
    {
        var startingStatus = operation == "submit"
            ? PurchaseOrderStatus.Draft
            : PurchaseOrderStatus.PendingApproval;

        var order = CreateOrderInState(startingStatus);

        Action action = operation switch
        {
            "submit" => () => order.Submit(timestamp),
            "approve" => () => order.Approve(timestamp, DecisionUserId),
            "reject" => () => order.Reject(timestamp, DecisionUserId),
            _ => throw new ArgumentException("Unknown operation.")
        };

        AssertRejectedOperationPreservesState(order, action, exceptionType);
    }

    public static IEnumerable<object[]> LockedLineOperations()
    {
        var statuses = new[]
        {
        PurchaseOrderStatus.PendingApproval,
        PurchaseOrderStatus.Approved,
        PurchaseOrderStatus.Rejected
    };

        foreach (var status in statuses)
        {
            foreach (var operation in new[] { "add", "update", "remove" })
            {
                yield return [status, operation];
            }
        }
    }

    [Theory]
    [MemberData(nameof(LockedLineOperations))]
    public void LineModification_AfterSubmission_PreservesOrder(
        PurchaseOrderStatus status,
        string operation)
    {
        var order = CreateOrderInState(status);
        var lineId = Assert.Single(order.Lines).Id;

        Action action = operation switch
        {
            "add" => () => order.AddLine(
                Guid.NewGuid(), "NEW", "New product", 1, 5m),
            "update" => () => order.UpdateLine(lineId, 5, 20m),
            "remove" => () => order.RemoveLine(lineId),
            _ => throw new ArgumentException("Unknown operation.")
        };

        AssertRejectedOperationPreservesState(
            order, action, typeof(InvalidOperationException));
    }

    public static IEnumerable<object?[]> MissingRequiredOrderText()
    {
        var fields = new[]
        {
        "orderNumber",
        "supplierCode",
        "supplierName",
        "warehouseCode",
        "warehouseName"
    };

        foreach (var field in fields)
        {
            foreach (var value in new string?[] { null, "", "   ", "\t\r\n" })
            {
                yield return [field, value];
            }
        }
    }

    [Theory]
    [MemberData(nameof(MissingRequiredOrderText))]
    public void Constructor_WithMissingRequiredText_ThrowsForCorrectParameter(
        string field,
        string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["orderNumber"] = "PO-001",
            ["supplierCode"] = "SUP-001",
            ["supplierName"] = "Supplier",
            ["warehouseCode"] = "BRU-01",
            ["warehouseName"] = "Warehouse"
        };

        values[field] = value;

        var exception = Assert.Throws<ArgumentException>(
            () => new PurchaseOrder(
                values["orderNumber"]!,
                Guid.NewGuid(),
                values["supplierCode"]!,
                values["supplierName"]!,
                Guid.NewGuid(),
                values["warehouseCode"]!,
                values["warehouseName"]!,
                CreatedAt,
                CreatorId));

        Assert.Equal(field, exception.ParamName);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Constructor_EnforcesNormalizedOrderNumberLength(
        int length,
        bool accepted)
    {
        // Surrounding whitespace should not count toward the limit.
        var orderNumber = $"  {new string('a', length)}  ";

        PurchaseOrder Create()
        {
            return new PurchaseOrder(
                orderNumber,
                Guid.NewGuid(),
                "SUP-001",
                "Supplier",
                Guid.NewGuid(),
                "BRU-01",
                "Warehouse",
                CreatedAt,
                CreatorId);
        }

        if (accepted)
        {
            var order = Create();

            Assert.Equal(new string('A', length), order.OrderNumber);
        }
        else
        {
            var exception = Assert.Throws<ArgumentException>(() => Create());

            Assert.Equal("orderNumber", exception.ParamName);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithEmptyReferenceIdentifier_Throws(
        bool emptySupplier)
    {
        var supplierId = emptySupplier ? Guid.Empty : Guid.NewGuid();
        var warehouseId = emptySupplier ? Guid.NewGuid() : Guid.Empty;

        var exception = Assert.Throws<ArgumentException>(
            () => new PurchaseOrder(
                "PO-001",
                supplierId,
                "SUP-001",
                "Supplier",
                warehouseId,
                "BRU-01",
                "Warehouse",
                CreatedAt,
                CreatorId));

        Assert.Equal(
            emptySupplier ? "supplierId" : "warehouseId",
            exception.ParamName);
    }

    public static IEnumerable<object[]> InvalidCreationTimestamps()
    {
        yield return
        [
            default(DateTimeOffset),
        typeof(ArgumentOutOfRangeException)
        ];

        yield return
        [
            CreatedAt.ToOffset(TimeSpan.FromHours(1)),
        typeof(ArgumentException)
        ];
    }

    [Theory]
    [MemberData(nameof(InvalidCreationTimestamps))]
    public void Constructor_WithInvalidTimestamp_Throws(
        DateTimeOffset timestamp,
        Type exceptionType)
    {
        var exception = Assert.Throws(
            exceptionType,
            () => new PurchaseOrder(
                "PO-001",
                Guid.NewGuid(),
                "SUP-001",
                "Supplier",
                Guid.NewGuid(),
                "BRU-01",
                "Warehouse",
                timestamp,
                CreatorId));

        Assert.Equal(
            "createdAt",
            Assert.IsAssignableFrom<ArgumentException>(exception).ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyCreatorId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new PurchaseOrder(
                "PO-001",
                Guid.NewGuid(),
                "SUP-001",
                "Supplier",
                Guid.NewGuid(),
                "BRU-01",
                "Warehouse",
                CreatedAt,
                Guid.Empty));

        Assert.Equal("createdByUserId", exception.ParamName);
    }

    [Fact]
    public void NewOrdersAndLines_ReceiveDistinctNonemptyIdentities()
    {
        var first = CreateOrder();
        var second = CreateOrder();
        var productId = Guid.NewGuid();

        var firstLineId = first.AddLine(
            productId, "PRODUCT", "Product", 1, 10m);

        var secondLineId = second.AddLine(
            productId, "PRODUCT", "Product", 1, 10m);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);

        Assert.NotEqual(Guid.Empty, firstLineId);
        Assert.NotEqual(Guid.Empty, secondLineId);
        Assert.NotEqual(firstLineId, secondLineId);
    }

    [Fact]
    public void ReplaceDraftLines_KeepsRetainedIdentityAndSnapshots()
    {
        var order = CreateOrder();
        var retainedProductId = Guid.NewGuid();
        var removedProductId = Guid.NewGuid();
        var addedProductId = Guid.NewGuid();

        var retainedLineId = order.AddLine(
            retainedProductId, "LAPTOP", "Original laptop", 1, 20m);

        order.AddLine(removedProductId, "DOCK", "Dock", 1, 5m);

        order.ReplaceDraftLines(
        [
            new(retainedProductId, "NEW-SKU", "Renamed laptop", 2, 22m),
        new(addedProductId, "MOUSE", "Mouse", 3, 4m)
        ]);

        Assert.Collection(
            order.Lines,
            retained =>
            {
                Assert.Equal(retainedLineId, retained.Id);
                Assert.Equal("LAPTOP", retained.ProductSku);
                Assert.Equal("Original laptop", retained.ProductName);
                Assert.Equal(2, retained.OrderedQuantity);
                Assert.Equal(22m, retained.UnitPrice);
            },
            added =>
            {
                Assert.Equal(addedProductId, added.ProductId);
                Assert.Equal("MOUSE", added.ProductSku);
                Assert.NotEqual(Guid.Empty, added.Id);
            });

        Assert.Equal(56m, order.TotalAmount);
    }

    [Fact]
    public void ReplaceDraftLines_WithInvalidFinalLine_LeavesDraftIntact()
    {
        var order = CreateOrder();
        var productId = Guid.NewGuid();

        order.AddLine(productId, "LAPTOP", "Laptop", 1, 20m);
        order.AddLine(Guid.NewGuid(), "DOCK", "Dock", 1, 5m);
        var originalLines = order.Lines.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => order.ReplaceDraftLines(
        [
            new(productId, "LAPTOP", "Laptop", 2, 22m),
        new(Guid.NewGuid(), "MOUSE", "Mouse", 0, 4m)
        ]));

        Assert.Collection(
            order.Lines,
            first => Assert.Same(originalLines[0], first),
            second => Assert.Same(originalLines[1], second));

        Assert.Equal(25m, order.TotalAmount);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void ReplaceDraftLines_WithEmptyList_ClearsDraft()
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "DOCK", "Dock", 1, 5m);

        order.ReplaceDraftLines([]);

        Assert.Empty(order.Lines);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void ReplaceDraftLines_AfterSubmission_RejectsEdit()
    {
        var order = CreateOrder();
        order.AddLine(Guid.NewGuid(), "DOCK", "Dock", 1, 5m);
        var originalLine = Assert.Single(order.Lines);
        order.Submit(CreatedAt.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => order.ReplaceDraftLines([]));

        Assert.Same(originalLine, Assert.Single(order.Lines));
        Assert.Equal(5m, order.TotalAmount);
    }
}
