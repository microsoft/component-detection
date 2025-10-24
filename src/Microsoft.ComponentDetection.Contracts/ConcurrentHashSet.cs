#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>Represents a thread-safe set of values.</summary>
/// <typeparam name="T">The type of elements in the hash set.</typeparam>
public class ConcurrentHashSet<T> : IEnumerable<T>
{
    private readonly ConcurrentDictionary<T, byte> dictionary;

    // Create different constructors for different equality comparers
    public ConcurrentHashSet() => this.dictionary = new ConcurrentDictionary<T, byte>();

    public ConcurrentHashSet(IEqualityComparer<T> comparer) => this.dictionary = new ConcurrentDictionary<T, byte>(comparer);

    /// <summary>Adds the specific element to the <see cref="ConcurrentHashSet{T}"/> object.</summary>
    /// <param name="item">The element to add to the set.</param>
    /// <returns>true if element was added to <see cref="ConcurrentHashSet{T}"/> object; false, if item was already present.</returns>
    public bool Add(T item)
    {
        return this.dictionary.TryAdd(item, 0);
    }

    /// <summary>Removes the specific element to the <see cref="ConcurrentHashSet{T}"/> object.</summary>
    /// <param name="item">The element to be removed from the set.</param>
    /// <returns>true if element was successfully found and removed; otherwise, false.</returns>
    public bool Remove(T item)
    {
        return this.dictionary.TryRemove(item, out _);
    }

    /// <summary>Determines whether the <see cref="ConcurrentHashSet{T}"/> contains the specified element.</summary>
    /// <param name="item">The element to locate in the <see cref="ConcurrentHashSet{T}"/> object.</param>
    /// <returns>true if the <see cref="ConcurrentHashSet{T}"/> object contains the specified element; otherwise, false.</returns>
    public bool Contains(T item)
    {
        return this.dictionary.ContainsKey(item);
    }

    /// <summary>Removes all elements from a <see cref="ConcurrentHashSet{T}"/> object.</summary>
    public void Clear() => this.dictionary.Clear();

    public ISet<T> ToHashSet()
    {
        return new HashSet<T>(this.dictionary.Keys);
    }

    /// <summary>Returns an enumerator that iterates through the <see cref="ConcurrentHashSet{T}"/>.</summary>
    /// <returns>An enumerator for the <see cref="ConcurrentHashSet{T}"/>.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return this.dictionary.Keys.GetEnumerator();
    }

    /// <summary>Returns an enumerator that iterates through the <see cref="ConcurrentHashSet{T}"/>.</summary>
    /// <returns>An enumerator for the <see cref="ConcurrentHashSet{T}"/>.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
