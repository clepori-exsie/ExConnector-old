namespace ExConnector.Core.DTOs;

/// <summary>
/// Résultat paginé pour une liste de tiers
/// </summary>
public record TiersListResult(IReadOnlyList<TiersDto> Items, int Total);

