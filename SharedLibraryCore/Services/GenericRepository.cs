using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Services
{
    // https://stackoverflow.com/questions/43677906/crud-operations-with-entityframework-using-generic-type
    public class GenericRepository<TEntity> where TEntity : class
    {
        private dynamic _context;
        private DbSet<TEntity> _dbSet;

        ~GenericRepository()
        {
            _context.Dispose();
        }

        protected DbContext Context
        {
            get
            {
                if (_context == null)
                {
                    _context = new DatabaseContext();
                }

                return _context;
            }
        }

        protected DbSet<TEntity> DBSet
        {
            get
            {
                if (_dbSet == null)
                {
                    _dbSet = this.Context.Set<TEntity>();
                }

                return _dbSet;
            }
        }

        public virtual async Task<IList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderExpression = null)
        {
            return await this.GetQuery(predicate, orderExpression).ToListAsync();
        }

        public virtual IEnumerable<TEntity> Find(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderExpression = null)
        {
            return this.GetQuery(predicate, orderExpression).AsEnumerable();
        }

        public virtual IQueryable<TEntity> GetQuery(Expression<Func<TEntity, bool>> predicate = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderExpression = null)
        {
            IQueryable<TEntity> qry = this.DBSet;

            foreach (var property in this.Context.Model.FindEntityType(typeof(TEntity)).GetNavigations())
                qry = qry.Include(property.Name);


            if (predicate != null)
                qry = qry.Where(predicate);

            if (orderExpression != null)
                return orderExpression(qry);


            return qry;
        }

        public virtual void Insert<T>(T entity) where T : class
        {
            DbSet<T> dbSet = this.Context.Set<T>();
            dbSet.Add(entity);
        }

        public virtual TEntity Insert(TEntity entity)
        {
            return DBSet.Add(entity).Entity;
        }

        public virtual void Update<T>(T entity) where T : class
        {
            DbSet<T> dbSet = this.Context.Set<T>();
            dbSet.Attach(entity);
            this.Context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void Update(TEntity entity)
        {
            this.Attach(entity);
            this.Context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void Delete<T>(T entity) where T : class
        {
            DbSet<T> dbSet = this.Context.Set<T>();

            if (this.Context.Entry(entity).State == EntityState.Detached)
                dbSet.Attach(entity);

            dbSet.Remove(entity);

        }

        public virtual void Delete(TEntity entity)
        {
            if (this.Context.Entry(entity).State == EntityState.Detached)
                this.Attach(entity);

            this.DBSet.Remove(entity);

        }

        public virtual void Delete<T>(object[] id) where T : class
        {
            DbSet<T> dbSet = this.Context.Set<T>();
            T entity = dbSet.Find(id);
            dbSet.Attach(entity);
            dbSet.Remove(entity);

        }

        public virtual void Delete(object id)
        {
            TEntity entity = this.DBSet.Find(id);
            this.Delete(entity);
        }


        public virtual void Attach(TEntity entity)
        {
            if (this.Context.Entry(entity).State == EntityState.Detached)
                this.DBSet.Attach(entity);
        }

        public virtual void SaveChanges()
        {
            this.Context.SaveChanges();
        }

        public virtual Task SaveChangesAsync()
        {
            return this.Context.SaveChangesAsync();
        }

    }
}
