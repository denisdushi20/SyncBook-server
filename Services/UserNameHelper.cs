namespace SyncBook.Server.Services;

public static class UserNameHelper
{
    public static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (string.Empty, string.Empty);
        }

        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }

    public static string JoinFullName(string firstName, string lastName) =>
        $"{firstName.Trim()} {lastName.Trim()}".Trim();
}
