using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Domain.Entities;

namespace SRECommerce.Infrastructure.Data;
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string demoPassword)
    {
        if (string.IsNullOrWhiteSpace(demoPassword) || demoPassword.Length < 12)
            throw new ArgumentException("A development-only demo password of at least 12 characters is required.", nameof(demoPassword));

        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin))
        {
            var admin = new User
            {
                FullName = "Super Admin",
                Email = "admin@srecommerce.local",
                Role = Roles.Admin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword)
            };

            db.Users.Add(admin);
        }

        if (!await db.Users.AnyAsync(u => u.Email == "customer@srecommerce.local"))
        {
            db.Users.Add(new User
            {
                FullName = "Demo Customer",
                Email = "customer@srecommerce.local",
                Role = Roles.Customer,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword)
            });
        }

        if (!await db.Users.AnyAsync(u => u.Email == "employee@srecommerce.local"))
        {
            db.Users.Add(new User
            {
                FullName = "Store Employee",
                Email = "employee@srecommerce.local",
                Role = Roles.Employee,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword)
            });
        }

        var desiredCategories = new List<Category>
        {
            new Category { Name = "Mobiles", Slug = "mobiles", ImageUrl = "/assets/Images/categories/mobiles.jpg" },
            new Category { Name = "Laptops", Slug = "laptops", ImageUrl = "/assets/Images/categories/laptops.jpg" },
            new Category { Name = "Fashion", Slug = "fashion", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Beauty", Slug = "beauty", ImageUrl = "/assets/Images/categories/beauty.jpg" },
            new Category { Name = "Home Decor", Slug = "home-decor", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Accessories", Slug = "accessories", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Kitchen", Slug = "kitchen", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Toys", Slug = "toys", ImageUrl = "/assets/Images/categories/beauty.jpg" },
            new Category { Name = "Baby Care", Slug = "baby-care", ImageUrl = "/assets/Images/categories/beauty.jpg" },
            new Category { Name = "Grocery", Slug = "grocery", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Sports", Slug = "sports", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Books", Slug = "books", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Footwear", Slug = "footwear", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Watches", Slug = "watches", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Bags", Slug = "bags", ImageUrl = "/assets/Images/categories/fashion.jpg" },
            new Category { Name = "Furniture", Slug = "furniture", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Appliances", Slug = "appliances", ImageUrl = "/assets/Images/categories/laptops.jpg" },
            new Category { Name = "Health", Slug = "health", ImageUrl = "/assets/Images/categories/beauty.jpg" },
            new Category { Name = "Stationery", Slug = "stationery", ImageUrl = "/assets/Images/categories/home-decor.jpg" },
            new Category { Name = "Automotive", Slug = "automotive", ImageUrl = "/assets/Images/categories/mobiles.jpg" }
        };

        foreach (var desired in desiredCategories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Slug == desired.Slug);
            if (existing is null)
            {
                db.Categories.Add(desired);
            }
            else if (string.IsNullOrWhiteSpace(existing.ImageUrl))
            {
                existing.ImageUrl = desired.ImageUrl;
            }
        }

        await db.SaveChangesAsync();

        if (!await db.Banners.AnyAsync())
        {
            db.Banners.AddRange(
                new Banner
                {
                    Title = "Mobiles Sale",
                    ImageUrl = "/assets/Images/categories/mobiles.jpg",
                    LinkType = BannerLinkType.Category,
                    LinkValue = "mobiles",
                    SortOrder = 1
                },
                new Banner
                {
                    Title = "Laptop Deals",
                    ImageUrl = "/assets/Images/categories/laptops.jpg",
                    LinkType = BannerLinkType.Category,
                    LinkValue = "laptops",
                    SortOrder = 2
                },
                new Banner
                {
                    Title = "Fashion Picks",
                    ImageUrl = "/assets/Images/categories/fashion.jpg",
                    LinkType = BannerLinkType.Category,
                    LinkValue = "fashion",
                    SortOrder = 3
                }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.HomeSections.AnyAsync())
        {
            db.HomeSections.AddRange(
                new HomeSection
                {
                    Type = (int)HomeSectionType.CategoriesGrid,
                    Title = "Shop by Category",
                    SortOrder = 1,
                    ItemsToShow = 5,
                    CardsPerRow = 5,
                    IsEnabled = true
                },
                new HomeSection
                {
                    Type = (int)HomeSectionType.BannerSlider,
                    Title = "Featured Banners",
                    SortOrder = 2,
                    ItemsToShow = 3,
                    CardsPerRow = 1,
                    IsEnabled = true
                },
                new HomeSection
                {
                    Type = (int)HomeSectionType.ProductsGrid,
                    Title = "Top Picks",
                    SortOrder = 3,
                    ItemsToShow = 6,
                    CardsPerRow = 3,
                    IsEnabled = true
                }
            );
            await db.SaveChangesAsync();
        }

        var sections = await db.HomeSections
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        AddMissingHomeSection("hero-banners", (int)HomeSectionType.BannerSlider, "Baby Pink Mega Deals", 1, 3, 1,
            "{\"variant\":\"hero\",\"tone\":\"pink\"}");
        AddMissingHomeSection("categories", (int)HomeSectionType.CategoriesGrid, "Shop Popular Departments", 2, 12, 6,
            "{\"variant\":\"tiles\",\"tone\":\"soft\"}");
        AddMissingHomeSection("new-arrivals", (int)HomeSectionType.ProductsGrid, "New Arrivals", 3, 24, 6,
            "{\"variant\":\"dense\",\"tone\":\"white\"}");
        AddMissingHomeSection("deals", (int)HomeSectionType.ProductsGrid, "Pink Friday Deals", 4, 24, 6,
            "{\"variant\":\"sale\",\"tone\":\"pink\"}");
        AddMissingHomeSection("mobiles", (int)HomeSectionType.ProductsGrid, "Mobiles and Smart Tech", 5, 18, 6,
            "{\"variant\":\"compact\",\"tone\":\"white\"}");
        AddMissingHomeSection("fashion", (int)HomeSectionType.ProductsGrid, "Fashion, Beauty and Lifestyle", 6, 18, 6,
            "{\"variant\":\"compact\",\"tone\":\"blush\"}");
        AddMissingHomeSection("home-decor", (int)HomeSectionType.ProductsGrid, "Home, Kitchen and Decor", 7, 18, 6,
            "{\"variant\":\"compact\",\"tone\":\"white\"}");

        await db.SaveChangesAsync();

        sections = await db.HomeSections
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        void AddMissingHomeSection(string sourceKey, int type, string title, int sortOrder, int itemsToShow, int cardsPerRow, string styleJson)
        {
            var existing = sections.FirstOrDefault(s => s.SourceKey == sourceKey || s.Title == title);
            if (existing is null)
            {
                db.HomeSections.Add(new HomeSection
                {
                    Type = type,
                    Title = title,
                    SortOrder = sortOrder,
                    ItemsToShow = itemsToShow,
                    CardsPerRow = cardsPerRow,
                    SourceKey = sourceKey,
                    StyleJson = styleJson,
                    IsEnabled = true
                });
                return;
            }

            existing.SourceKey ??= sourceKey;
            existing.StyleJson ??= styleJson;
            existing.ItemsToShow = Math.Max(existing.ItemsToShow, itemsToShow);
            existing.CardsPerRow = Math.Max(existing.CardsPerRow, cardsPerRow);
        }

        if (!await db.HomeSectionItems.AnyAsync(i => i.EntityType != HomeEntityType.Product) && sections.Count > 0)
        {
            var categorySection = sections.FirstOrDefault(s => s.Type == (int)HomeSectionType.CategoriesGrid);
            var bannerSection = sections.FirstOrDefault(s => s.Type == (int)HomeSectionType.BannerSlider);

            if (categorySection != null)
            {
                var categoryIds = await db.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => c.Id)
                    .Take(6)
                    .ToListAsync();

                var order = 1;
                foreach (var id in categoryIds)
                {
                    db.HomeSectionItems.Add(new HomeSectionItem
                    {
                        HomeSectionId = categorySection.Id,
                        EntityType = HomeEntityType.Category,
                        EntityId = id,
                        SortOrder = order++,
                        IsEnabled = true
                    });
                }
            }

            if (bannerSection != null)
            {
                var bannerIds = await db.Banners
                    .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                    .Select(b => b.Id)
                    .Take(5)
                    .ToListAsync();

                var order = 1;
                foreach (var id in bannerIds)
                {
                    db.HomeSectionItems.Add(new HomeSectionItem
                    {
                        HomeSectionId = bannerSection.Id,
                        EntityType = HomeEntityType.Banner,
                        EntityId = id,
                        SortOrder = order++,
                        IsEnabled = true
                    });
                }
            }

            await db.SaveChangesAsync();
        }
        var categoryMap = await db.Categories
            .ToDictionaryAsync(c => c.Slug, c => c.Id);

        if (!await db.Products.AnyAsync())
        {
            if (categoryMap.TryGetValue("mobiles", out var catId))
            {
                db.Products.AddRange(
                    new Product { Name = "SR Phone X", Slug = "sr-phone-x", Sku = "SRX-001", Price = 39999, StockQty = 20, CategoryId = catId, ImageUrl = "/assets/Images/categories/mobiles.jpg" },
                    new Product { Name = "SR Phone Lite", Slug = "sr-phone-lite", Sku = "SRL-002", Price = 19999, StockQty = 50, CategoryId = catId, ImageUrl = "/assets/Images/categories/mobiles.jpg" }
                );
                await db.SaveChangesAsync();
            }
        }

        var desiredProducts = new List<(string Name, string Slug, string Sku, decimal Price, decimal Discount, int Stock, string CategorySlug, string ImageUrl)>
        {
            ("UltraBook Air 14", "ultrabook-air-14", "UBA14", 54999, 2500, 17, "laptops", "/assets/Images/categories/laptops.jpg"),
            ("CreatorBook Pro 16", "creatorbook-pro-16", "CBP16", 84999, 4000, 8, "laptops", "/assets/Images/categories/laptops.jpg"),
            ("Student Laptop Go", "student-laptop-go", "SLG13", 35999, 1500, 22, "laptops", "/assets/Images/categories/laptops.jpg"),
            ("Gaming Laptop RTX", "gaming-laptop-rtx", "GLRTX", 119999, 7000, 5, "laptops", "/assets/Images/categories/laptops.jpg"),
            ("Nova X1 Phone", "nova-x1-phone", "NVX1", 19999, 1000, 42, "mobiles", "/assets/Images/categories/mobiles.jpg"),
            ("Nova X1 Pro Phone", "nova-x1-pro-phone", "NVX1P", 29999, 1500, 28, "mobiles", "/assets/Images/categories/mobiles.jpg"),
            ("SR Phone Max", "sr-phone-max", "SRMAX", 45999, 3000, 12, "mobiles", "/assets/Images/categories/mobiles.jpg"),
            ("Budget Smart 5G", "budget-smart-5g", "BS5G", 13999, 700, 55, "mobiles", "/assets/Images/categories/mobiles.jpg"),
            ("Wireless Earbuds Plus", "wireless-earbuds-plus", "WEP01", 2499, 300, 90, "accessories", "/assets/Images/categories/fashion.jpg"),
            ("Smart Watch Active", "smart-watch-active", "SWA01", 3999, 500, 35, "accessories", "/assets/Images/categories/fashion.jpg"),
            ("Fast Charger 65W", "fast-charger-65w", "FC65", 1499, 150, 75, "accessories", "/assets/Images/categories/mobiles.jpg"),
            ("Laptop Backpack", "laptop-backpack", "LBP01", 2199, 250, 40, "accessories", "/assets/Images/categories/fashion.jpg"),
            ("Fashion Set", "fashion-set", "FASH01", 2499, 300, 44, "fashion", "/assets/Images/categories/fashion.jpg"),
            ("Classic Denim Jacket", "classic-denim-jacket", "CDJ01", 3499, 400, 26, "fashion", "/assets/Images/categories/fashion.jpg"),
            ("Everyday Sneakers", "everyday-sneakers", "SHOE01", 2999, 350, 31, "fashion", "/assets/Images/categories/fashion.jpg"),
            ("Formal Shirt Pack", "formal-shirt-pack", "FSP02", 1899, 200, 48, "fashion", "/assets/Images/categories/fashion.jpg"),
            ("Beauty Essentials", "beauty-essentials", "BEAUTY01", 1299, 150, 38, "beauty", "/assets/Images/categories/beauty.jpg"),
            ("Glow Skin Care Kit", "glow-skin-care-kit", "GLOW01", 2199, 250, 27, "beauty", "/assets/Images/categories/beauty.jpg"),
            ("Matte Lipstick Trio", "matte-lipstick-trio", "LIP03", 899, 100, 62, "beauty", "/assets/Images/categories/beauty.jpg"),
            ("Hair Care Combo", "hair-care-combo", "HAIR01", 1199, 120, 46, "beauty", "/assets/Images/categories/beauty.jpg"),
            ("Cozy Home Lamp", "cozy-home-lamp", "HOME01", 899, 90, 60, "home-decor", "/assets/Images/categories/home-decor.jpg"),
            ("Minimal Wall Clock", "minimal-wall-clock", "CLOCK01", 1299, 150, 32, "home-decor", "/assets/Images/categories/home-decor.jpg"),
            ("Cotton Cushion Set", "cotton-cushion-set", "CUS04", 1599, 180, 36, "home-decor", "/assets/Images/categories/home-decor.jpg"),
            ("Ceramic Vase Pair", "ceramic-vase-pair", "VASE02", 1999, 250, 20, "home-decor", "/assets/Images/categories/home-decor.jpg"),
            ("Nonstick Cookware Set", "nonstick-cookware-set", "COOK05", 3999, 450, 18, "kitchen", "/assets/Images/categories/home-decor.jpg"),
            ("Electric Kettle", "electric-kettle", "KET01", 1299, 120, 34, "kitchen", "/assets/Images/categories/home-decor.jpg"),
            ("Storage Jar Set", "storage-jar-set", "JAR06", 999, 90, 52, "kitchen", "/assets/Images/categories/home-decor.jpg"),
            ("Chef Knife Set", "chef-knife-set", "KNIFE04", 1799, 200, 24, "kitchen", "/assets/Images/categories/home-decor.jpg")
        };

        foreach (var p in desiredProducts)
        {
            if (!categoryMap.TryGetValue(p.CategorySlug, out var catId)) continue;
            if (await db.Products.AnyAsync(x => x.Slug == p.Slug)) continue;

            db.Products.Add(new Product
            {
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Price = p.Price,
                Discount = p.Discount,
                StockQty = p.Stock,
                CategoryId = catId,
                ImageUrl = p.ImageUrl
            });
        }

        await db.SaveChangesAsync();

        var currentProductCount = await db.Products.CountAsync();
        if (currentProductCount < 1000)
        {
            var categoriesForCatalog = await db.Categories
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.Name, c.Slug, c.ImageUrl })
                .ToListAsync();

            var existingSlugs = (await db.Products
                .Select(p => p.Slug)
                .ToListAsync())
                .ToHashSet();

            var adjectives = new[]
            {
                "Premium", "Classic", "Smart", "Ultra", "Daily", "Essential", "Urban", "Luxe",
                "Fresh", "Elite", "Compact", "Modern", "Plus", "Pro", "Signature", "Comfort"
            };
            var productKinds = new[]
            {
                "Combo", "Kit", "Pack", "Edition", "Set", "Model", "Choice", "Series",
                "Bundle", "Collection", "Select", "Max", "Lite", "Prime", "Studio", "Active"
            };

            var toCreate = 1000 - currentProductCount;
            var created = 0;
            var serial = 1;

            while (created < toCreate && categoriesForCatalog.Count > 0)
            {
                foreach (var category in categoriesForCatalog)
                {
                    if (created >= toCreate) break;

                    var adjective = adjectives[serial % adjectives.Length];
                    var kind = productKinds[(serial / adjectives.Length) % productKinds.Length];
                    var name = $"{adjective} {category.Name} {kind} {serial:0000}";
                    var slug = $"{category.Slug}-{adjective.ToLowerInvariant()}-{kind.ToLowerInvariant()}-{serial:0000}";
                    if (existingSlugs.Contains(slug))
                    {
                        serial++;
                        continue;
                    }

                    var priceBase = 299 + ((serial * 173) % 95000);
                    var discount = serial % 4 == 0 ? Math.Round(priceBase * 0.08m, 0) : serial % 7 == 0 ? Math.Round(priceBase * 0.12m, 0) : 0m;

                    db.Products.Add(new Product
                    {
                        Name = name,
                        Slug = slug,
                        Sku = $"SRE-{category.Slug.ToUpperInvariant().Replace("-", "")}-{serial:0000}",
                        Price = priceBase,
                        Discount = discount,
                        StockQty = 8 + ((serial * 11) % 140),
                        CategoryId = category.Id,
                        ImageUrl = category.ImageUrl
                    });

                    existingSlugs.Add(slug);
                    created++;
                    serial++;
                }
            }

            await db.SaveChangesAsync();
        }

        var productSection = sections.FirstOrDefault(s => s.Type == (int)HomeSectionType.ProductsGrid);
        if (productSection != null && !await db.HomeSectionItems.AnyAsync(i =>
                i.HomeSectionId == productSection.Id && i.EntityType == HomeEntityType.Product))
        {
            var productIds = await db.Products
                .OrderByDescending(p => p.Id)
                .Select(p => p.Id)
                .Take(12)
                .ToListAsync();

            var order = 1;
            foreach (var id in productIds)
            {
                db.HomeSectionItems.Add(new HomeSectionItem
                {
                    HomeSectionId = productSection.Id,
                    EntityType = HomeEntityType.Product,
                    EntityId = id,
                    SortOrder = order++,
                    IsEnabled = true
                });
            }

            productSection.ItemsToShow = Math.Max(productSection.ItemsToShow, 12);
            productSection.CardsPerRow = Math.Max(productSection.CardsPerRow, 4);
            await db.SaveChangesAsync();
        }

        if (!await db.Orders.AnyAsync())
        {
            var customer = await db.Users.FirstAsync(u => u.Email == "customer@srecommerce.local");
            var demoProducts = await db.Products
                .OrderBy(p => p.Id)
                .Take(8)
                .ToListAsync();

            if (demoProducts.Count >= 4)
            {
                AddDemoOrder(customer, OrderStatuses.Delivered, DateTime.UtcNow.AddDays(-7), demoProducts[0], 1, demoProducts[3], 2);
                AddDemoOrder(customer, OrderStatuses.Processing, DateTime.UtcNow.AddDays(-3), demoProducts[1], 1, demoProducts[4], 1);
                AddDemoOrder(customer, OrderStatuses.Pending, DateTime.UtcNow.AddDays(-1), demoProducts[2], 2, demoProducts[5], 1);
                await db.SaveChangesAsync();
            }
        }

        void AddDemoOrder(User customer, string status, DateTime createdAt, Product first, int firstQty, Product second, int secondQty)
        {
            var order = new Order
            {
                UserId = customer.Id,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                Phone = "9999999999",
                ShippingAddress = "Demo Street, New Delhi",
                Status = status,
                CreatedAt = createdAt,
                ShippingFee = 99,
                OrderNumber = $"SRE-DEMO-{createdAt:MMddHHmm}"
            };

            AddItem(order, first, firstQty);
            AddItem(order, second, secondQty);
            order.Total += order.ShippingFee;
            db.Orders.Add(order);
        }

        static void AddItem(Order order, Product product, int quantity)
        {
            var discount = product.Discount ?? 0;
            var lineTotal = Math.Max(0, product.Price - discount) * quantity;
            order.Subtotal += product.Price * quantity;
            order.DiscountTotal += discount * quantity;
            order.Total += lineTotal;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Discount = discount,
                Quantity = quantity,
                LineTotal = lineTotal
            });
        }

    }
}

