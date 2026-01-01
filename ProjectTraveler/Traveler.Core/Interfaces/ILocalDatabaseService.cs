using System;
using System.Collections.Generic;

namespace Traveler.Core.Interfaces;

public interface ILocalDatabaseService
{
    void Save<T>(T item, string collectionName);
    T? Load<T>(string collectionName, Func<T, bool> predicate);
    IEnumerable<T> LoadAll<T>(string collectionName);
    void Delete<T>(string collectionName, Func<T, bool> predicate);
    void DeleteAll<T>(string collectionName);
}
