using Ezz_api.Models;
using Ezz_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

namespace Ezz_api
{
    public class Program
    {
        public static async Task RoleSeed(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                // 1) Seed roles
                string[] roles = new[] { "Admin", "User" };
                foreach (var roleName in roles)
                    if (!await roleMgr.RoleExistsAsync(roleName))
                        await roleMgr.CreateAsync(new IdentityRole(roleName));

                // 2) Seed an initial admin user (optional)
                var adminEmail = "admin@ezzstore.com";
                var admin = await userMgr.FindByEmailAsync(adminEmail);
                if (admin == null)
                {
                    admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail , EmailConfirmed=true};
                    var createResult = await userMgr.CreateAsync(admin, "P@ssw0rd!");
                    if (!createResult.Succeeded) throw new Exception("Failed to create admin user");
                    await userMgr.AddToRoleAsync(admin, "Admin");
                    await userMgr.AddClaimAsync(admin, new Claim("Permission", "CanManageProducts"));
                }
            }
        }
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            //Add CORS policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowLocalhost4200", policy =>
                {
                    policy.WithOrigins("http://localhost:4200")
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                });
            });
            builder.Services.AddControllers()
                .AddJsonOptions(opts =>
                {
                    opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });            

            builder.Services.AddEndpointsApiExplorer();

            // Add HttpClient for Google token verification
            builder.Services.AddHttpClient();

            builder.Services.AddSwaggerGen(opt =>
            {
                opt.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter 'Bearer' followed by a space and your JWT token."
                });

                opt.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultProvider;
            })
              .AddEntityFrameworkStores<ApplicationDbContext>()
               .AddDefaultTokenProviders();

            // Configure token lifespan (default is 1 day)
            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromHours(2);
            });

            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
            // Register EmailService for IEmailService
            builder.Services.AddScoped<Ezz_api.Services.IEmailService, Ezz_api.Services.EmailService>();
            builder.Services.AddScoped<Ezz_api.Services.IPayPalService, Ezz_api.Services.PayPalService>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Google:ClientId"] ?? "";
                options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "";
            });

            builder.Services.AddAuthorization();

            builder.Services.AddRouting();
            builder.Services.AddScoped<IContactService, ContactService>();
            builder.Services.AddScoped<IChatbotService, ChatbotService>();

            var app = builder.Build();

            var uploadsDir = Path.Combine(app.Environment.WebRootPath, "uploads");
            var uploadsDir1 = Path.Combine(app.Environment.WebRootPath, "uploads", "Categories");
            var uploadsDir2 = Path.Combine(app.Environment.WebRootPath, "uploads", "Products");
            if(!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }
            if (!Directory.Exists(uploadsDir1))
            {
                Directory.CreateDirectory(uploadsDir1);
            }
            if (!Directory.Exists(uploadsDir2))
            {
                Directory.CreateDirectory(uploadsDir2);
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseStaticFiles();

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors("AllowLocalhost4200");

            app.MapControllers();


            await RoleSeed(app);
            app.Run();
        }
    }
}
