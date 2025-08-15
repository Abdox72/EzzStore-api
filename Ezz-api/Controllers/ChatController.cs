using Microsoft.AspNetCore.Mvc;
using Ezz_api.Models;
using Ezz_api.Services;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// معالجة الأسئلة العامة عن المنتجات والمبيعات
        /// </summary>
        /// <param name="request">طلب يحتوي على نص السؤال</param>
        /// <returns>إجابة ذكية بناءً على نوع السؤال</returns>
        [HttpPost("ask")]
        public async Task<ActionResult<ChatResponse>> AskQuestion([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Question))
                {
                    return BadRequest(new ChatResponse
                    {
                        Success = false,
                        Answer = "نص السؤال مطلوب",
                        QueryType = "validation_error",
                        ErrorMessage = "Question is required"
                    });
                }

                var response = await _chatService.ProcessQuestionAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ داخلي في الخادم",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على المنتجات الأكثر مبيعاً
        /// </summary>
        /// <param name="category">التصنيف (اختياري)</param>
        /// <returns>قائمة المنتجات الأكثر مبيعاً</returns>
        [HttpGet("top-selling")]
        public async Task<ActionResult<ChatResponse>> GetTopSellingProducts([FromQuery] string? category = "")
        {
            try
            {
                var response = await _chatService.GetTopSellingProductsAsync(category ?? "");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأكثر مبيعاً",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على المنتجات الأقل سعراً
        /// </summary>
        /// <param name="category">التصنيف (اختياري)</param>
        /// <returns>قائمة المنتجات الأقل سعراً</returns>
        [HttpGet("lowest-price")]
        public async Task<ActionResult<ChatResponse>> GetLowestPriceProducts([FromQuery] string? category = "")
        {
            try
            {
                var response = await _chatService.GetLowestPriceProductsAsync(category ?? "");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأقل سعراً",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على المنتجات الأكثر مخزوناً
        /// </summary>
        /// <param name="category">التصنيف (اختياري)</param>
        /// <returns>قائمة المنتجات الأكثر مخزوناً</returns>
        [HttpGet("highest-stock")]
        public async Task<ActionResult<ChatResponse>> GetHighestStockProducts([FromQuery] string? category = "")
        {
            try
            {
                var response = await _chatService.GetHighestStockProductsAsync(category ?? "");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأكثر مخزوناً",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على إحصائيات التصنيفات
        /// </summary>
        /// <returns>إحصائيات مفصلة عن التصنيفات</returns>
        [HttpGet("category-statistics")]
        public async Task<ActionResult<ChatResponse>> GetCategoryStatistics()
        {
            try
            {
                var response = await _chatService.GetCategoryStatisticsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب إحصائيات التصنيفات",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على عدد المنتجات
        /// </summary>
        /// <returns>إحصائيات عدد المنتجات</returns>
        [HttpGet("product-count")]
        public async Task<ActionResult<ChatResponse>> GetProductCount()
        {
            try
            {
                var response = await _chatService.GetProductCountAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب عدد المنتجات",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على إجمالي المبيعات
        /// </summary>
        /// <returns>إحصائيات المبيعات والإيرادات</returns>
        [HttpGet("total-revenue")]
        public async Task<ActionResult<ChatResponse>> GetTotalRevenue()
        {
            try
            {
                var response = await _chatService.GetTotalRevenueAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب إجمالي المبيعات",
                    QueryType = "server_error",
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}

