namespace Pohoda.Contract.Models;

/// <summary>One skladová karta match returned by a name search.</summary>
public sealed record StockNameMatch(string Code, string Name);
