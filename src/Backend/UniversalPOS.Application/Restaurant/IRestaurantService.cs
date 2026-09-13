using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Application.Restaurant;

public interface IRestaurantService
{
    Task<IReadOnlyList<FloorDto>> GetFloorsAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<OrderDto> OpenTableAsync(long companyId, long branchId, long tableId, long waiterUserId, OpenTableRequest request, CancellationToken cancellationToken = default);
    Task<OrderDto> GetOrderAsync(long companyId, long orderId, CancellationToken cancellationToken = default);
    Task<OrderDto> AddOrderLinesAsync(long companyId, long orderId, List<AddOrderLineRequest> lines, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PreparationTicketDto>> SendToKitchenAsync(long companyId, long branchId, long orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitchenStation>> GetKitchenStationsAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreparationTicketDto>> GetKdsTicketsAsync(long companyId, long branchId, long stationId, CancellationToken cancellationToken = default);
    Task<PreparationTicketDto> UpdateTicketStatusAsync(long companyId, long ticketId, UpdateTicketStatusRequest request, CancellationToken cancellationToken = default);

    Task<SaleReceiptDto> BillOrderAsync(long companyId, long branchId, long cashierUserId, long orderId, BillOrderRequest request, CancellationToken cancellationToken = default);
}
