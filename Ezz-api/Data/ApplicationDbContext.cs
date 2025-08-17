using Ezz_api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Ezz_api.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            //seed data
            builder.Entity<Category>().HasData(
                new Category() 
                {
                    Id =1,
                    Name = "العود",
                    Description = "أجود أنواع العود من أفضل المصادر العالمية",
                    ImageUrl = "/uploads/categories/عود.webp"
                },                
                new Category() 
                {
                    Id = 2 ,
                    Name = "المسك",
                    Description = "مسك طبيعي من أفضل المناطق",
                    ImageUrl = "/uploads/categories/مسك.png"
                },     
                new Category() 
                {
                    Id = 3 ,
                    Name = "العطور",
                    Description = "مجموعة فاخرة من العطور العالمية",
                    ImageUrl = "/uploads/categories/عطور.jpg"
                },
                new Category() 
                {
                    Id = 4 ,
                    Name = "البخور",
                    Description = "بخور عالي الجودة بروائح مميزة",
                    ImageUrl = "/uploads/categories/بخور.webp"
                }
            );

            // seed product 
            builder.Entity<Product>().HasData(
                new Product()
                {
                    Id = 1,
                    Title = "عود هندي",
                    Description = "عود هندي فاخر برائحة غنية",
                    Price = 100.00m,
                    CategoryId = 1,
                    Stock = 100,
                },
                new Product()
                {
                    Id = 2,
                    Title = "مسك أبيض",
                    Description = "مسك أبيض نقي برائحة مميزة",
                    Price = 50.00m,
                    CategoryId = 2,
                    Stock = 200,
                },
                new Product()
                {
                    Id = 3,
                    Title = "عطر فرنسي",
                    Description = "عطر فرنسي فخم برائحة جذابة",
                    Price = 200.00m,
                    CategoryId = 3,
                    Stock = 50,
                },
                new Product()
                {
                    Id = 4,
                    Title = "بخور عربي",
                    Description = "بخور عربي تقليدي بروائح عطرة",
                    Price = 30.00m,
                    CategoryId = 4,
                    Stock = 20,
                },
                //more data
                new Product()
                {
                    Id = 5,
                    Title = "بخور تركي",
                    Description = "بخور تركي أصلي بروائح مدهشه",
                    Price = 45.00m,
                    CategoryId = 4,
                    Stock = 39,
                },
                new Product()
                {
                    Id = 6,
                    Title = "عطر برازيلي",
                    Description = "عطر برازيلي فخم برائحة منعشه",
                    Price = 250.00m,
                    CategoryId = 3,
                    Stock = 303,
                }
            );
            //product images
            builder.Entity<ProductImage>().HasData(
                new ProductImage() { Id = 1, ImageUrl = "/uploads/products/عود.webp", ProductId = 1 },
                new ProductImage() { Id = 2, ImageUrl = "/uploads/products/مسك.png", ProductId = 2 },
                new ProductImage() { Id = 3, ImageUrl = "/uploads/products/عطور.jpg", ProductId = 3 },
                new ProductImage() { Id = 4, ImageUrl = "/uploads/products/بخور.webp", ProductId = 4 },
                new ProductImage() { Id = 5, ImageUrl = "/uploads/products/بخور.webp", ProductId = 5 },
                new ProductImage() { Id = 6, ImageUrl = "/uploads/products/عطور.jpg", ProductId = 6 }
            );

            // Configure Order relationships
            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

             builder.Entity<OrderItem>()
             .HasOne(oi => oi.Order)
             .WithMany(o => o.OrderItems)
             .HasForeignKey(oi => oi.OrderId)
             .OnDelete(DeleteBehavior.Cascade);




        }



    }
}