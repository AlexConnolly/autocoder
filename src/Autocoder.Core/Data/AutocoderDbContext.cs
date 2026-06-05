using Autocoder.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Autocoder.Core.Data;

public class AutocoderDbContext : DbContext
{
    public AutocoderDbContext(DbContextOptions<AutocoderDbContext> options) : base(options) { }

    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<ColumnShellCommand> ColumnShellCommands => Set<ColumnShellCommand>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ContextEntry> ContextEntries => Set<ContextEntry>();
    public DbSet<BoardRepository> Repositories => Set<BoardRepository>();
    public DbSet<TaskRepository> TaskRepositories => Set<TaskRepository>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Board>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasMany(b => b.Columns).WithOne(c => c.Board).HasForeignKey(c => c.BoardId);
            e.HasMany(b => b.Repositories).WithOne(r => r.Board).HasForeignKey(r => r.BoardId);
            e.HasMany(b => b.Tasks).WithOne(t => t.Board).HasForeignKey(t => t.BoardId);
        });

        mb.Entity<Column>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasMany(c => c.ShellCommands)
             .WithOne(s => s.Column)
             .HasForeignKey(s => s.ColumnId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ColumnShellCommand>(e => e.HasKey(s => s.Id));

        mb.Entity<WorkTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasMany(t => t.ContextEntries).WithOne(ce => ce.Task).HasForeignKey(ce => ce.TaskId);
            e.HasMany(t => t.TaskRepositories).WithOne(tr => tr.Task).HasForeignKey(tr => tr.TaskId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<ContextEntry>(e => e.HasKey(ce => ce.Id));
        mb.Entity<BoardRepository>(e => e.HasKey(r => r.Id));
        mb.Entity<TaskRepository>(e =>
        {
            e.HasKey(tr => tr.Id);
            e.HasOne(tr => tr.Repository).WithMany().HasForeignKey(tr => tr.RepositoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
