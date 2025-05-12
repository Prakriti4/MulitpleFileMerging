using Microsoft.EntityFrameworkCore;
using Multiplefileintopdf.Models;

namespace Multiplefileintopdf.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        public DbSet<Record> records { get; set; }
    }
}
