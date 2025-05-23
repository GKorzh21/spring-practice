﻿using Core.DTOs.Filters;
using Core.DTOs;
using Core.Enums;
using Infrastructure.Interfaces;
using Core.Models.FunctionsReturnModels;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Infrastructure.Services
{
    // Сервис для работы с заказами
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;

        public OrderService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<GetOrderDTO>> GetOrdersAsync()
        {
            return await _context.orders
                .Select(o => new GetOrderDTO(o))
                .ToListAsync();
        }

        public async Task<GetOrderDTO> GetOrderAsync(int id, bool IncludeClientInfo = false)
        {
            var query = _context.orders.Where(o => o.Id == id);


            if (IncludeClientInfo)
            {
                query = query.Include(o => o.Client);
            }

            var order = await query.FirstOrDefaultAsync();

            if (order == null)
            {
                return null;
            }

            return IncludeClientInfo
                ? new GetOrderWithClientDTO(order)
                : new GetOrderDTO(order);
        }

        // Работа с заказами и вызов функций БД

        public async Task<IEnumerable<GetOrderDTO>> GetOrdersFilteredAsync(OrderFilterDTO filter, PaginationDTO pagination)
        {
            var query = _context.orders.AsQueryable();

            if (filter.cost != null)
                query = query.Where(o => o.Cost == filter.cost);
            if (filter.date != null)
                query = query.Where(o => o.Date == filter.date);
            if (filter.time != null)
                query = query.Where(o => o.Time == filter.time);
            if (filter.client_id != null)
                query = query.Where(o => o.ClientId == filter.client_id);
            if (filter.status != null && Enum.TryParse<OrderStatus>(filter.status, out var statusFilter))
                query = query.Where(o => o.Status.ToString() == filter.status);

            var page = pagination.Page;
            var pageSize = pagination.PageSize;

            var orders = await query
                .OrderBy(o => o.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new GetOrderDTO(o))
                .ToListAsync();

            return orders;
        }

        public async Task<IEnumerable<BdaySums>> GetCostsBdaysAsync()
        {
            var costs = await _context.GetCostsBdays()
                .AsNoTracking()
                .ToListAsync();

            return costs;
        }

        public async Task<IEnumerable<AvgCostsByHour>> GetAvgCostsByHourAsync()
        {
            var costs = await _context.GetAvgCostsByHour()
                .AsNoTracking()
                .ToListAsync();

            return costs;
        }

        public async Task<GetOrderDTO> PostOrderAsync(PostPutOrderDTO dto)
        {
            if (!Enum.TryParse(dto.Status, out OrderStatus status))
            {
                return null;
            }

            if (!await _context.clients.AnyAsync(c => c.Id == dto.ClientId))
                return null;

            var order = new Order
            {
                Cost = dto.Cost,
                Date = dto.Date,
                Time = dto.Time,
                ClientId = dto.ClientId,
                Status = status
            };

            _context.orders.Add(order);
            await _context.SaveChangesAsync();

            return new GetOrderDTO(order);
        }

        public async Task<int> PutOrderAsync(int id, PostPutOrderDTO order)
        {
            if (!Enum.TryParse(order.Status, out OrderStatus newStatus))
            {
                return -1;
            }

            if (!await _context.clients.AnyAsync(c => c.Id == order.ClientId))
                return -1;

            Order newOrder = new Order
            {
                Id = id,
                Cost = order.Cost,
                Date = order.Date,
                Time = order.Time,
                ClientId = order.ClientId,
                Status = newStatus
            };

            _context.Entry(newOrder).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(id))
                {
                    return -2;
                }
                else
                {
                    throw;
                }
            }

            return 0;
        }

        public async Task<bool> DeleteOrderAsync(int id)
        {
            var order = await _context.orders.FindAsync(id);
            if (order == null)
            {
                return false;
            }

            _context.orders.Remove(order);
            await _context.SaveChangesAsync();

            return true;
        }

        public bool OrderExists(int id)
        {
            return _context.orders.Any(e => e.Id == id);
        }
    }
}
