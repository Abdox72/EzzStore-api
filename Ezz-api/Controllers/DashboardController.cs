using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ezz_api.Models;
using Ezz_api.Services;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<DashboardStatistics>> GetDashboardStatistics()
        {
            try
            {
                var statistics = await _dashboardService.GetDashboardStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("statistics/date-range")]
        public async Task<ActionResult<DashboardStatistics>> GetDashboardStatisticsByDateRange([FromBody] DateRange dateRange)
        {
            try
            {
                if (dateRange.StartDate > dateRange.EndDate)
                {
                    return BadRequest(new { message = "Start date cannot be after end date" });
                }

                var statistics = await _dashboardService.GetDashboardStatisticsByDateRangeAsync(dateRange);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("revenue-chart")]
        public async Task<ActionResult<List<RevenueChartData>>> GetRevenueChart([FromQuery] int days = 30)
        {
            try
            {
                if (days <= 0 || days > 365)
                {
                    return BadRequest(new { message = "Days must be between 1 and 365" });
                }

                var chartData = await _dashboardService.GetRevenueChartDataAsync(days);
                return Ok(chartData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("order-status-chart")]
        public async Task<ActionResult<List<OrderStatusChartData>>> GetOrderStatusChart()
        {
            try
            {
                var chartData = await _dashboardService.GetOrderStatusChartDataAsync();
                return Ok(chartData);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("top-products")]
        public async Task<ActionResult<List<TopProductData>>> GetTopProducts([FromQuery] int count = 5)
        {
            try
            {
                if (count <= 0 || count > 20)
                {
                    return BadRequest(new { message = "Count must be between 1 and 20" });
                }

                var topProducts = await _dashboardService.GetTopProductsAsync(count);
                return Ok(topProducts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
} 