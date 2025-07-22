using WebScrapperApi.Models;
using System.Collections.Generic;

namespace WebScrapperApi.Configuration
{
    public static class MetroConfig
    {
        public const string METRO_BASE_URL = "https://www.metro.co.uk/"; // Update as needed

        public static readonly List<Category> METRO_CATEGORIES = new List<Category>
        {
            new() { Name = "packaging" , Url = "https://metrocashandcarryhull.com/products/Packaging-c161316305"},
            new() { Name = "drinks", Url = "https://metrocashandcarryhull.com/products/Drinks-c161318810"},
            new() { Name = "Bakery", Url = "https://metrocashandcarryhull.com/products/Bakery-c161314552"},
            new() { Name = "cooking-ingredients", Url = "https://metrocashandcarryhull.com/products/Cooking-Ingredients-c161312041" },
            new() { Name = "dairy", Url = "https://metrocashandcarryhull.com/products/Dairy-c161312042"},
            new() { Name = "fruit-vegetables", Url = "https://metrocashandcarryhull.com/products/Fruit-&-Vegetables-c161314553" },
            new() { Name = "hygiene", Url = "https://metrocashandcarryhull.com/products/Hygiene-c161312043" },
            new() { Name = "meat-poultry", Url = "https://metrocashandcarryhull.com/products/Meat-&-Poultry-c161309066" },
            new() { Name = "oils", Url = "https://metrocashandcarryhull.com/products/Oils-c161315052" },
            new() { Name = "potato-products-sides", Url = "https://metrocashandcarryhull.com/products/Potato-Products-&-Sides-c161315815" },
            new() { Name = "sauces-condiments", Url = "https://metrocashandcarryhull.com/products/Sauces-&-Condiments-c161316793" },
            new() { Name = "spices-herbs", Url = "https://metrocashandcarryhull.com/products/Spices-&-Herbs-c161312044" },
            new() { Name = "desserts", Url = "https://metrocashandcarryhull.com/products/Desserts-c163075752" },
            new() { Name = "rice-flour", Url = "https://metrocashandcarryhull.com/products/Rice-&-Flour-c168699321" },
        };

        // main selectors for Metro products
        public static class MetroSelectors
        {
            public const string PRODUCT_GRID = "div.grid__products";
            public const string PRODUCT_ITEM = "div.grid-product__wrap";
            public const string PRODUCT_NAME = ".grid-product__title";
            public const string PRODUCT_PRICE = ".grid-product__price";
            public const string PRODUCT_PRICE_ALT = ".ec-price-item";
            public const string PRODUCT_URL = "a.grid-product__title";
            public const string PRODUCT_IMAGE_WRAP = ".grid-product__image-wrap";
            public const string PRODUCT_IMAGE = "img";
            public const string NEXT_PAGE = "a.pager__button.pager__button--next";
        }
    }
}