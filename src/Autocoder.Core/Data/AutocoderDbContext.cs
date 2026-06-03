using Autocoder.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Autocoder.Core.Data;

public class AutocoderDbContext : DbContext
{
    public AutocoderDbContext(DbContextOptions<AutocoderDbContext> options) : base(options) { }

    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ContextEntry> ContextEntries => Set<ContextEntry>();
    public DbSet<BoardRepository> Repositories => Set<BoardRepository>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Board>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasMany(b => b.Columns).WithOne(c => c.Board).HasForeignKey(c => c.BoardId);
            e.HasMany(b => b.Repositories).WithOne(r => r.Board).HasForeignKey(r => r.BoardId);
            e.HasMany(b => b.Tasks).WithOne(t => t.Board).HasForeignKey(t => t.BoardId);
        });

        mb.Entity<Column>(e => e.HasKey(c => c.Id));
        mb.Entity<WorkTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasMany(t => t.ContextEntries).WithOne(ce => ce.Task).HasForeignKey(ce => ce.TaskId);
        });
        mb.Entity<ContextEntry>(e => e.HasKey(ce => ce.Id));
        mb.Entity<BoardRepository>(e => e.HasKey(r => r.Id));
    }
}
