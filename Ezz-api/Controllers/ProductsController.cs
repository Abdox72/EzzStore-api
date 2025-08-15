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