using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using MedConnect.Data;

namespace MedConnect.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<T> _dbSet;
    private readonly ILogger<Repository<T>> _logger;

    public Repository(ApplicationDbContext context, ILogger<Repository<T>> logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        _logger.LogInformation("Fetching all entities of type {EntityType}", typeof(T).Name);
        return await _dbSet.ToListAsync();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Fetching entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        _logger.LogInformation("Finding entities of type {EntityType} with predicate", typeof(T).Name);
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        _logger.LogInformation("Adding entity of type {EntityType}", typeof(T).Name);
        await _dbSet.AddAsync(entity);
    }

    public Task UpdateAsync(T entity)
    {
        _logger.LogInformation("Updating entity of type {EntityType}", typeof(T).Name);
        _dbSet.Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        _logger.LogInformation("Deleting entity of type {EntityType}", typeof(T).Name);
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        _logger.LogInformation("Saving changes to the database for type {EntityType}", typeof(T).Name);
        await _context.SaveChangesAsync();
    }
}