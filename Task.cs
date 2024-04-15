// Models

public class Product
{
    public Guid ProductId { get; set; }
    public string Name { get; set; }
    public ICollection<Lot> Lots { get; set; }
}

public class Lot
{
    public Guid LotId { get; set; }
    public Guid ProductId { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public Product Product { get; set; }
    public decimal Quantity { get; set; }  
}

public class ProductLotQuantity  
{
    public Guid LotId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class ProductLotTransaction
{
    public Guid ProductLotTransactionId { get; set; }
    public Guid? LotId { get; set; }  
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public TransactionType Type { get; set; }

    public Lot Lot { get; set; }  
}

public enum TransactionType
{
    LIFO,
    FIFO
}


// Entity context, Fluent API

public class InventoryContext : DbContext
{
    public InventoryContext(DbContextOptions<InventoryContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<Lot> Lots { get; set; }
    public DbSet<ProductLotTransaction> ProductLotTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Lots)
            .WithOne(l => l.Product)
            .HasForeignKey(l => l.ProductId);

        modelBuilder.Entity<ProductLotTransaction>()
            .HasOne(t => t.Lot)
            .WithMany()
            .HasForeignKey(t => t.LotId);

         
    }
}


// Strategy Pattern
public interface IProductLotSelectionStrategy
{
    Lot SelectLot(InventoryContext context, Guid productId, TransactionType type);
}

public class LifoSelectionStrategy : IProductLotSelectionStrategy
{
    public Lot SelectLot(InventoryContext context, Guid productId, TransactionType type)
    {
        return context.Lots.Where(l => l.ProductId == productId)
            .OrderBy(l => l.CreatedAt)
            .FirstOrDefault();
    }
}

public class FifoSelectionStrategy : IProductLotSelectionStrategy
{
    public Lot SelectLot(InventoryContext context, Guid productId, TransactionType type)
    {
        return context.Lots.Where(l => l.ProductId == productId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefault();
    }
}


// Controllers,  CRUD Operations

using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

[ApiController]
public class ProductController : ControllerBase
{
    private readonly InventoryContext _context;

    public ProductController(InventoryContext context)
    {
        _context = context;
    }

     

    [HttpPost("/products")]
    public IActionResult CreateProduct(Product product)
    {
        _context.Products.Add(product);
        _context.SaveChanges();
        return Ok(product);
    }

    [HttpGet("/products")]
    public IActionResult GetAllProducts()
    {
        var products = _context.Products.ToList();
        return Ok(products);
    }

    [HttpGet("/products/{id}")]
    public IActionResult GetProduct(Guid id)
    {
        var product = _context.Products.Find(id);
        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPut("/products/{id}")]
    public IActionResult UpdateProduct(Guid id, Product updatedProduct)
    {
        var existingProduct = _context.Products.Find(id);
        if (existingProduct == null)
            return NotFound();

        existingProduct.Name = updatedProduct.Name;
        _context.SaveChanges();
        return Ok(existingProduct);
    }

    [HttpDelete("/products/{id}")]
    public IActionResult DeleteProduct(Guid id)
    {
        var product = _context.Products.Find(id);
        if (product == null)
            return NotFound();

        _context.Products.Remove(product);
        _context.SaveChanges();
        return Ok();
    }
}

[ApiController]
public class LotController : ControllerBase
{
    private readonly InventoryContext _context;

    public LotController(InventoryContext context)
    {
        _context = context;
    }

   

    [HttpPost("/lots")]
    public IActionResult CreateLot(Lot lot)
    {
        _context.Lots.Add(lot);
        _context.SaveChanges();
        return Ok(lot);
    }

    [HttpGet("/lots")]
    public IActionResult GetAllLots()
    {
        var lots = _context.Lots.ToList();
        return Ok(lots);
    }

    [HttpGet("/lots/{id}")]
    public IActionResult GetLot(Guid id)
    {
        var lot = _context.Lots.Find(id);
        if (lot == null)
            return NotFound();

        return Ok(lot);
    }

    [HttpPut("/lots/{id}")]
    public IActionResult UpdateLot(Guid id, Lot updatedLot)
    {
        var existingLot = _context.Lots.Find(id);
        if (existingLot == null)
            return NotFound();

        existingLot.Description = updatedLot.Description;
        _context.SaveChanges();
        return Ok(existingLot);
    }

    [HttpDelete("/lots/{id}")]
    public IActionResult DeleteLot(Guid id)
    {
        var lot = _context.Lots.Find(id);
        if (lot == null)
            return NotFound();

        _context.Lots.Remove(lot);
        _context.SaveChanges();
        return Ok();
    }
}

[ApiController]
public class ProductLotQuantityController : ControllerBase
{
    private readonly InventoryContext _context;

    public ProductLotQuantityController(InventoryContext context)
    {
        _context = context;
    }

     

    [HttpGet("/product-lot-quantities")]
    public IActionResult GetProductLotQuantities()
    {
        var productLotQuantities = _context.Lots
            .Select(l => new ProductLotQuantity
            {
                LotId = l.LotId,
                ProductId = l.ProductId,
                Quantity = l.Quantity
            })
            .ToList();
        return Ok(productLotQuantities);
    }
}

[ApiController]
public class ProductLotTransactionController : ControllerBase
{
    private readonly InventoryContext _context;
    private readonly Dictionary<TransactionType, IProductLotSelectionStrategy> _strategies;

    public ProductLotTransactionController(InventoryContext context, Dictionary<TransactionType, IProductLotSelectionStrategy> strategies)
    {
        _context = context;
        _strategies = strategies;
    }

   

    [HttpPost("/product-lot-transactions")]
    public IActionResult CreateProductLotTransaction(ProductLotTransaction transaction)
    {
        Lot selectedLot = null;
        if (transaction.LotId == null)
        {
            var strategy = _strategies[transaction.Type];
            selectedLot = strategy.SelectLot(_context, transaction.ProductId, transaction.Type);
            transaction.LotId = selectedLot.LotId;
        }

        _context.ProductLotTransactions.Add(transaction);

        // Update corresponding ProductLotQuantity
        if (selectedLot != null)
        {
            var productLotQuantity = _context.Lots
                .Where(l => l.LotId == selectedLot.LotId && l.ProductId == transaction.ProductId)
                .Select(l => new ProductLotQuantity
                {
                    LotId = l.LotId,
                    ProductId = l.ProductId,
                    Quantity = l.Quantity
                })
                .FirstOrDefault();

            if (productLotQuantity != null)
                productLotQuantity.Quantity += transaction.Quantity;
        }

        _context.SaveChanges();
        return Ok(transaction);
    }

    [HttpGet("/product-lot-transactions")]
    public IActionResult GetProductLotTransactions()
    {
        var transactions = _context.ProductLotTransactions.ToList();
        return Ok(transactions);
    }

 
    [HttpPut("/product-lot-transactions")]
    [HttpDelete("/product-lot-transactions")]
    public IActionResult MethodNotAllowed()
    {
        return StatusCode(405, "Method Not Allowed");
    }
}
