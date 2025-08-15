using Ezz_api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Ezz_api.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _db;

        public ChatService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ChatResponse> ProcessQuestionAsync(ChatRequest request)
        {
            try
            {
                var question = request.Question.ToLower();
                var response = new ChatResponse();

                // تحليل السؤال لتحديد نوع الاستعلام
                if (ContainsKeywords(question, new[] { "الأكثر مبيعاً", "أكثر مبيعاً", "الأكثر طلباً", "أفضل مبيعات" }))
                {
                    var category = ExtractCategory(question);
                    return await GetTopSellingProductsAsync(category);
                }
                else if (ContainsKeywords(question, new[] { "الأقل سعراً", "أرخص", "أقل سعر", "أقل تكلفة" }))
                {
                    var category = ExtractCategory(question);
                    return await GetLowestPriceProductsAsync(category);
                }
                else if (ContainsKeywords(question, new[] { "الأكثر كمية", "أكبر مخزون", "أكثر مخزون", "أكبر كمية" }))
                {
                    var category = ExtractCategory(question);
                    return await GetHighestStockProductsAsync(category);
                }
                else if (ContainsKeywords(question, new[] { "إحصائيات التصنيفات", "التصنيفات", "فئات المنتجات" }))
                {
                    return await GetCategoryStatisticsAsync();
                }
                else if (ContainsKeywords(question, new[] { "عدد المنتجات", "كم منتج", "إجمالي المنتجات" }))
                {
                    return await GetProductCountAsync();
                }
                else if (ContainsKeywords(question, new[] { "إجمالي المبيعات", "إجمالي الإيرادات", "المبيعات", "الإيرادات" }))
                {
                    return await GetTotalRevenueAsync();
                }
                else
                {
                    return new ChatResponse
                    {
                        Success = false,
                        Answer = "عذراً، لم أفهم سؤالك. يمكنك أن تسأل عن: الأكثر مبيعاً، الأقل سعراً، الأكثر كمية، إحصائيات التصنيفات، عدد المنتجات، أو إجمالي المبيعات.",
                        QueryType = "unknown",
                        ErrorMessage = "سؤال غير مفهوم"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء معالجة سؤالك. يرجى المحاولة مرة أخرى.",
                    QueryType = "error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetTopSellingProductsAsync(string category = "")
        {
            try
            {
                // سحب البيانات أولاً ثم التجميع على الذاكرة لتفادي مشاكل SQLite مع التجميع على decimal
                var items = await _db.OrderItems
                    .Select(oi => new { oi.ProductId, oi.ProductName, oi.Quantity, oi.TotalPrice })
                    .ToListAsync();

                if (!string.IsNullOrEmpty(category))
                {
                    var catLower = category.ToLower();
                    items = items.Where(i => (i.ProductName ?? string.Empty).ToLower().Contains(catLower)).ToList();
                }

                var topProducts = items
                    .GroupBy(i => new { i.ProductId, i.ProductName })
                    .Select(g => new ProductSalesData
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName ?? string.Empty,
                        TotalSold = g.Sum(x => x.Quantity),
                        // جمع الإيرادات عبر double ثم تحويلها إلى decimal لتفادي مشكلة SQLite
                        TotalRevenue = (decimal)g.Sum(x => (double)x.TotalPrice)
                    })
                    .OrderByDescending(p => p.TotalSold)
                    .Take(3)
                    .ToList();

                if (!topProducts.Any())
                {
                    return new ChatResponse
                    {
                        Success = true,
                        Answer = "لا توجد منتجات مبيعة حالياً.",
                        QueryType = "top_selling",
                        Data = topProducts
                    };
                }

                var answer = "أكثر المنتجات مبيعاً:\n";
                for (int i = 0; i < topProducts.Count; i++)
                {
                    var product = topProducts[i];
                    answer += $"{i + 1}. {product.ProductName} - عدد المبيعات: {product.TotalSold} - إجمالي الإيرادات: {product.TotalRevenue:F2} دينار\n";
                }

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer.Trim(),
                    QueryType = "top_selling",
                    Data = topProducts
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأكثر مبيعاً.",
                    QueryType = "top_selling",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetLowestPriceProductsAsync(string category = "")
        {
            try
            {
                Console.WriteLine("[ChatService] GetLowestPriceProductsAsync started");
                
                var query =
                    from p in _db.Products
                    join c in _db.Categories on p.CategoryId equals c.Id into gj
                    from c in gj.DefaultIfEmpty()
                    select new ProductPriceData
                    {
                        ProductId = p.Id,
                        ProductName = p.Title,
                        Price = p.Price,
                        Stock = p.Stock,
                        CategoryName = c != null ? c.Name : string.Empty
                    };

                if (!string.IsNullOrEmpty(category))
                {
                    var catLower = category.ToLower();
                    query = query.Where(p => (p.CategoryName ?? string.Empty).ToLower().Contains(catLower) ||
                                            (p.ProductName ?? string.Empty).ToLower().Contains(catLower));
                }

                Console.WriteLine("[ChatService] Fetching products to memory...");
                // Fetch to memory first, then order by price (cast to double for SQLite compatibility)
                var lowPriceProducts = await query.ToListAsync();
                Console.WriteLine($"[ChatService] Fetched {lowPriceProducts.Count} products");
                
                lowPriceProducts = lowPriceProducts
                    .OrderBy(p => (double)p.Price)
                    .Take(5)
                    .ToList();

                if (!lowPriceProducts.Any())
                {
                    return new ChatResponse
                    {
                        Success = true,
                        Answer = "لا توجد منتجات متاحة.",
                        QueryType = "lowest_price",
                        Data = lowPriceProducts
                    };
                }

                var answer = "أقل المنتجات سعراً:\n";
                for (int i = 0; i < lowPriceProducts.Count; i++)
                {
                    var product = lowPriceProducts[i];
                    answer += $"{i + 1}. {product.ProductName} - السعر: {product.Price:F2} دينار - المخزون: {product.Stock} - التصنيف: {product.CategoryName}\n";
                }

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer.Trim(),
                    QueryType = "lowest_price",
                    Data = lowPriceProducts
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatService] GetLowestPriceProductsAsync error: {ex.Message}");
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأقل سعراً.",
                    QueryType = "lowest_price",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetHighestStockProductsAsync(string category = "")
        {
            try
            {
                Console.WriteLine("[ChatService] GetHighestStockProductsAsync started");
                
                var query =
                    from p in _db.Products
                    join c in _db.Categories on p.CategoryId equals c.Id into gj
                    from c in gj.DefaultIfEmpty()
                    select new ProductPriceData
                    {
                        ProductId = p.Id,
                        ProductName = p.Title,
                        Price = p.Price,
                        Stock = p.Stock,
                        CategoryName = c != null ? c.Name : string.Empty
                    };

                if (!string.IsNullOrEmpty(category))
                {
                    var catLower = category.ToLower();
                    query = query.Where(p => (p.CategoryName ?? string.Empty).ToLower().Contains(catLower) ||
                                            (p.ProductName ?? string.Empty).ToLower().Contains(catLower));
                }

                Console.WriteLine("[ChatService] Fetching products to memory...");
                // Fetch to memory first, then order by stock
                var highStockProducts = await query.ToListAsync();
                Console.WriteLine($"[ChatService] Fetched {highStockProducts.Count} products");
                
                highStockProducts = highStockProducts
                    .OrderByDescending(p => p.Stock)
                    .Take(5)
                    .ToList();

                if (!highStockProducts.Any())
                {
                    return new ChatResponse
                    {
                        Success = true,
                        Answer = "لا توجد منتجات متاحة.",
                        QueryType = "highest_stock",
                        Data = highStockProducts
                    };
                }

                var answer = "المنتجات الأكثر مخزوناً:\n";
                for (int i = 0; i < highStockProducts.Count; i++)
                {
                    var product = highStockProducts[i];
                    answer += $"{i + 1}. {product.ProductName} - المخزون: {product.Stock} - السعر: {product.Price:F2} دينار - التصنيف: {product.CategoryName}\n";
                }

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer.Trim(),
                    QueryType = "highest_stock",
                    Data = highStockProducts
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatService] GetHighestStockProductsAsync error: {ex.Message}");
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب المنتجات الأكثر مخزوناً.",
                    QueryType = "highest_stock",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetCategoryStatisticsAsync()
        {
            try
            {
                // تحميل المنتجات (حقول بسيطة) ثم حساب الإحصائيات على الذاكرة لدعم SQLite
                var products = await _db.Products
                    .Select(p => new { p.CategoryId, Price = (double)p.Price, p.Stock })
                    .ToListAsync();

                var categories = await _db.Categories
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();

                var catMap = categories.ToDictionary(c => c.Id, c => c.Name);

                var categoryStats = products
                    .GroupBy(p => p.CategoryId)
                    .Select(g => new CategoryProductData
                    {
                        CategoryId = g.Key,
                        CategoryName = catMap.ContainsKey(g.Key) ? catMap[g.Key] : string.Empty,
                        ProductCount = g.Count(),
                        TotalValue = (decimal)g.Sum(x => x.Price * x.Stock)
                    })
                    .OrderByDescending(c => c.ProductCount)
                    .ToList();

                if (!categoryStats.Any())
                {
                    return new ChatResponse
                    {
                        Success = true,
                        Answer = "لا توجد تصنيفات متاحة.",
                        QueryType = "category_statistics",
                        Data = categoryStats
                    };
                }

                var answer = "إحصائيات التصنيفات:\n";
                foreach (var category in categoryStats)
                {
                    answer += $"• {category.CategoryName}: {category.ProductCount} منتج - إجمالي القيمة: {category.TotalValue:F2} دينار\n";
                }

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer.Trim(),
                    QueryType = "category_statistics",
                    Data = categoryStats
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatService] GetCategoryStatisticsAsync error: {ex.Message}");
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب إحصائيات التصنيفات.",
                    QueryType = "category_statistics",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetProductCountAsync()
        {
            try
            {
                var totalProducts = await _db.Products.CountAsync();
                var inStockProducts = await _db.Products.CountAsync(p => p.Stock > 0);
                var outOfStockProducts = await _db.Products.CountAsync(p => p.Stock <= 0);

                var answer = $"إجمالي عدد المنتجات: {totalProducts}\n" +
                           $"المنتجات المتوفرة: {inStockProducts}\n" +
                           $"المنتجات غير المتوفرة: {outOfStockProducts}";

                var data = new
                {
                    TotalProducts = totalProducts,
                    InStockProducts = inStockProducts,
                    OutOfStockProducts = outOfStockProducts
                };

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer,
                    QueryType = "product_count",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب عدد المنتجات.",
                    QueryType = "product_count",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ChatResponse> GetTotalRevenueAsync()
        {
            try
            {
                var totalRevenue = await _db.Orders
                    .Where(o => o.PaymentStatus == "paid")
                    .Select(o => (double)o.TotalAmount)
                    .SumAsync();

                var totalOrders = await _db.Orders
                    .Where(o => o.PaymentStatus == "paid")
                    .CountAsync();

                var answer = $"إجمالي المبيعات: {(decimal)totalRevenue:F2} دينار\n" +
                           $"إجمالي عدد الطلبات: {totalOrders}";

                var data = new
                {
                    TotalRevenue = (decimal)totalRevenue,
                    TotalOrders = totalOrders
                };

                return new ChatResponse
                {
                    Success = true,
                    Answer = answer,
                    QueryType = "total_revenue",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Success = false,
                    Answer = "حدث خطأ أثناء جلب إجمالي المبيعات.",
                    QueryType = "total_revenue",
                    ErrorMessage = ex.Message
                };
            }
        }

        private bool ContainsKeywords(string text, string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword.ToLower()));
        }

        private string ExtractCategory(string question)
        {
            // استخراج التصنيف من السؤال
            var categoryKeywords = new[] { "برفيوم", "عطور", "ملابس", "إكسسوارات", "أحذية" };
            
            foreach (var keyword in categoryKeywords)
            {
                if (question.Contains(keyword.ToLower()))
                {
                    return keyword;
                }
            }
            
            return string.Empty;
        }
    }
}

