using Ezz_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly string _uploadsDir;

        public ProductsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _uploadsDir = Path.Combine(env.WebRootPath, "uploads" , "products");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _db.Products.Include(p => p.Images).Include(p => p.Category).ToListAsync());

        [HttpGet("paginated")]
        public async Task<IActionResult> GetPaginated([FromQuery] ProductFilterParameters parameters)
        {
            try
            {
                var query = _db.Products.Include(p => p.Images).Include(p => p.Category).AsQueryable();

                // Apply filters
                if (parameters.CategoryId.HasValue)
                    query = query.Where(p => p.CategoryId == parameters.CategoryId.Value);

                if (parameters.MinPrice.HasValue)
                    query = query.Where(p => p.Price >= parameters.MinPrice.Value);

                if (parameters.MaxPrice.HasValue)
                    query = query.Where(p => p.Price <= parameters.MaxPrice.Value);

                if (parameters.MinStock.HasValue)
                    query = query.Where(p => p.Stock >= parameters.MinStock.Value);

                if (parameters.MaxStock.HasValue)
                    query = query.Where(p => p.Stock <= parameters.MaxStock.Value);

                if (parameters.InStock.HasValue)
                {
                    if (parameters.InStock.Value)
                        query = query.Where(p => p.Stock > 0);
                    else
                        query = query.Where(p => p.Stock <= 0);
                }

                if (!string.IsNullOrEmpty(parameters.SearchTerm))
                {
                    var searchTerm = parameters.SearchTerm.ToLower();
                    query = query.Where(p => 
                        p.Title.ToLower().Contains(searchTerm) ||
                        p.Description.ToLower().Contains(searchTerm) ||
                        p.Category.Name.ToLower().Contains(searchTerm)
                    );
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                if (!string.IsNullOrEmpty(parameters.SortBy))
                {
                    query = parameters.SortBy.ToLower() switch
                    {
                        "price" => parameters.SortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                        "stock" => parameters.SortDescending ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
                        "name" => parameters.SortDescending ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                        "category" => parameters.SortDescending ? query.OrderByDescending(p => p.Category.Name) : query.OrderBy(p => p.Category.Name),
                        _ => parameters.SortDescending ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id)
                    };
                }
                else
                {
                    query = query.OrderBy(p => p.Id);
                }

                // Apply pagination
                var products = await query
                    .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                    .Take(parameters.PageSize)
                    .ToListAsync();

                var response = new PaginatedResponse<Product>(products, totalCount, parameters.PageNumber, parameters.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var product = await _db.Products.Include(p => p.Images).Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
            return product is null ? NotFound() : Ok(product);
        }

        [Consumes("multipart/form-data")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] ProductFormDto dto)
        {
            var product = new Product
            {
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                CategoryId = dto.CategoryId
            };
            foreach (var file in dto.Images ?? Enumerable.Empty<IFormFile>())
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(_uploadsDir, fileName);
                await using var stream = System.IO.File.Create(filePath);
                await file.CopyToAsync(stream);
                product.Images.Add(new ProductImage { ImageUrl = $"/uploads/products/{fileName}" });
            }
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product is null) return NotFound();
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [Consumes("multipart/form-data")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] ProductFormDto dto)
        {
            var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
            if (product is null) return NotFound();
            product.Title = dto.Title;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            product.CategoryId = dto.CategoryId;


            List<ProductImage> oldImgs = new List<ProductImage>();
            if(dto.Images?.Count > 0)
            {
                foreach(var img in product.Images)
                {
                    oldImgs.Add(img);
                }
                product.Images.Clear();
            }
            foreach (var file in dto.Images ?? Enumerable.Empty<IFormFile>())
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(_uploadsDir, fileName);
                await using var stream = System.IO.File.Create(filePath);
                await file.CopyToAsync(stream);
                product.Images.Add(new ProductImage { ImageUrl = $"/uploads/products/{fileName}" });
            }
            foreach (var image in oldImgs)
            {
                var _fileName = Path.GetFileName(image.ImageUrl);
                var _filePath = Path.Combine(_uploadsDir, _fileName);
                if (System.IO.File.Exists(_filePath))
                    System.IO.File.Delete(_filePath);
            }
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
    public class ProductFormDto
    {
        [FromForm(Name = "title")]
        public required string Title { get; set; }

        [FromForm(Name = "description")]
        public string Description { get; set; } = string.Empty;

        [FromForm(Name = "price")]
        public required decimal Price { get; set; }

        [FromForm(Name = "stock")]
        public required int Stock { get; set; }

        [FromForm(Name = "categoryId")]
        public required int CategoryId { get; set; }

        [FromForm(Name = "images")]
        public IList<IFormFile>? Images { get; set; }
    }
}