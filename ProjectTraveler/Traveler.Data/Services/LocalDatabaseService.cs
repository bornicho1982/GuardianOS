using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using Traveler.Core.Interfaces;

namespace Traveler.Data.Services;

public class LocalDatabaseService : ILocalDatabaseService, IDisposable
{
    private readonly LiteDatabase _database;
    private const string DbName = "user_data.db";

    public LocalDatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "GuardianOS");
        
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        
        var dbPath = Path.Combine(folder, DbName);
        _database = new LiteDatabase(dbPath);
    }

    public void Save<T>(T item, string collectionName)
    {
        var collection = _database.GetCollection<T>(collectionName);
        collection.Upsert(item);
    }

    public T? Load<T>(string collectionName, Func<T, bool> predicate)
    {
        var collection = _database.GetCollection<T>(collectionName);
        // LiteDB requires Expression<Func<T,bool>> for FindOne if efficient, 
        // but passing Func is tricky with LiteDB's API directly expecting BsonExpression or Lambda Expression.
        // However, standard FindOne overloads exist. FindOne(Expression<Func<T, bool>> predicate).
        // The interface defines Func<T, bool>. I should change interface to Expression or convert.
        // Simplest: loading all and filtering (bad for perf) or change interface.
        // Better: Use FindOne(x => predicate(x)) but LINQ provider must understand it.
        // Best for now: Assume interface intends Expression, but it's defined as Func.
        // I will implement it by loading all/querying if simple, or use FindAll and FirstOrDefault.
        
        // Actually, LiteDB's FindOne takes an Expression<Func<T, bool>>.
        // If my interface uses Func<T, bool>, I cannot directly pass it to LiteDB efficiently.
        // I will ignore the interface limitation for a moment and just implement what works, 
        // effectively doing client-side eval if needed, OR change the interface to Expression.
        // Given I just wrote the interface, I should update it to Expression<Func<T, bool>> for better perf.
        
        // For now, simple implementation to satisfy interface:
        return collection.FindAll().FirstOrDefault(predicate);
    }
    
    // Optimizing Load to accept Expression would be better, but let's stick to the interface contract I just made.
    // Wait, I can try to cast or just use LINQ.
    
    public IEnumerable<T> LoadAll<T>(string collectionName)
    {
        var collection = _database.GetCollection<T>(collectionName);
        return collection.FindAll();
    }

    public void Delete<T>(string collectionName, Func<T, bool> predicate)
    {
        var collection = _database.GetCollection<T>(collectionName);
        var items = collection.FindAll().Where(predicate).ToList();
        
        // LiteDB needs ID to delete efficiently, or DeleteMany with expression.
        // Deleting by predicate implies finding keys first.
        // This is a naive implementation but correct for small datasets (Loadouts, Tokens).
        foreach (var item in items)
        {
             // Get ID using Mapper
             var doc = _database.Mapper.ToDocument(item);
             if (doc.TryGetValue("_id", out var id))
             {
                 collection.Delete(id);
             }
        }
    }

    public void DeleteAll<T>(string collectionName)
    {
        var collection = _database.GetCollection<T>(collectionName);
        collection.DeleteAll();
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
