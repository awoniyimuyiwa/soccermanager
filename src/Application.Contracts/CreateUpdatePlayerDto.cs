
using Domain;

namespace Application.Contracts;

public record CreateUpdatePlayerDto(
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    int Type);


public record CreatePlayerDto(
    string? Country,
    DateOnly DateOfBirth ,
    string? FirstName,
    string? LastName,
    int Type,
    decimal Value = Constants.InitialPlayerValue)
    : CreateUpdatePlayerDto(
        Country,
        DateOfBirth,
        FirstName,
        LastName,
        Type);
public record UpdatePlayerDto(
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    int Type,
    string ConcurrencyStamp)
    : CreateUpdatePlayerDto(
        Country,
        DateOfBirth,
        FirstName,
        LastName,
        Type);
