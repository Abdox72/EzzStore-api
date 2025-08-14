using Ezz_api.DTOs;
using Ezz_api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace Ezz_api.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _grokApiKey;
        private readonly ILogger<ChatbotService> _logger;
        private readonly Random _random;

        public ChatbotService(ApplicationDbContext context, HttpClient httpClient, IConfiguration configuration, ILogger<ChatbotService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _grokApiKey = configuration["chatpot_apikey"]??"";
            _logger = logger;
            _random = new Random();
        }

        public async Task<ChatbotResponse> ProcessMessageAsync(ChatbotRequest request)
        {
            try
            {
                string message = request.Message;
                
                // Search for relevant products
                var relevantProducts = await SearchProductsAsync(message);
                
                // Get relevant categories
                var relevantCategories = await GetRelevantCategoriesAsync(message);
                
                // Generate AI response using Grok API
                var aiResponse = await GenerateAIResponseAsync(message, relevantProducts, request.ConversationHistory, request.UserId);
                
                // Create response with additional context
                var response = new ChatbotResponse 
                { 
                    Reply = aiResponse,
                    RelatedProducts = relevantProducts,
                    SuggestedCategories = relevantCategories,
                    IsSystemMessage = false
                };
                
                // Add additional context if needed
                if (ContainsKeywords(message, new[] { "طلب", "شحن", "دفع", "إرجاع" }))
                {
                    response.AdditionalContext = new Dictionary<string, string>();
                    
                    if (ContainsKeywords(message, new[] { "طلب", "طلبات", "طلبية", "اوردر" }))
                        response.AdditionalContext.Add("context_type", "order");
                    else if (ContainsKeywords(message, new[] { "شحن", "توصيل" }))
                        response.AdditionalContext.Add("context_type", "shipping");
                    else if (ContainsKeywords(message, new[] { "دفع", "سعر", "تكلفة" }))
                        response.AdditionalContext.Add("context_type", "payment");
                    else if (ContainsKeywords(message, new[] { "إرجاع", "استرجاع" }))
                        response.AdditionalContext.Add("context_type", "return");
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chatbot message: {Message}", request.Message);
                return new ChatbotResponse 
                { 
                    Reply = "عذراً، حدث خطأ أثناء معالجة رسالتك. يرجى المحاولة مرة أخرى لاحقاً.",
                    IsSystemMessage = true
                };
            }
        }

        public async Task<List<Product>> GetSuggestedProductsAsync(string query, int maxResults = 5)
        {
            return await SearchProductsAsync(query, maxResults);
        }
        
        public async Task<List<Category>> GetRelevantCategoriesAsync(string query, int maxResults = 3)
        {
            var searchTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var categories = await _context.Categories
                .Where(c => searchTerms.Any(term => 
                    c.Name.ToLower().Contains(term) ||
                    c.Description.ToLower().Contains(term)))
                .Take(maxResults)
                .ToListAsync();
                
            // If no direct matches, return popular categories
            if (!categories.Any())
            {
                categories = await _context.Categories
                    .Take(maxResults)
                    .ToListAsync();
            }
            
            return categories;
        }
        
        private async Task<List<Product>> SearchProductsAsync(string message, int maxResults = 8)
        {
            var searchTerms = message.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Improved search with more sophisticated term matching
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => searchTerms.Any(term => 
                    p.Title.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    p.Category.Name.ToLower().Contains(term)))
                .Take(maxResults)
                .ToListAsync();

            // If no direct matches, try fuzzy matching with partial terms
            if (!products.Any() && searchTerms.Any(t => t.Length > 3))
            {
                var longerTerms = searchTerms.Where(t => t.Length > 3)
                    .Select(t => t.Substring(0, t.Length - 1)).ToList(); // Try with partial terms
                
                products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .Where(p => longerTerms.Any(term => 
                        p.Title.ToLower().Contains(term) ||
                        p.Description.ToLower().Contains(term) ||
                        p.Category.Name.ToLower().Contains(term)))
                    .Take(maxResults / 2)
                    .ToListAsync();
            }
            
            // If still no matches, try semantic search (similar products in popular categories)
            if (!products.Any())
            {
                // Get popular categories
                var popularCategories = await _context.Categories.Take(2).ToListAsync();
                if (popularCategories.Any())
                {
                    var categoryIds = popularCategories.Select(c => c.Id).ToList();
                    products = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .Where(p => categoryIds.Contains(p.CategoryId))
                        .OrderBy(p => p.Id) // Consistent ordering
                        .Take(maxResults / 2)
                        .ToListAsync();
                }
            }

            return products;
        }

        private async Task<string> GenerateAIResponseAsync(string userMessage, List<Product> products, List<ChatMessage>? conversationHistory = null, string? userId = null)
        {
            // Get additional context information
            var systemContext = await GetSystemContextAsync(userMessage);
            var categoryInfo = await GetCategoryInfoAsync();
            
            // Format product information with more details
            var productInfo = string.Join("\n", products.Select(p => 
                $"- {p.Title}: {p.Description} - السعر: {p.Price:C} - المتوفر: {p.Stock} - الفئة: {p.Category?.Name ?? "غير محدد"}"));

            // Check if we have conversation history to provide context
            string conversationContext = "";
            if (conversationHistory != null && conversationHistory.Count > 0)
            {
                var lastMessages = conversationHistory.TakeLast(3); // Take last 3 messages for context
                conversationContext = "\nسياق المحادثة السابق:\n" + 
                    string.Join("\n", lastMessages.Select(m => 
                        $"{(m.IsUser ? "المستخدم" : "المساعد")}: {m.Content}"));
            }
            
            // Add personalization if user ID is available
            string personalization = !string.IsNullOrEmpty(userId) ? 
                "\nهذا عميل مسجل في النظام، قدم له خدمة شخصية ومميزة." : "";
                
            // Enhanced prompt with more system knowledge
            var prompt = $@"
أنت مساعد ذكي وودود لمتجر عطور ""عز ستور"" المتخصص في العطور والبخور والمسك والعود. أنت خبير في جميع منتجات المتجر وتفاصيل الطلبات والشحن والدفع. كن مثل صديق حقيقي يساعد في التسوق، استخدم لغة عربية عفوية وطبيعية كأنك تتحدث مع صديق، أضف إيموجي للدفء، أظهر تعاطفاً واهتماماً شخصياً، وأضف لمسة فكاهية أو قصة قصيرة إذا لزم الأمر. اجعل الرد يحتوي على جمل متعددة لشرح التفاصيل بشكل أفضل، واجعله يتدفق كمحادثة طبيعية مع انتقال سلس بين الأفكار.

معلومات عن المتجر:
- متجر عز ستور متخصص في العطور والبخور والمسك والعود الفاخر
- طرق الدفع المتاحة: واتساب، سترايب (بطاقة ائتمان)، باي بال
- خيارات الشحن متوفرة لجميع المناطق
- سياسة الإرجاع: يمكن إرجاع المنتجات خلال 14 يوم من الاستلام إذا كانت في حالتها الأصلية
- ساعات العمل: من الأحد إلى الخميس، 9 صباحاً - 9 مساءً
- التواصل: واتساب، بريد إلكتروني، هاتف
- الضمان: جميع المنتجات مضمونة لمدة شهر من تاريخ الشراء

فئات المنتجات المتوفرة:
{categoryInfo}

معلومات إضافية عن المنتجات:
- العود: يتميز بالرائحة القوية والثبات لفترات طويلة، مناسب للمناسبات الخاصة
- المسك: منعش ومنشط، مناسب للاستخدام اليومي
- العطور: متنوعة لتناسب جميع الأذواق والمناسبات
- البخور: يضفي أجواء روحانية وهادئة على المكان

{systemContext}
{conversationContext}
{personalization}

المستخدم سأل: ""{userMessage}""

المنتجات ذات الصلة:
{productInfo}

قدم رد طبيعي يشمل:
1. ترحيب دافئ وشخصي مع الرد على السؤال مباشرة
2. معلومات مفصلة عن المنتجات مع جمل إضافية للوصف والتوصيات
3. نصائح شخصية أو أسئلة لمواصلة الحوار
4. إنهاء بطريقة مفتوحة تشجع على المزيد من الأسئلة
5. إذا كان السؤال عن معلومات النظام مثل الطلبات أو الشحن أو الدفع، قدم معلومات دقيقة ومفصلة
6. إذا كان السؤال غير متعلق بالمتجر أو منتجاته أو خدماته، اعتذر بلطف واقترح التحدث عن المنتجات المتاحة

اجعل الرد متوسط الطول (حوالي 150-250 كلمة) ليكون مشوقاً وغير ممل، ومتنوعاً في الهيكل.";

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                model = "grok-beta",
                max_tokens = 500, // Increased token limit for more detailed responses
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_grokApiKey}");

            try
            {
                var response = await _httpClient.PostAsync("https://api.x.ai/v1/chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var grokResponse = JsonSerializer.Deserialize<GrokApiResponse>(responseContent);
                    
                    return grokResponse?.choices?.FirstOrDefault()?.message?.content ?? 
                           GenerateFallbackResponse(userMessage, products);
                }
                else
                {
                    _logger.LogWarning("Grok API returned error: {StatusCode}", response.StatusCode);
                    return GenerateFallbackResponse(userMessage, products);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Grok API");
                return GenerateFallbackResponse(userMessage, products);
            }
        }

        private string GenerateFallbackResponse(string userMessage, List<Product> products)
        {
            // Check if the message is about orders, shipping, or payment
            bool isAboutOrders = ContainsKeywords(userMessage, new[] { "طلب", "طلبات", "طلبية", "اوردر", "الطلب", "طلبي", "order" });
            bool isAboutShipping = ContainsKeywords(userMessage, new[] { "شحن", "توصيل", "الشحن", "شحنة", "shipping", "delivery" });
            bool isAboutPayment = ContainsKeywords(userMessage, new[] { "دفع", "الدفع", "فلوس", "سعر", "تكلفة", "payment", "pay", "price" });
            bool isAboutReturn = ContainsKeywords(userMessage, new[] { "إرجاع", "استرجاع", "مرتجع", "return", "refund" });
            bool isAboutContact = ContainsKeywords(userMessage, new[] { "اتصال", "تواصل", "رقم", "هاتف", "واتساب", "ايميل", "بريد", "contact", "email", "phone", "whatsapp" });
            
            if (isAboutOrders)
            {
                return "أهلاً بك في عز ستور! 🌹 بخصوص الطلبات، يمكنك متابعة طلبك من خلال صفحة 'طلباتي' في حسابك. بعد تأكيد الطلب، ستتلقى رسالة تأكيد بالبريد الإلكتروني تحتوي على رقم التتبع. يستغرق تجهيز الطلب عادةً من 1-2 يوم عمل قبل الشحن. هل تحتاج مساعدة في تتبع طلب معين أو لديك استفسار آخر عن الطلبات؟ أنا هنا لمساعدتك! 😊";
            }
            else if (isAboutShipping)
            {
                return "مرحباً بك في عز ستور! 🚚 بالنسبة للشحن، نحن نوفر خدمة توصيل لجميع المناطق. الشحن المحلي يستغرق 2-4 أيام عمل، والشحن الدولي 7-14 يوم. يمكنك تتبع شحنتك باستخدام رقم التتبع المرسل إلى بريدك الإلكتروني. هل تريد معرفة تكلفة الشحن لمنطقة معينة أو لديك أي استفسار آخر عن التوصيل؟ أخبرني وسأساعدك بكل سرور! 💫";
            }
            else if (isAboutPayment)
            {
                return "أهلاً وسهلاً بك في عز ستور! 💳 نوفر عدة طرق للدفع لراحتك: الدفع عند الاستلام، بطاقات الائتمان عبر Stripe، وكذلك PayPal وWhatsApp. جميع المعاملات مؤمنة بالكامل. إذا واجهت أي مشكلة في عملية الدفع، يمكنك التواصل معنا مباشرة وسنساعدك في إتمام عملية الشراء بسلاسة. هل لديك سؤال محدد عن إحدى طرق الدفع؟ أنا هنا لمساعدتك! 😊";
            }
            else if (isAboutReturn)
            {
                return "مرحباً بك في عز ستور! 🔄 بخصوص سياسة الإرجاع، يمكنك إرجاع أي منتج خلال 14 يوماً من تاريخ الاستلام شرط أن يكون في حالته الأصلية. لبدء عملية الإرجاع، يرجى زيارة صفحة 'طلباتي' واختيار الطلب المراد إرجاعه ثم اتباع الخطوات. سيتم رد المبلغ خلال 5-7 أيام عمل بعد استلام المنتج والتأكد من حالته. هل تحتاج مساعدة في إرجاع منتج معين؟ أخبرني وسأرشدك خطوة بخطوة! 💫";
            }
            else if (isAboutContact)
            {
                return "أهلاً بك في عز ستور! 📱 يمكنك التواصل معنا بعدة طرق: عبر الواتساب على الرقم +966-5XXXXXXXX، أو البريد الإلكتروني info@ezzstore.com، أو من خلال نموذج الاتصال في موقعنا. فريقنا متاح للرد على استفساراتك من الأحد إلى الخميس، من الساعة 9 صباحاً حتى 9 مساءً. هل يمكنني مساعدتك في أمر محدد؟ 🌹";
            }
            
            if (!products.Any())
            {
                // Get random suggestions based on categories
                var categories = _context.Categories.Take(3).ToList();
                var categoryNames = categories.Select(c => c.Name).ToList();
                var randomCategoryIndex = _random.Next(0, categoryNames.Count);
                var randomCategory = categoryNames[randomCategoryIndex];
                
                return $"يا هلا في عز ستور! 😊 آسف يا صديقي، ما لقيت منتجات تطابق طلبك بالضبط. بس خليني أقولك، عندنا تشكيلة واسعة من العطور اللي بتناسب كل الأذواق، زي {randomCategory} الفاخر ومنتجات مميزة أخرى. مثلاً، لو تبي عطر يدوم طويلاً، جرب مجموعتنا الجديدة. قولي أكثر عن اللي بدور عليه، نوع الريحة أو المناسبة، وأنا أساعدك خطوة بخطوة. شو رأيك؟";
            }

            var response = new StringBuilder();
            response.AppendLine("أهلين وسهلين في عز ستور! 😄 شكراً لسؤالك، خليني أرد عليك بالتفصيل. لقيت لك شوية منتجات رهيبة بناءً على كلامك:");
            response.AppendLine();
            
            // Show more products if available
            int productsToShow = Math.Min(products.Count, 4);
            foreach (var product in products.Take(productsToShow))
            {
                response.AppendLine($"🌟 {product.Title}: {product.Description}. ده المنتج ده مثالي لو كنت تبي شيء {GetProductBenefit(product)}, وسعره {product.Price:C} بس، وفيه {product.Stock} قطعة متوفرة. {GetProductRecommendation(product)}");
                response.AppendLine();
            }
            
            // Add personalized closing based on products shown
            if (products.Count > 0)
            {
                var category = products.FirstOrDefault()?.Category?.Name ?? "منتجاتنا";
                response.AppendLine($"بتفكر تشتري واحد من دول؟ {category} من أفضل المنتجات اللي بنقدمها، وأنا متأكد إنها هتعجبك. أو عايز أقترح لك بدائل بناءً على ميزانيتك أو تفضيلاتك؟ أنا هنا عشان أجيب على كل أسئلتك، زي الصديق اللي بيساعد في التسوق. قولي! 😉");
            }
            else
            {
                response.AppendLine("بتفكر تشتري من منتجاتنا؟ أو عايز أقترح لك منتجات بناءً على ميزانيتك أو تفضيلاتك؟ أنا هنا عشان أجيب على كل أسئلتك، زي الصديق اللي بيساعد في التسوق. قولي! 😉");
            }
            
            return response.ToString();
        }
        
        private bool ContainsKeywords(string message, string[] keywords)
        {
            if (string.IsNullOrEmpty(message)) return false;
            
            message = message.ToLower();
            return keywords.Any(keyword => message.Contains(keyword.ToLower()));
        }
        
        private string GetProductBenefit(Product product)
        {
            // Return different benefits based on product category with more variety
            if (product.Category == null)
                return "يعطيك إحساس بالانتعاش والتميز";
                
            string categoryName = product.Category.Name;
            
            // Multiple benefits per category for variety
            var benefits = new Dictionary<string, List<string>>
            {
                { "العود", new List<string> 
                    { 
                        "يعطيك إحساس بالفخامة والأصالة", 
                        "يمنحك هوية عطرية فريدة تدوم طويلاً",
                        "يعكس الذوق الرفيع والأناقة في كل مناسبة"
                    }
                },
                { "المسك", new List<string> 
                    { 
                        "يمنحك رائحة منعشة تدوم طويلاً", 
                        "يضفي لمسة من النقاء والانتعاش على يومك",
                        "يجمع بين الأصالة والرقي في عطر واحد"
                    }
                },
                { "العطور", new List<string> 
                    { 
                        "يناسب الاستخدام اليومي والمناسبات الخاصة", 
                        "يعبر عن شخصيتك المميزة بلمسة فاخرة",
                        "يمنحك ثقة وحضور طوال اليوم"
                    }
                },
                { "البخور", new List<string> 
                    { 
                        "يضفي أجواء روحانية وهدوء على المكان", 
                        "يملأ منزلك برائحة شرقية أصيلة",
                        "يساعد على الاسترخاء والراحة النفسية"
                    }
                }
            };
            
            // If category exists in our dictionary, select a random benefit
            if (benefits.ContainsKey(categoryName))
            {
                var categoryBenefits = benefits[categoryName];
                int index = product.Id % categoryBenefits.Count; // Use product ID for consistent selection
                return categoryBenefits[index];
            }
            
            return "يعطيك إحساس بالانتعاش والتميز";
        }
        
        private string GetProductRecommendation(Product product)
        {
            // Return personalized recommendations based on product with more variety
            var generalRecommendations = new[]
            {
                "أنا شخصياً جربت مشابه له وكان مذهل!",
                "هذا من أكثر المنتجات مبيعاً لدينا هذا الشهر!",
                "عملاؤنا دائماً يثنون على جودته العالية!",
                "يمكنك تجربته مع منتجاتنا الأخرى للحصول على تجربة متكاملة!",
                "أنصحك بتجربته، فهو من اختياراتي المفضلة!",
                "هذا المنتج يحظى بتقييمات ممتازة من عملائنا!",
                "أضمن لك أنك ستحب هذا المنتج من أول استخدام!",
                "هذا المنتج من الإضافات الجديدة لتشكيلتنا وقد لاقى إعجاب الكثيرين!"
            };
            
            // Category-specific recommendations
            if (product.Category != null)
            {
                var categoryRecommendations = new Dictionary<string, List<string>>
                {
                    { "العود", new List<string> 
                        { 
                            "العود من أفخم العطور الشرقية وهذا النوع بالتحديد مميز جداً!",
                            "إذا كنت تبحث عن عود أصيل يدوم طويلاً، فهذا خيار ممتاز!",
                            "هذا العود يتميز برائحته الفريدة التي تجمع بين الأصالة والعصرية!"
                        }
                    },
                    { "المسك", new List<string> 
                        { 
                            "المسك من أنقى أنواع العطور وهذا النوع تحديداً من أفضل ما قدمنا!",
                            "إذا كنت تفضل الروائح المنعشة والنقية، فهذا المسك سيناسبك تماماً!",
                            "هذا المسك مثالي للاستخدام اليومي ويمنحك إحساساً بالانتعاش طوال اليوم!"
                        }
                    },
                    { "العطور", new List<string> 
                        { 
                            "هذا العطر من أكثر العطور التي تناسب الذوق العربي الأصيل!",
                            "إذا كنت تبحث عن عطر يدوم طويلاً ويناسب جميع المناسبات، فهذا خيارك الأمثل!",
                            "هذا العطر يجمع بين النفحات الشرقية والغربية بطريقة مبتكرة ومميزة!"
                        }
                    },
                    { "البخور", new List<string> 
                        { 
                            "هذا البخور يضفي على منزلك رائحة زكية تدوم لساعات طويلة!",
                            "إذا كنت تحب الأجواء الروحانية والهادئة، فهذا البخور سيناسبك تماماً!",
                            "هذا البخور من أفضل أنواع البخور التي تناسب المنازل والمجالس!"
                        }
                    }
                };
                
                // If we have specific recommendations for this category, use them sometimes
                if (categoryRecommendations.ContainsKey(product.Category.Name) && product.Id % 3 != 0) // 2/3 chance of category-specific
                {
                    var specificRecs = categoryRecommendations[product.Category.Name];
                    int index = product.Id % specificRecs.Count;
                    return specificRecs[index];
                }
            }
            
            // Use product ID as a consistent way to select a recommendation
            int generalIndex = product.Id % generalRecommendations.Length;
            return generalRecommendations[generalIndex];
        }
        
        private async Task<string> GetSystemContextAsync(string userMessage)
        {
            var contextBuilder = new StringBuilder();
            
            // Add context about orders if the message seems to be about orders
            if (ContainsKeywords(userMessage, new[] { "طلب", "طلبات", "طلبية", "اوردر", "order" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن نظام الطلبات:
- يمكن للعملاء تتبع طلباتهم من صفحة 'طلباتي'
- حالات الطلب: قيد الانتظار، مؤكد، تم الشحن، تم التوصيل، ملغي
- حالات الدفع: قيد الانتظار، مدفوع، فشل، مسترد
- يمكن إلغاء الطلب قبل الشحن فقط
- يمكن طلب استرداد المبلغ خلال 14 يوم من الاستلام
- يمكن للعميل طلب تغيير عنوان التوصيل قبل الشحن
- يتم إرسال إشعارات عن حالة الطلب عبر البريد الإلكتروني والرسائل النصية
- يمكن الاستفسار عن حالة الطلب عبر رقم الطلب أو البريد الإلكتروني
- الحد الأدنى للطلب هو 50 دينار");
            }
            
            // Add context about shipping if the message seems to be about shipping
            if (ContainsKeywords(userMessage, new[] { "شحن", "توصيل", "الشحن", "شحنة", "shipping", "delivery" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن الشحن والتوصيل:
- الشحن المحلي يستغرق 2-4 أيام عمل
- الشحن الدولي يستغرق 7-14 يوم
- يتم توفير رقم تتبع لجميع الشحنات
- يمكن تتبع الشحنة من صفحة 'طلباتي'
- رسوم الشحن تعتمد على الوجهة والوزن
- الشحن مجاني للطلبات التي تزيد قيمتها عن 300 دينار
- يمكن اختيار توصيل سريع بتكلفة إضافية
- شركات الشحن المعتمدة: أرامكس، سمسا، زاجل، DHL
- في حالة تأخر الشحنة، يمكن التواصل مع خدمة العملاء
- يتم تغليف المنتجات بعناية لضمان وصولها بحالة ممتازة");
            }
            
            // Add context about payment if the message seems to be about payment
            if (ContainsKeywords(userMessage, new[] { "دفع", "الدفع", "فلوس", "سعر", "تكلفة", "payment", "pay", "price" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن طرق الدفع:
- الدفع عند الاستلام (متاح للشحن المحلي فقط)
- بطاقات الائتمان عبر Stripe (Visa, MasterCard, American Express)
- PayPal (متاح للشحن الدولي)
- WhatsApp (للطلبات الخاصة والمخصصة)
- تقسيط عبر تمارا (3 أقساط بدون فوائد)
- خصم 5% على الدفع المسبق
- يمكن تقسيط المبلغ على 3 أو 6 أشهر بدون فوائد لحاملي بطاقات مصرف الراجحي
- يتم إصدار الفاتورة الإلكترونية بعد إتمام عملية الدفع
- جميع الأسعار تشمل ضريبة القيمة المضافة (15%)");
            }
            
            // Add context about returns if the message seems to be about returns
            if (ContainsKeywords(userMessage, new[] { "إرجاع", "استرجاع", "مرتجع", "return", "refund" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن سياسة الإرجاع:
- يمكن إرجاع المنتجات خلال 14 يوم من تاريخ الاستلام
- يجب أن يكون المنتج في حالته الأصلية وغير مستخدم
- يتم استرداد المبلغ بنفس طريقة الدفع الأصلية
- يمكن استبدال المنتج بمنتج آخر بدلاً من استرداد المبلغ
- تكلفة الشحن للإرجاع يتحملها العميل إلا في حالة وجود عيب في المنتج
- لا يمكن إرجاع العطور المفتوحة أو المستخدمة
- يتم معالجة طلبات الإرجاع خلال 3 أيام عمل
- يمكن طلب استبدال المنتج في حال وجود عيب مصنعي");
            }
            
            // Add context about contact if the message seems to be about contact
            if (ContainsKeywords(userMessage, new[] { "اتصال", "تواصل", "رقم", "هاتف", "واتساب", "ايميل", "بريد", "contact", "email", "phone", "whatsapp" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن التواصل:
- رقم الواتساب: +966-5XXXXXXXX
- البريد الإلكتروني: info@ezzstore.com
- ساعات العمل: من الأحد إلى الخميس، 9 صباحاً - 9 مساءً
- يمكن التواصل عبر نموذج الاتصال في الموقع
- متوفر خدمة دردشة مباشرة خلال ساعات العمل
- العنوان: الرياض، المملكة العربية السعودية
- وسائل التواصل الاجتماعي: انستغرام، تويتر، فيسبوك
- خدمة العملاء متاحة على مدار الساعة عبر الواتساب");
            }
            
            // Add context about discounts and offers
            if (ContainsKeywords(userMessage, new[] { "خصم", "تخفيض", "عرض", "كوبون", "discount", "offer", "coupon", "sale" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن الخصومات والعروض:
- خصم 10% للمشتركين الجدد في النشرة البريدية
- خصم 15% على المشتريات التي تزيد عن 500 دينار
- عروض خاصة في المناسبات والأعياد
- برنامج نقاط الولاء: نقطة واحدة لكل 10 دينار، يمكن استبدالها بخصومات
- كوبونات خصم شهرية للعملاء الدائمين
- عروض حصرية للمتابعين على وسائل التواصل الاجتماعي
- خصم إضافي 5% عند الدفع المسبق
- عروض الجمعة البيضاء بخصومات تصل إلى 70%");
            }
            
            // Add context about product quality and authenticity
            if (ContainsKeywords(userMessage, new[] { "جودة", "أصلي", "تقليد", "ضمان", "quality", "authentic", "fake", "warranty" }))
            {
                contextBuilder.AppendLine(@"
معلومات عن جودة المنتجات:
- جميع منتجاتنا أصلية 100% ومضمونة
- نوفر ضمان لمدة شهر على جميع المنتجات
- يتم فحص جودة المنتجات قبل شحنها
- نستورد منتجاتنا من أفضل المصانع والموردين
- شهادات الجودة والأصالة متوفرة لجميع المنتجات
- نلتزم بمعايير الجودة العالمية في اختيار منتجاتنا
- في حال وجود أي مشكلة في المنتج، يمكن استبداله فوراً");
            }
            
            return contextBuilder.ToString();
        }
        
        private async Task<string> GetCategoryInfoAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            var result = new StringBuilder();
            
            foreach (var category in categories)
            {
                // Add product count for each category
                int productCount = await _context.Products.CountAsync(p => p.CategoryId == category.Id);
                string countText = productCount > 0 ? $" (يحتوي على {productCount} منتج)" : "";
                
                result.AppendLine($"- {category.Name}{countText}: {category.Description}");
            }
            
            return result.ToString();
        }
    }

    // Helper classes for Grok API response
    public class GrokApiResponse
    {
        public GrokChoice[]? choices { get; set; }
    }

    public class GrokChoice
    {
        public GrokMessage? message { get; set; }
    }

    public class GrokMessage
    {
        public string? content { get; set; }
    }
}