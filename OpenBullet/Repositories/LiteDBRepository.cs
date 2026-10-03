using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using RuriLib.Interfaces;
using RuriLib.Models;

namespace OpenBulletCE.Repositories
{
    public class LiteDBRepository<T> : IRepository<T, Guid> where T : Persistable<Guid>
    { 
        // Per-DB file connection & lock pool to avoid lock contention across different databases
        private static readonly ConcurrentDictionary<string, (LiteDatabase Db, object LockObj)> _dbPool 
            = new ConcurrentDictionary<string, (LiteDatabase, object)>(StringComparer.OrdinalIgnoreCase);

        public string ConnectionString { get; set; }
        public string Collection { get; set; }

        public LiteDBRepository(string connectionString, string collection)
        {
            ConnectionString = connectionString;
            Collection = collection;
        }

        public static (LiteDatabase Db, object LockObj) GetConnection(string connectionString)
        {
            return _dbPool.GetOrAdd(connectionString, connStr =>
            {
                var dir = Path.GetDirectoryName(connStr);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var db = new LiteDatabase(connStr);
                return (db, new object());
            });
        }

        public static void CloseAll()
        {
            foreach (var kvp in _dbPool)
            {
                try
                {
                    lock (kvp.Value.LockObj)
                    {
                        kvp.Value.Db.Dispose();
                    }
                }
                catch { }
            }
            _dbPool.Clear();
        }

        public static void ShrinkAll()
        {
            foreach (var kvp in _dbPool)
            {
                try
                {
                    lock (kvp.Value.LockObj)
                    {
                        kvp.Value.Db.Shrink();
                    }
                }
                catch { }
            }
        }

        public void Add(T entity)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                coll.Insert(entity);
            }
        }

        public void Add(IEnumerable<T> entities)
        {
            var list = entities.ToList();
            foreach (var e in list)
            {
                if (e.Id == Guid.Empty)
                {
                    e.Id = Guid.NewGuid();
                }
            }

            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                coll.InsertBulk(list);
            }
        }

        public IEnumerable<T> Get()
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                return coll.FindAll().ToList();
            }
        }

        public T Get(Guid id)
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                return coll.FindById(id);
            }
        }

        public void Remove(T entity)
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                coll.Delete(entity.Id);
            }
        }

        public void Remove(IEnumerable<T> entities)
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                foreach (var entity in entities)
                {
                    coll.Delete(entity.Id);
                }
            }
        }

        public void RemoveAll()
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                conn.Db.DropCollection(Collection);
            }
        }

        public void Update(T entity)
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                coll.Update(entity);
            }
        }

        public void Update(IEnumerable<T> entities)
        {
            var conn = GetConnection(ConnectionString);
            lock (conn.LockObj)
            {
                var coll = conn.Db.GetCollection<T>(Collection);
                coll.Update(entities);
            }
        }
    }
}
