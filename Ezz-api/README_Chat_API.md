# Chat API Documentation

## نظرة عامة
تم إنشاء Chat API ذكي لتحليل الأسئلة الطبيعية باللغة العربية وتنفيذ استعلامات قاعدة البيانات تلقائياً.

## Endpoints

### 1. POST /api/chat/ask
**الوصف**: معالجة الأسئلة العامة عن المنتجات والمبيعات

**Request Body**:
```json
{
  "question": "ما هو أكثر برفيوم مبيعاً؟"
}
```

**Response**:
```json
{
  "success": true,
  "answer": "أكثر المنتجات مبيعاً:\n1. برفيوم فاخر - عدد المبيعات: 15 - إجمالي الإيرادات: 450.00 دينار\n2. عطر كلاسيكي - عدد المبيعات: 12 - إجمالي الإيرادات: 360.00 دينار",
  "queryType": "top_selling",
  "data": [...]
}
```

### 2. GET /api/chat/top-selling?category=برفيوم
**الوصف**: الحصول على المنتجات الأكثر مبيعاً

**Query Parameters**:
- `category` (اختياري): تصنيف المنتجات

### 3. GET /api/chat/lowest-price?category=ملابس
**الوصف**: الحصول على المنتجات الأقل سعراً

### 4. GET /api/chat/highest-stock?category=إكسسوارات
**الوصف**: الحصول على المنتجات الأكثر مخزوناً

### 5. GET /api/chat/category-statistics
**الوصف**: الحصول على إحصائيات التصنيفات

### 6. GET /api/chat/product-count
**الوصف**: الحصول على عدد المنتجات

### 7. GET /api/chat/total-revenue
**الوصف**: الحصول على إجمالي المبيعات

## أنواع الأسئلة المدعومة

### 1. المنتجات الأكثر مبيعاً
- "ما هو أكثر برفيوم مبيعاً؟"
- "أي منتج الأكثر طلباً؟"
- "أفضل مبيعات في العطور"

### 2. المنتجات الأقل سعراً
- "ما هو أرخص برفيوم؟"
- "أقل سعر في الملابس"
- "أقل تكلفة في الإكسسوارات"

### 3. المنتجات الأكثر مخزوناً
- "أي منتج الأكثر كمية؟"
- "أكبر مخزون في الأحذية"
- "أكثر مخزون في الملابس"

### 4. إحصائيات عامة
- "كم عدد المنتجات؟"
- "إحصائيات التصنيفات"
- "إجمالي المبيعات"

## أمثلة على الاستخدام

### مثال 1: سؤال عن الأكثر مبيعاً
```json
POST /api/chat/ask
{
  "question": "ما هو أكثر برفيوم مبيعاً؟"
}
```

**الرد المتوقع**:
```
أكثر المنتجات مبيعاً:
1. برفيوم فاخر - عدد المبيعات: 15 - إجمالي الإيرادات: 450.00 دينار
2. عطر كلاسيكي - عدد المبيعات: 12 - إجمالي الإيرادات: 360.00 دينار
3. عطر رومانسي - عدد المبيعات: 10 - إجمالي الإيرادات: 300.00 دينار
```

### مثال 2: سؤال عن الأقل سعراً
```json
POST /api/chat/ask
{
  "question": "أرخص منتج في الملابس"
}
```

**الرد المتوقع**:
```
أقل المنتجات سعراً:
1. قميص قطني - السعر: 25.00 دينار - المخزون: 50 - التصنيف: ملابس
2. بنطلون جينز - السعر: 35.00 دينار - المخزون: 30 - التصنيف: ملابس
3. فستان صيفي - السعر: 45.00 دينار - المخزون: 25 - التصنيف: ملابس
```

### مثال 3: سؤال عن الإحصائيات
```json
POST /api/chat/ask
{
  "question": "كم عدد المنتجات المتوفرة؟"
}
```

**الرد المتوقع**:
```
إجمالي عدد المنتجات: 150
المنتجات المتوفرة: 120
المنتجات غير المتوفرة: 30
```

## هيكل البيانات

### ChatRequest
```csharp
public class ChatRequest
{
    public string Question { get; set; }
}
```

### ChatResponse
```csharp
public class ChatResponse
{
    public string Answer { get; set; }
    public string QueryType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }
}
```

## أنواع الاستعلامات (QueryType)

- `top_selling`: المنتجات الأكثر مبيعاً
- `lowest_price`: المنتجات الأقل سعراً
- `highest_stock`: المنتجات الأكثر مخزوناً
- `category_statistics`: إحصائيات التصنيفات
- `product_count`: عدد المنتجات
- `total_revenue`: إجمالي المبيعات
- `unknown`: سؤال غير مفهوم
- `error`: خطأ في المعالجة

## إضافة أنواع أسئلة جديدة

لإضافة نوع سؤال جديد:

1. **إضافة الكلمات المفتاحية** في `ProcessQuestionAsync`:
```csharp
else if (ContainsKeywords(question, new[] { "كلمة1", "كلمة2" }))
{
    return await NewMethodAsync();
}
```

2. **إضافة الطريقة الجديدة** في `IChatService`:
```csharp
Task<ChatResponse> NewMethodAsync();
```

3. **تنفيذ الطريقة** في `ChatService`:
```csharp
public async Task<ChatResponse> NewMethodAsync()
{
    // التنفيذ
}
```

## ملاحظات تقنية

- يستخدم Entity Framework Core للوصول لقاعدة البيانات
- يدعم اللغة العربية بالكامل
- معالجة الأخطاء شاملة
- يمكن توسيعه بسهولة لإضافة أنواع أسئلة جديدة
- يستخدم LINQ للاستعلامات المعقدة
- يدعم التصنيفات كمعامل اختياري

## اختبار API

يمكن اختبار API باستخدام Swagger UI أو Postman:

1. **Swagger**: `https://localhost:7249/swagger`
2. **Postman**: إرسال POST request إلى `/api/chat/ask`
3. **cURL**: 
```bash
curl -X POST "https://localhost:7249/api/chat/ask" \
     -H "Content-Type: application/json" \
     -d '{"question": "ما هو أكثر برفيوم مبيعاً؟"}'
```

