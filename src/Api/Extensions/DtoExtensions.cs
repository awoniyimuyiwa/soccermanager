using Api.Models.V1;
using Domain;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace Api.Extensions;

public static class DtoExtensions
{
    public static CursorListModel<T> ToModel<T>(
        this CursorList<T> cursorList,
        IDataProtector protector,
        string? purpose) where T : class
    {
        string? protectedCursor = null;

        if (cursorList.Next is not null)
        {
            var json = JsonSerializer.Serialize(cursorList.Next);

            protectedCursor = json.Protect(protector, purpose);
        }

        return new CursorListModel<T>(
            cursorList.Items,
            protectedCursor,
            cursorList.PageSize);
    }
}


