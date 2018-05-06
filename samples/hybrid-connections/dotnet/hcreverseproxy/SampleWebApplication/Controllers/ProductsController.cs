using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Http;
using System.Web.Http.Results;

namespace SampleWebApplication.Controllers
{
    public class ProductsController : ApiController
    {
        static List<Product> products = new List<Product>
        {
            new Product { Id = 1, Name = "Tomato Soup", Category = "Groceries", Price = 1 },
            new Product { Id = 2, Name = "Yo-yo", Category = "Toys", Price = 3.75M },
            new Product { Id = 3, Name = "Hammer", Category = "Hardware", Price = 16.99M }
        };

        public IEnumerable<Product> GetAllProducts()
        {
            Console.WriteLine($"GET {this.Request.RequestUri}");
            return products;
        }

        [HttpGet]
        public Product GetProductById(int id)
        {
            Console.WriteLine($"GET {this.Request.RequestUri}");
            var product = products.FirstOrDefault((p) => p.Id == id);
            if (product == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            return product;
        }

        public IEnumerable<Product> GetProductsByCategory(string category)
        {
            Console.WriteLine($"GET {this.Request.RequestUri}");
            return products.Where(p => string.Equals(p.Category, category,
                    StringComparison.OrdinalIgnoreCase));
        }

        public IHttpActionResult DeleteProductById(int id)
        {
            Console.WriteLine($"DELETE {this.Request.RequestUri}");
            var product = products.FirstOrDefault((p) => p.Id == id);
            if (product != null)
            {
                products.Remove(product);
                return this.Ok();
            }
            else
            {
                return new StatusCodeResult(HttpStatusCode.NoContent, this);
            }
        }

        public IHttpActionResult PostProduct(Product product)
        {
            Console.WriteLine($"POST {this.Request.RequestUri}");
            products.Add(product);
            return CreatedAtRoute("DefaultApi", new { id = product.Id }, product);
        }
    }
}
