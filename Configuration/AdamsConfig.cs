namespace WebScrapperApi.Configuration
{
    public static class AdamsConfig
    {
        public const string ADAMS_BASE_URL = "https://adamsfoodservice.com/";

        // TODO: Replace with actual category URLs from Adams Food Service
        public static readonly List<Category> ADAMS_CATEGORIES =
            [
                new Category { Name = "appetizers", Url = "https://adamsfoodservice.com/product-category/appetizers/" },
                new Category { Name = "burgers-kebabs", Url = "https://adamsfoodservice.com/product-category/burgers-kebabs/" },
                new Category { Name = "chips-potatoes", Url = "https://adamsfoodservice.com/product-category/chips-potatoes/" },
                new Category { Name = "cleaning-hygiene", Url = "https://adamsfoodservice.com/product-category/cleaning-hygiene/" },
                new Category { Name = "cooking-ingredients", Url = "https://adamsfoodservice.com/product-category/cooking-ingredients/" },
                new Category { Name = "dairy-eggs", Url = "https://adamsfoodservice.com/product-category/dairy-eggs/" },
                new Category { Name = "desserts", Url = "https://adamsfoodservice.com/product-category/desserts/" },
                new Category { Name = "drinks", Url = "https://adamsfoodservice.com/product-category/drinks/" },
                new Category { Name = "flour-breading", Url = "https://adamsfoodservice.com/product-category/flour-breading/" },
                new Category { Name = "fruit-veg", Url = "https://adamsfoodservice.com/product-category/fruit-veg/" },
                new Category { Name = "meats", Url = "https://adamsfoodservice.com/product-category/meats/" },
                new Category { Name = "packaging", Url = "https://adamsfoodservice.com/product-category/packaging/" },
                new Category { Name = "pastry-bread", Url = "https://adamsfoodservice.com/product-category/pastry-bread/" },
                new Category { Name = "poultry", Url = "https://adamsfoodservice.com/product-category/poultry/" },
                new Category { Name = "rice-lentils", Url = "https://adamsfoodservice.com/product-category/rice-lentils/" },
                new Category { Name = "sauces-dressings", Url = "https://adamsfoodservice.com/product-category/sauces-dressings/" },
                new Category { Name = "seafood", Url = "https://adamsfoodservice.com/product-category/seafood/" }
            ];


        public static class AdamsSelectors
        {
            // Product list and item selectors
            public const string PRODUCT_LIST = "ul.wc-block-product-template__responsive.wc-block-product-template";
            public const string PRODUCT_ITEM = "ul.wc-block-product-template__responsive li.wc-block-product";
            // Product detail selectors (relative to product item)
            public const string PRODUCT_NAME_RELATIVE = "h6 a";
            public const string PRODUCT_SKU_RELATIVE = ".wc-block-components-product-sku span.sku";
            public const string PRODUCT_IMAGE_RELATIVE = "img";

            // Load more functionality
            public const string LOAD_MORE_BUTTON = "A.wp-block-query-pagination-next";

            // Alternative selectors for fallback
            public const string PRODUCT_GRID = ".wp-block-woocommerce-product-collection";
            public const string PRODUCT_CONTAINER = ".products";
        }
    }
}