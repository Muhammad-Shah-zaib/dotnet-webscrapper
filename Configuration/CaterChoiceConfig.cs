

namespace WebScrapperApi.Configuration
{
    public static class CaterChoiceConfig
    {
        public const string CATER_CHOICE_BASE_URL = "https://cater-choice.com/";

        public static readonly List<Category> CATER_CHOICE_CATEGORIES = new()
        {
            new Category { Name = "appetizers-and-sides", Url = "https://cater-choice.com/product-category/appetizers-and-sides" },
            new Category { Name = "bakery", Url = "https://cater-choice.com/product-category/bakery" },
            new Category { Name = "breading-batter", Url = "https://cater-choice.com/product-category/breading-batter"},
            new Category { Name = "burger", Url = "https://cater-choice.com/product-category/burger"},
            new Category { Name = "chicken-and-poultry", Url = "https://cater-choice.com/product-category/chicken-and-poultry"},
            new Category { Name = "cooking-ingredients", Url = "https://cater-choice.com/product-category/chocolates-and-snacks"},
            new Category { Name = "dairy-egg", Url = "https://cater-choice.com/product-category/cooking-ingredients"},
            new Category { Name = "chocolates-and-snacks", Url = "https://cater-choice.com/product-category/dairy-egg"},
            new Category { Name = "dessert-and-ice-cream", Url = "https://cater-choice.com/product-category/dessert-and-ice-cream"},
            new Category { Name = "doners-kebabs", Url = "https://cater-choice.com/product-category/doners-kebabs"},
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
} 