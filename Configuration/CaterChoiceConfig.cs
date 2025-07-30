

namespace WebScrapperApi.Configuration;

public static class CaterChoiceConfig
{
    public const string CATER_CHOICE_BASE_URL = "https://cater-choice.com/";

    public static readonly List<Category> CATER_CHOICE_CATEGORIES = new()
    {
        new Category { Name = "appetizers-and-sides", Url = "https://cater-choice.com/product-category/appetizers-and-sides" },
        new Category { Name = "bakery", Url = "https://cater-choice.com/product-category/bakery" },
        new Category { Name = "breading-batter", Url = "https://cater-choice.com/product-category/breading-batter" },
        new Category { Name = "burger", Url = "https://cater-choice.com/product-category/burger" },
        new Category { Name = "chicken-and-poultry", Url = "https://cater-choice.com/product-category/chicken-and-poultry" },
        new Category { Name = "chocolates-and-snacks", Url = "https://cater-choice.com/product-category/chocolates-and-snacks" },
        new Category { Name = "cooking-ingredients", Url = "https://cater-choice.com/product-category/cooking-ingredients" },
        new Category { Name = "dairy-egg", Url = "https://cater-choice.com/product-category/dairy-egg" },
        new Category { Name = "dessert-and-ice-cream", Url = "https://cater-choice.com/product-category/dessert-and-ice-cream" },
        new Category { Name = "doners-kebabs", Url = "https://cater-choice.com/product-category/doners-kebabs" },
        new Category { Name = "drinks", Url = "https://cater-choice.com/product-category/drinks" },
        new Category { Name = "fish-and-seafood", Url = "https://cater-choice.com/product-category/fish-and-seafood" },
        new Category { Name = "flour", Url = "https://cater-choice.com/product-category/flour" },
        new Category { Name = "fruits-and-nuts", Url = "https://cater-choice.com/product-category/fruits-and-nuts" },
        new Category { Name = "honey-and-spread", Url = "https://cater-choice.com/product-category/honey-and-spread" },
        new Category { Name = "hygiene", Url = "https://cater-choice.com/product-category/hygiene" },
        new Category { Name = "kitchen-equipments", Url = "https://cater-choice.com/product-category/kitchen-equipments" },
        new Category { Name = "latest-product", Url = "https://cater-choice.com/product-category/latest-product" },
        new Category { Name = "meat", Url = "https://cater-choice.com/product-category/meat" },
        new Category { Name = "ms-frozen-and-chilled", Url = "https://cater-choice.com/product-category/ms-frozen-and-chilled" },
        new Category { Name = "new-products", Url = "https://cater-choice.com/product-category/new-products" },
        new Category { Name = "oil", Url = "https://cater-choice.com/product-category/oil" },
        new Category { Name = "packaging", Url = "https://cater-choice.com/product-category/packaging" },
        new Category { Name = "pastry", Url = "https://cater-choice.com/product-category/pastry" },
        new Category { Name = "potato", Url = "https://cater-choice.com/product-category/potato" },
        new Category { Name = "rice-pasta-dried-foods", Url = "https://cater-choice.com/product-category/rice-pasta-dried-foods" },
        new Category { Name = "sandwich-filings", Url = "https://cater-choice.com/product-category/sandwich-filings" },
        new Category { Name = "sauces-dressings-and-relishes", Url = "https://cater-choice.com/product-category/sauces-dressings-and-relishes" },
        new Category { Name = "sealing-materials", Url = "https://cater-choice.com/product-category/sealing-materials" },
        new Category { Name = "stationery", Url = "https://cater-choice.com/product-category/stationery" },
        new Category { Name = "sugar-and-sweeteners", Url = "https://cater-choice.com/product-category/sugar-and-sweeteners" },
        new Category { Name = "vegetables", Url = "https://cater-choice.com/product-category/vegetables" },
        new Category { Name = "vegetarian-and-vegan", Url = "https://cater-choice.com/product-category/vegetarian-and-vegan" }
    };

    public static class CaterChoiceSelectors
    {
        public const string PRODUCT_GRID = ".grid.md\\:grid-cols-12.sm\\:grid-cols-1.gap-4";
        public const string PRODUCT_CONTAINER = ".gridcontroll";
        public const string PRODUCT_ITEM_XPATH = "/html/body/main/form/section/div/div/div[2]/div[2]/div";
            
        // Product detail selectors
        public const string PRODUCT_NAME = ".text-center h3 a";
        public const string PRODUCT_IMAGE = ".mb-\\[15px\\] img";
        public const string PACK_SIZE = ".text-center div.truncate strong";
        public const string CASE_PRICE = ".custom_design_hm > div:nth-of-type(1) strong";
        public const string SINGLE_PRICE = ".custom_design_hm > div:nth-of-type(2) strong";
        public const string PRODUCT_CODE = "body > main > section.py-\\[40px\\] > div > div > div.xl\\:col-span-8.lg\\:col-span-7 > h5:nth-child(2)";
        public const string PRODUCT_DESCRIPTION = ".woocommerce-product-details__short-description";
    }
}