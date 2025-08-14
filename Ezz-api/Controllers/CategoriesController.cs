using Ezz_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace Ezz_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly string _uploadsDir;

        public CategoriesController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _uploadsDir = Path.Combine(env.WebRootPath, "uploads","categories");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _db.Categories.Select(c => new { ID = c.Id , c.Name ,c.Description , Image = c.ImageUrl , ProductCount = c.Products.Count  }).ToListAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var category = await _db.Categories.Include(c => c.Products).ThenInclude(p=>p.Images)
                .FirstOrDefaultAsync(c => c.Id == id);
            return category is null ? NotFound() : Ok(category);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CategoryFormDto dto)
        {
            var category = new Category { Name = dto.Name, Description = dto.Description };
            if (dto.Image is not null)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
                var filePath = Path.Combine(_uploadsDir, fileName);
                await using var stream = System.IO.File.Create(filePath);
                await dto.Image.CopyToAsync(stream);
                category.ImageUrl = $"/uploads/categories/{fileName}";
            }
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category is null) return NotFound();
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] CategoryFormDto dto)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category is null) return NotFound();
            category.Name = dto.Name;
            category.Description = dto.Description;
            if (dto.Image is not null)
            {
                //remove the old image
                if (category.ImageUrl is not null)
                {
                    var oldFilePath = Path.Combine(_uploadsDir, category.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
                //add the new image
                // Ensure the uploads directory exists
                if (!Directory.Exists(_uploadsDir))
                {
                    Directory.CreateDirectory(_uploadsDir);
                }
                // Save the new image
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
                var filePath = Path.Combine(_uploadsDir, fileName);
                await using var stream = System.IO.File.Create(filePath);
                await dto.Image.CopyToAsync(stream);
                category.ImageUrl = $"/uploads/categories/{fileName}";
            }
            _db.Categories.Update(category);
            await _db.SaveChangesAsync();
            return NoContent();
        }

    }
    public class CategoryFormDto
    {
        [FromForm(Name = "name")]
        public required string Name { get; set; }

        [FromForm(Name = "description")]
        public string Description { get; set; } = string.Empty;

        [FromForm(Name = "image")]
        public IFormFile? Image { get; set; }
    }
}